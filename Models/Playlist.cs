using SpotifyAPI.Web;

namespace Alify.Models
{
    public class Playlist
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int TrackCount { get; set; }
        public List<Track> Tracks { get; set; } = [];
    }

    public class Track
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Artist { get; set; }
        public string Lyrics { get; set; } // Populated by Musixmatch
        public bool IsHarmful { get; set; } // Set by Azure Content Safety
    }
    public class SpotifyPlaybackInfo
    {
        public FullTrack CurrentlyPlaying { get; set; }
        public List<FullTrack> Queue { get; set; } = new();
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
}
