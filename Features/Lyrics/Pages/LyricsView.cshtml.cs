using Alify.Core.Models;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace Alify.Pages
{
    public class LyricsViewModel : PageModel
    {
        private readonly SpotifyService _spotifyService;
        private readonly LyricService _lyricService;
        private readonly ArtistLyricService _artistLyricService;

        public LyricsViewModel(SpotifyService spotifyService, LyricService lyricService, ArtistLyricService artistLyricService)
        {
            _spotifyService = spotifyService;
            _lyricService = lyricService;
            _artistLyricService = artistLyricService;
        }

        public LyricMapping LyricMapping { get; set; }
        public string MainArtist { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var spotifyClient = await _spotifyService.GetSpotifyClientAsync();
            if (spotifyClient != null)
            {
                var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotifyClient);
                if (playbackInfo?.CurrentlyPlaying?.FullTrack != null)
                {
                    var track = playbackInfo.CurrentlyPlaying.FullTrack;
                    var lyrics = await _lyricService.GetLyricsAsync(track.Artists[0].Name, track.Name);
                    if (!string.IsNullOrEmpty(lyrics))
                    {
                        LyricMapping = _artistLyricService.ParseLyricsWithArtistMapping(lyrics);
                        MainArtist = track.Artists[0].Name;
                    }
                }
            }
            return Page();
        }
    }
}
