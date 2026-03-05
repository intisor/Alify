using Alify.Core.Infrastructure.FeatureFlags;
using Alify.Core.Models;
using Alify.Core.Services;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.FeatureManagement.Mvc;
using System.Threading.Tasks;

namespace Alify.Pages
{
    // [FeatureGate] is from Microsoft.FeatureManagement.AspNetCore.
    // When EnableLyricsChat = false in config, ASP.NET Core returns 404 automatically —
    // no manual if-statements needed. The page simply doesn't exist to the outside world.
    [FeatureGate(FeatureFlags.EnableLyricsChat)]
    public class LyricsViewModel : PageModel
    {
        private readonly ArtistLyricService _artistLyricService;
        private readonly LyricService _lyricService;

        public LyricsViewModel(ArtistLyricService artistLyricService, LyricService lyricService)
        {
            _artistLyricService = artistLyricService;
            _lyricService = lyricService;
        }

        public LyricMapping? LyricMapping { get; set; }
        public string? MainArtist { get; set; }
        public string? TrackName { get; set; }

        public async Task<IActionResult> OnGetAsync(string artist = "The Weeknd", string track = "Blinding Lights")
        {
            // Simple demo search for lyrics project - no Spotify dependency
            var lyrics = await _lyricService.GetLyricsAsync(artist, track);
            if (!string.IsNullOrEmpty(lyrics))
            {
                LyricMapping = _artistLyricService.ParseLyricsWithArtistMapping(lyrics, artist);
                MainArtist = artist;
                TrackName = track;
            }

            return Page();
        }
    }
}