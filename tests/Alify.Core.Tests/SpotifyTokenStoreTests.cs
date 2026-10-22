using Alify.Core.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Alify.Core.Tests;

public class SpotifyTokenStoreTests : IDisposable
{
    private readonly string _tempFilePath;
    private readonly EphemeralDataProtectionProvider _protectionProvider;
    private readonly ProtectedFileSpotifyTokenStore _store;

    public SpotifyTokenStoreTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"alify_token_test_{Guid.NewGuid():N}.dat");
        _protectionProvider = new EphemeralDataProtectionProvider();
        _store = new ProtectedFileSpotifyTokenStore(_protectionProvider, _tempFilePath);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task GetTokensAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        var result = await _store.GetTokensAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveTokensAsync_SavesEncryptedData_AndCanBeReadBack()
    {
        var authData = new SpotifyAuthData
        {
            AccessToken = "test-access-token-12345",
            RefreshToken = "test-refresh-token-67890",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            UserId = "user-test-id",
            Scope = "user-read-playback-state user-modify-playback-state"
        };

        await _store.SaveTokensAsync(authData);

        Assert.True(File.Exists(_tempFilePath));
        var rawContent = await File.ReadAllTextAsync(_tempFilePath);
        // Assert raw content is protected and does not contain plaintext access token
        Assert.DoesNotContain("test-access-token-12345", rawContent);

        var retrieved = await _store.GetTokensAsync();
        Assert.NotNull(retrieved);
        Assert.Equal("test-access-token-12345", retrieved.AccessToken);
        Assert.Equal("test-refresh-token-67890", retrieved.RefreshToken);
        Assert.Equal("user-test-id", retrieved.UserId);
        Assert.Equal("user-read-playback-state user-modify-playback-state", retrieved.Scope);
        Assert.False(retrieved.IsExpired);
    }

    [Fact]
    public async Task ClearTokensAsync_RemovesFileAndReturnsNull()
    {
        var authData = new SpotifyAuthData
        {
            AccessToken = "sample-token",
            RefreshToken = "sample-refresh",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };

        await _store.SaveTokensAsync(authData);
        Assert.True(File.Exists(_tempFilePath));

        await _store.ClearTokensAsync();

        Assert.False(File.Exists(_tempFilePath));
        var retrieved = await _store.GetTokensAsync();
        Assert.Null(retrieved);
    }

    [Fact]
    public void IsExpired_CorrectlyIdentifiesExpiredTokens()
    {
        var expiredData = new SpotifyAuthData
        {
            AccessToken = "expired",
            RefreshToken = "refresh",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };

        var validData = new SpotifyAuthData
        {
            AccessToken = "valid",
            RefreshToken = "refresh",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };

        Assert.True(expiredData.IsExpired);
        Assert.False(validData.IsExpired);
    }
}
