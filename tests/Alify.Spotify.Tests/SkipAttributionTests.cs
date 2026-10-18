using Alify.Core.Models;
using SpotifyAPI.Web;
using Xunit;

namespace Alify.Spotify.Tests;

public class SkipAttributionTests
{
    [Fact]
    public void SkipActionResult_Success_PreservesOriginalTargetTrack()
    {
        // Simulate Track 1 (Explicit / Flagged)
        var targetTrack = new Track
        {
            FullTrack = new FullTrack { Id = "track-1", Name = "Explicit Song", Explicit = true },
            IsFlagged = true
        };

        // Simulate Track 2 (Clean)
        var nextTrack = new Track
        {
            FullTrack = new FullTrack { Id = "track-2", Name = "Clean Song", Explicit = false },
            IsFlagged = false
        };

        var result = SkipActionResult.Success(targetTrack, nextTrack, "Explicit content detected");

        Assert.True(result.Skipped);
        Assert.NotNull(result.TargetTrack);
        Assert.Equal("track-1", result.TargetTrack.FullTrack?.Id);
        Assert.Equal("Explicit Song", result.TargetTrack.FullTrack?.Name);
        Assert.True(result.TargetTrack.IsFlagged);

        Assert.NotNull(result.NextTrack);
        Assert.Equal("track-2", result.NextTrack.FullTrack?.Id);
        Assert.False(result.NextTrack.IsFlagged);
    }

    [Fact]
    public void SkipActionResult_NoAction_IndicatesNotSkipped()
    {
        var currentTrack = new Track
        {
            FullTrack = new FullTrack { Id = "clean-1", Name = "Safe Song", Explicit = false },
            IsFlagged = false
        };

        var result = SkipActionResult.NoAction(currentTrack, "Track is clean");

        Assert.False(result.Skipped);
        Assert.Equal("clean-1", result.TargetTrack?.FullTrack?.Id);
        Assert.Null(result.NextTrack);
        Assert.Equal("Track is clean", result.Reason);
    }

    [Fact]
    public void MusicQueue_DequeueTrack_AdvancesCurrentTrackToNext()
    {
        var track1 = new Track { FullTrack = new FullTrack { Id = "t1", Name = "Song 1" } };
        var track2 = new Track { FullTrack = new FullTrack { Id = "t2", Name = "Song 2" } };

        var queue = new MusicQueue
        {
            Tracks = [track1, track2],
            CurrentIndex = 0
        };

        Assert.Equal("t1", queue.CurrentTrack?.FullTrack?.Id);

        // Dequeue advances CurrentIndex
        queue.DequeueTrack();

        // Target track was t1, but queue.CurrentTrack is now t2!
        // This validates AUD-003: returning queue.CurrentTrack after DequeueTrack gives t2, not t1.
        Assert.Equal("t2", queue.CurrentTrack?.FullTrack?.Id);
    }
}
