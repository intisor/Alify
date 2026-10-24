using Alify.Core.Infrastructure.Security;
using Alify.Core.Models;
using Alify.Core.Services;
using Alify.Features.Spotify.Events;
using Alify.Features.Spotify.Services;
using Alify.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;
using Xunit;

namespace Alify.Spotify.Tests;

public class SpotifyAuthPersistenceTests
{
    private class MockSession : ISession
    {
        private readonly Dictionary<string, byte[]> _storage = new();

        public bool IsAvailable => true;
        public string Id => "mock-session-id";
        public IEnumerable<string> Keys => _storage.Keys;

        public void Clear() => _storage.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _storage.Remove(key);
        public void Set(string key, byte[] value) => _storage[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _storage.TryGetValue(key, out value!);
    }

    private class MockTokenStore : ISpotifyTokenStore
    {
        public SpotifyAuthData? StoredData { get; set; }

        public Task<SpotifyAuthData?> GetTokensAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StoredData);
        }

        public Task SaveTokensAsync(SpotifyAuthData data, CancellationToken cancellationToken = default)
        {
            StoredData = data;
            return Task.CompletedTask;
        }

        public Task ClearTokensAsync(CancellationToken cancellationToken = default)
        {
            StoredData = null;
            return Task.CompletedTask;
        }
    }

    private (SpotifyService service, MockSession session, MockTokenStore store) CreateService(
        MockTokenStore? tokenStore = null)
    {
        var session = new MockSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Session = session;

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var options = Options.Create(new SpotifyOptions
        {
            ClientId = "test-client-id",
            RedirectUri = "http://localhost:5001/callback"
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var freeLyrics = new FreeLyricsProviderService(new HttpClient(), NullLogger<FreeLyricsProviderService>.Instance);
        var requestCache = new SpotifyRequestCache(cache, NullLogger<SpotifyRequestCache>.Instance, freeLyrics);
        var sseService = new SseService(NullLogger<SseService>.Instance);
        var queueService = new QueueService(cache, requestCache, new ArtistLyricService(), NullLogger<QueueService>.Instance, freeLyrics);
        var store = tokenStore ?? new MockTokenStore();

        var service = new SpotifyService(
            options,
            httpContextAccessor,
            cache,
            requestCache,
            sseService,
            queueService,
            freeLyrics,
            NullLogger<SpotifyService>.Instance,
            store
        );

        return (service, session, store);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenSessionHasValidToken_ReturnsTrue()
    {
        var (service, session, _) = CreateService();
        session.SetString("SpotifyAccessToken", "valid-access-token");
        session.SetString("SpotifyTokenExpiry", DateTime.UtcNow.AddHours(1).ToString("o"));

        var result = await service.EnsureAuthenticatedAsync();

        Assert.True(result);
        Assert.True(service.IsAuthenticated());
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenSessionEmpty_RestoresFromTokenStore()
    {
        var store = new MockTokenStore
        {
            StoredData = new SpotifyAuthData
            {
                AccessToken = "stored-token-123",
                RefreshToken = "stored-refresh-456",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
                UserId = "user-abc"
            }
        };

        var (service, session, _) = CreateService(store);

        // Verify session is initially empty
        Assert.Null(session.GetString("SpotifyAccessToken"));

        var result = await service.EnsureAuthenticatedAsync();

        Assert.True(result);
        Assert.Equal("stored-token-123", session.GetString("SpotifyAccessToken"));
        Assert.Equal("stored-refresh-456", session.GetString("SpotifyRefreshToken"));
        Assert.Equal("user-abc", session.GetString("SpotifyUserId"));
        Assert.True(service.IsAuthenticated());
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenNoTokensAnywhere_ReturnsFalse()
    {
        var (service, _, _) = CreateService();

        var result = await service.EnsureAuthenticatedAsync();

        Assert.False(result);
        Assert.False(service.IsAuthenticated());
    }

    [Fact]
    public void ClearAuthentication_RemovesSessionAndClearsTokenStore()
    {
        var store = new MockTokenStore
        {
            StoredData = new SpotifyAuthData
            {
                AccessToken = "token",
                RefreshToken = "refresh",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            }
        };

        var (service, session, _) = CreateService(store);
        session.SetString("SpotifyAccessToken", "token");

        service.ClearAuthentication();

        Assert.Null(session.GetString("SpotifyAccessToken"));
        Assert.Null(store.StoredData);
        Assert.False(service.IsAuthenticated());
    }

    [Fact]
    public void StartAuth_WithByokClientId_UsesAndStoresByokClientId()
    {
        var (service, session, _) = CreateService();
        var byokId = "my-custom-byok-client-id-12345";

        var loginUrl = service.StartAuth(byokId);

        Assert.Contains($"client_id={byokId}", loginUrl);
        Assert.Equal(byokId, session.GetString("SpotifyClientId"));
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_RestoresByokClientIdFromTokenStore()
    {
        var byokId = "byok-restored-id-999";
        var store = new MockTokenStore
        {
            StoredData = new SpotifyAuthData
            {
                ClientId = byokId,
                AccessToken = "valid-byok-access-token",
                RefreshToken = "valid-byok-refresh-token",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
            }
        };

        var (service, session, _) = CreateService(store);
        var result = await service.EnsureAuthenticatedAsync();

        Assert.True(result);
        Assert.Equal(byokId, session.GetString("SpotifyClientId"));
    }
}
