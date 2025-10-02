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

        public bool MatchesCurrentPlayback(string currentTrackId)
        {
            return !IsEmpty && CurrentTrack.FullTrack?.Id == currentTrackId;
        }
    }
}