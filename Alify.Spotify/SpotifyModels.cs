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
    /// Represents a playable item (track or episode) with its lyrics, moderation status, and episode metadata.
    /// </summary>
    [DebuggerDisplay("{DisplayArtist} - {DisplayName}, IsEpisode: {IsEpisode}, IsFlagged: {IsFlagged}")]
    public class Track
    {
        public FullTrack? FullTrack { get; set; }
        public FullEpisode? FullEpisode { get; set; }
        public string? Lyrics { get; set; }
        public LyricMapping? MappedLyrics { get; set; }
        public bool IsFlagged { get; set; }

        // --- Episode-specific properties ---

        /// <summary>Resume position in milliseconds (episodes only).</summary>
        public int? ResumePositionMs { get; set; }

        /// <summary>Whether the episode has been fully played (episodes only).</summary>
        public bool? FullyPlayed { get; set; }

        // --- Unified computed properties ---

        /// <summary>True if this item is a podcast episode rather than a music track.</summary>
        public bool IsEpisode => FullEpisode != null;

        /// <summary>Display name: track name or episode name.</summary>
        public string DisplayName => IsEpisode
            ? FullEpisode!.Name ?? "Unknown Episode"
            : FullTrack?.Name ?? "Unknown Track";

        /// <summary>Display artist: artist name or show name.</summary>
        public string DisplayArtist => IsEpisode
            ? FullEpisode!.Show?.Name ?? "Unknown Show"
            : FullTrack?.Artists?.FirstOrDefault()?.Name ?? "Unknown Artist";

        /// <summary>Best available image URL.</summary>
        public string? DisplayImageUrl => IsEpisode
            ? FullEpisode!.Images?.FirstOrDefault()?.Url
            : FullTrack?.Album?.Images?.FirstOrDefault()?.Url;

        /// <summary>Unified item ID (track ID or episode ID).</summary>
        public string? ItemId => IsEpisode ? FullEpisode?.Id : FullTrack?.Id;

        /// <summary>Unified duration in milliseconds.</summary>
        public int DurationMs => IsEpisode
            ? FullEpisode?.DurationMs ?? 0
            : FullTrack?.DurationMs ?? 0;

        /// <summary>Unified Spotify URI.</summary>
        public string? Uri => IsEpisode ? FullEpisode?.Uri : FullTrack?.Uri;

        /// <summary>Episode description (null for tracks).</summary>
        public string? EpisodeDescription => FullEpisode?.Description;

        /// <summary>Show name (null for tracks).</summary>
        public string? ShowName => FullEpisode?.Show?.Name;

        /// <summary>Show ID (null for tracks).</summary>
        public string? ShowId => FullEpisode?.Show?.Id;
    }

    /// <summary>
    /// Represents the current Spotify playback information.
    /// </summary>
    [DebuggerDisplay("CurrentlyPlaying: {CurrentlyPlaying?.DisplayName}, QueueCount: {Queue.Count}, RemainingTime: {RemainingTimeMs}ms")]
    public class SpotifyPlaybackInfo
    {
        public Track? CurrentlyPlaying { get; set; }
        public List<Track> Queue { get; set; } = new();
        public int? RemainingTimeMs { get; set; }
        public SleepTimerInfo? SleepTimer { get; set; }
    }

    /// <summary>
    /// Information about the active sleep timer.
    /// </summary>
    public class SleepTimerInfo
    {
        public bool IsActive { get; set; }
        public int RemainingSeconds { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Mode { get; set; } // "duration", "end_of_track", "end_of_episode"
    }

    // Queue Cache 
    public class MusicQueue
    {
        public List<Track> Tracks { get; set; } = new();
        public int CurrentIndex { get; set; } = 0;
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public Track CurrentTrack => CurrentIndex < Tracks.Count ? Tracks[CurrentIndex] : Tracks[0];
        public bool IsEmpty => Tracks.Count == 0;
        public bool IsExpired => DateTime.UtcNow - CachedAt > TimeSpan.FromMinutes(10);

        // FIFO queue methods
        public void EnqueueTrack(Track track)
        {
            ArgumentNullException.ThrowIfNull(track);
            Tracks.Add(track);
        }

        public Track DequeueTrack()
        {
            if (IsEmpty) throw new InvalidOperationException("Queue is empty.");
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

        /// <summary>
        /// Checks if the queue matches the current playback by comparing item IDs (works for both tracks and episodes).
        /// </summary>
        public bool MatchesCurrentPlayback(string currentItemId)
        {
            return !IsEmpty && CurrentTrack.ItemId == currentItemId;
        }
    }
}