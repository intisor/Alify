using SpotifyAPI.Web;

namespace Alify.Models
{
    public class Playlist
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int TrackCount { get; set; }
        public List<Track> Tracks { get; set; } = new();
    }

    public class Track
    {
        public FullTrack FullTrack { get; set; }
        public string Lyrics { get; set; }
        public bool IsFlagged { get; set; }
    }

    public class SpotifyPlaybackInfo
    {
        public Track CurrentlyPlaying { get; set; }
        public List<Track> Queue { get; set; } = new();
        public int? RemainingTimeMs { get; set; }
    }

    public class LyricsModerationResult
    {
        public bool violence { get; set; }
        public bool hate { get; set; }
        public bool sexual { get; set; }
        public bool profanity { get; set; }
        public bool suitable_for_kids { get; set; }
        //public bool IsHarmful => violence || hate || sexual || profanity;
    }

    public class LyricLine
    {
        public int LineNumber { get; set; }
        public string Text { get; set; }
        public string Artist { get; set; }
        public string Section { get; set; } // e.g., "Verse 1", "Chorus", "Bridge"
        public bool IsAnnotation { get; set; }
    }

    public class LyricMapping
    {
        public List<LyricLine> Lines { get; set; } = new();
        public Dictionary<string, List<int>> ArtistToLineNumbers { get; set; } = new();
        public Dictionary<string, List<int>> SectionToLineNumbers { get; set; } = new();
    }
}
