using Alify.Core.Models;
using Alify.Features.Spotify.Events;
using Alify.Features.Spotify.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SpotifyAPI.Web;
using Xunit;

namespace Alify.Spotify.Tests;

public class SseServiceTests
{
    private readonly SseService _sseService = new(NullLogger<SseService>.Instance);

    [Fact]
    public async Task SendEventAsync_WithoutConnectedClients_CompletesSuccessfully()
    {
        // Must not throw or block when 0 clients are connected
        await _sseService.SendEventAsync("testEvent", new { message = "hello" });
    }

    [Fact]
    public async Task NotifyTrackSkippedEventAsync_DispatchesToRegisteredObservers()
    {
        var testObserver = new MockSpotifyObserver();
        _sseService.AddObserver(testObserver);

        var track = new Track
        {
            FullTrack = new FullTrack { Id = "test-1", Name = "Test Track" },
            IsFlagged = true
        };

        await _sseService.NotifyTrackSkippedEventAsync(track);

        Assert.Single(testObserver.SkippedTracks);
        Assert.Equal("test-1", testObserver.SkippedTracks[0].FullTrack?.Id);
    }

    [Fact]
    public async Task RemoveObserver_PreventsSubsequentNotifications()
    {
        var testObserver = new MockSpotifyObserver();
        _sseService.AddObserver(testObserver);
        _sseService.RemoveObserver(testObserver);

        var track = new Track
        {
            FullTrack = new FullTrack { Id = "test-2", Name = "Ignored Track" }
        };

        await _sseService.NotifyTrackSkippedEventAsync(track);

        Assert.Empty(testObserver.SkippedTracks);
    }

    [Fact]
    public void Cleanup_ClearsAllStateWithoutExceptions()
    {
        _sseService.Cleanup();
    }

    private sealed class MockSpotifyObserver : ISPotifyObserver
    {
        public List<Track> SkippedTracks { get; } = [];
        public List<SpotifyPlaybackInfo> PlaybackInfos { get; } = [];

        public Task SendTrackSkippedEventAsync(Track track)
        {
            SkippedTracks.Add(track);
            return Task.CompletedTask;
        }

        public Task SendPlaybackInfoAsync(SpotifyPlaybackInfo playbackInfo)
        {
            PlaybackInfos.Add(playbackInfo);
            return Task.CompletedTask;
        }
    }
}
