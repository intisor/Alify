using Alify.Core.Models;
using SpotifyAPI.Web;
using Xunit;

namespace Alify.Spotify.Tests;

public class PlaybackFaultMatrixTests
{
    [Fact]
    public void SkipActionResult_Failed_PreservesTargetTrackAndReason()
    {
        var targetTrack = new Track
        {
            FullTrack = new FullTrack { Id = "tr-1", Name = "Explicit Track", Explicit = true },
            IsFlagged = true
        };

        var result = SkipActionResult.Failed(targetTrack, "Spotify API error 403: Forbidden");

        Assert.False(result.Skipped);
        Assert.Equal(PlaybackActionStatus.Failed, result.Status);
        Assert.Equal("tr-1", result.TargetTrack?.FullTrack?.Id);
        Assert.Null(result.NextTrack);
        Assert.Contains("403", result.Reason);
    }

    [Fact]
    public void SkipActionResult_OutcomeUnknown_PreservesTargetAndRecordsTimeout()
    {
        var targetTrack = new Track
        {
            FullTrack = new FullTrack { Id = "tr-2", Name = "Explicit Track 2", Explicit = true },
            IsFlagged = true
        };

        var result = SkipActionResult.OutcomeUnknown(targetTrack, "Network timeout or response lost");

        Assert.False(result.Skipped);
        Assert.Equal(PlaybackActionStatus.OutcomeUnknown, result.Status);
        Assert.Equal("tr-2", result.TargetTrack?.FullTrack?.Id);
        Assert.Contains("timeout", result.Reason);
    }

    [Fact]
    public void SkipActionResult_CancelledStale_PreservesTargetAndReason()
    {
        var targetTrack = new Track
        {
            FullTrack = new FullTrack { Id = "tr-3", Name = "Explicit Track 3", Explicit = true },
            IsFlagged = true
        };

        var result = SkipActionResult.CancelledStale(targetTrack, "Track changed before preflight dispatch");

        Assert.False(result.Skipped);
        Assert.Equal(PlaybackActionStatus.CancelledStale, result.Status);
        Assert.Equal("tr-3", result.TargetTrack?.FullTrack?.Id);
        Assert.Contains("changed", result.Reason);
    }

    [Fact]
    public void MusicQueue_ConsecutiveExplicitItems_DequeuesSafelyWithoutIndexException()
    {
        var t1 = new Track { FullTrack = new FullTrack { Id = "1", Name = "Explicit 1", Explicit = true }, IsFlagged = true };
        var t2 = new Track { FullTrack = new FullTrack { Id = "2", Name = "Explicit 2", Explicit = true }, IsFlagged = true };
        var t3 = new Track { FullTrack = new FullTrack { Id = "3", Name = "Clean 3", Explicit = false }, IsFlagged = false };

        var queue = new MusicQueue
        {
            Tracks = [t1, t2, t3],
            CurrentIndex = 0
        };

        Assert.Equal("1", queue.CurrentTrack?.FullTrack?.Id);

        // First skip
        queue.DequeueTrack();
        Assert.Equal("2", queue.CurrentTrack?.FullTrack?.Id);

        // Second skip
        queue.DequeueTrack();
        Assert.Equal("3", queue.CurrentTrack?.FullTrack?.Id);

        // Third dequeue when at end
        queue.DequeueTrack();
        Assert.Null(queue.CurrentTrack);
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void MusicQueue_SyncWithEmptyQueue_HandlesGracefullyWithoutThrowing()
    {
        var queue = new MusicQueue { Tracks = [] };
        Assert.Null(queue.CurrentTrack);
        Assert.True(queue.IsEmpty);

        // Dequeue on empty queue does not throw
        queue.DequeueTrack();
        Assert.Null(queue.CurrentTrack);
    }
}
