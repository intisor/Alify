using System.Diagnostics;
using System.Linq;
using SpotifyAPI.Web;
using Alify.Core.Models;

namespace Alify.Core.Models
{
    [DebuggerDisplay("Id: {Id}, Name: {Name}, TrackCount: {TrackCount}, ActualTracks: {Tracks.Count}")]
    public class Playlist
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int TrackCount { get; set; }
        public List<Track> Tracks { get; set; } = new();
    }

    /// <summary>
    /// Represents a track with its lyrics and moderation status.
    /// </summary>
    [DebuggerDisplay("Track: {FullTrack?.Artists?.FirstOrDefault()?.Name} - {FullTrack?.Name}, IsFlagged: {IsFlagged}, HasLyrics: {!string.IsNullOrEmpty(Lyrics)}")]
    public class Track
    {
        public FullTrack? FullTrack { get; set; }
        public string? Lyrics { get; set; }
        public LyricMapping? MappedLyrics { get; set; }
        public bool IsFlagged { get; set; }
        public string? FlagReason { get; set; }
    }

    /// <summary>
    /// Exact action states for playback mutations (DATA-007 / PRD-003).
    /// </summary>
    public enum PlaybackActionStatus
    {
        NoAction,
        Requested,
        Succeeded,
        Failed,
        OutcomeUnknown,
        CancelledStale
    }

    /// <summary>
    /// Explicit result describing a skip action and its target track (ALI-011 / AUD-003 / DATA-007).
    /// Prevents post-dequeue attribution errors where the new queue head is confused with the skipped target.
    /// </summary>
    [DebuggerDisplay("Status: {Status}, Skipped: {Skipped}, Target: {TargetTrack?.FullTrack?.Name}, Next: {NextTrack?.FullTrack?.Name}")]
    public class SkipActionResult
    {
        public bool Skipped { get; set; }
        public PlaybackActionStatus Status { get; set; } = PlaybackActionStatus.NoAction;
        public Track? TargetTrack { get; set; }
        public Track? NextTrack { get; set; }
        public string? Reason { get; set; }

        public static SkipActionResult NoAction(Track? currentTrack = null, string reason = "No flagged track to skip") =>
            new() { Skipped = false, Status = PlaybackActionStatus.NoAction, TargetTrack = currentTrack, Reason = reason };

        public static SkipActionResult Success(Track targetTrack, Track? nextTrack, string reason = "Track flagged") =>
            new() { Skipped = true, Status = PlaybackActionStatus.Succeeded, TargetTrack = targetTrack, NextTrack = nextTrack, Reason = reason };

        public static SkipActionResult Failed(Track? targetTrack, string reason) =>
            new() { Skipped = false, Status = PlaybackActionStatus.Failed, TargetTrack = targetTrack, Reason = reason };

        public static SkipActionResult OutcomeUnknown(Track? targetTrack, string reason) =>
            new() { Skipped = false, Status = PlaybackActionStatus.OutcomeUnknown, TargetTrack = targetTrack, Reason = reason };

        public static SkipActionResult CancelledStale(Track? targetTrack, string reason) =>
            new() { Skipped = false, Status = PlaybackActionStatus.CancelledStale, TargetTrack = targetTrack, Reason = reason };
    }

    /// <summary>
    /// Represents the current Spotify playback information.
    /// </summary>
    [DebuggerDisplay("CurrentlyPlaying: {CurrentlyPlaying?.FullTrack?.Name}, QueueCount: {Queue.Count}, RemainingTime: {RemainingTimeMs}ms")]
    public class SpotifyPlaybackInfo
    {
        public Track? CurrentlyPlaying { get; set; }
        public List<Track> Queue { get; set; } = new();
        public int? RemainingTimeMs { get; set; }
    }

    // Queue Cache 
    public class MusicQueue
    {
        public List<Track> Tracks { get; set; } = new();
        public int CurrentIndex { get; set; } = 0;
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public Track? CurrentTrack => Tracks.Count > 0 && CurrentIndex >= 0 && CurrentIndex < Tracks.Count ? Tracks[CurrentIndex] : null;
        public bool IsEmpty => Tracks.Count == 0;
        public bool IsExpired => DateTime.UtcNow - CachedAt > TimeSpan.FromMinutes(10);

        // FIFO queue methods
        public void EnqueueTrack(Track track)
        {
            ArgumentNullException.ThrowIfNull(track);
            Tracks.Add(track);
        }

        public Track? DequeueTrack()
        {
            if (IsEmpty) return null;
            var track = Tracks[0];
            Tracks.RemoveAt(0);

            if (CurrentIndex > 0) CurrentIndex--;

            return track;
        }

        public void Clear()
        {
            Tracks.Clear();
            CurrentIndex = 0;
        }

        public bool MatchesCurrentPlayback(string currentTrackId)
        {
            return !IsEmpty && CurrentTrack?.FullTrack?.Id == currentTrackId;
        }
    }
}