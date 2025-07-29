using Alify.Models;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace Alify.Pages
{
    /// <summary>
    /// The model for the Lyrics View page. This class is responsible for fetching the necessary data
    /// and preparing it for rendering in the Razor Page. It acts as a bridge between the services
    /// (which handle the business logic) and the view (which handles the presentation).
    /// </summary>
    public class LyricsViewModel : PageModel
    {
        // BOOKMARK: Service Dependencies
        // These services are injected into the page model via dependency injection.
        // This is a core concept in ASP.NET Core for creating modular and testable applications.
        private readonly SpotifyService _spotifyService; // For interacting with the Spotify API.
        private readonly LyricService _lyricService;     // For fetching lyric data.
        private readonly ArtistLyricService _artistLyricService; // For parsing and structuring lyrics.

        public LyricsViewModel(SpotifyService spotifyService, LyricService lyricService, ArtistLyricService artistLyricService)
        {
            _spotifyService = spotifyService;
            _lyricService = lyricService;
            _artistLyricService = artistLyricService;
        }

        // BOOKMARK: Page Model Properties
        // These properties hold the data that will be displayed on the Razor Page.
        // The view will bind to these properties to render the dynamic content.

        /// <summary>
        /// Holds the parsed and structured lyric data.
        /// </summary>
        public LyricMapping LyricMapping { get; set; }

        /// <summary>
        /// The primary artist of the currently playing song. This is used to determine
        /// which chat bubbles should be aligned to the right (the "main" user).
        /// </summary>
        public string MainArtist { get; set; }

        /// <summary>
        /// The handler for HTTP GET requests to this page. This is where the main logic for the page resides.
        /// It orchestrates the calls to the various services to build the data model for the view.
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            // Get the current playback state from Spotify.
            var spotifyClient = await _spotifyService.GetSpotifyClientAsync();
            if (spotifyClient != null)
            {
                var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotifyClient);
                if (playbackInfo?.CurrentlyPlaying?.FullTrack != null)
                {
                    var track = playbackInfo.CurrentlyPlaying.FullTrack;
                    
                    // If a track is playing, fetch its lyrics using the track name and primary artist.
                    var lyrics = await _lyricService.GetLyricsAsync(track.Artists[0].Name, track.Name);
                    if (!string.IsNullOrEmpty(lyrics))
                    {
                        // If lyrics are found, parse them to identify artists and sections.
                        LyricMapping = _artistLyricService.ParseLyricsWithArtistMapping(lyrics);
                        // Set the main artist for the view.
                        MainArtist = track.Artists[0].Name;
                    }
                }
            }

            if (LyricMapping == null)
            {
                // Handle the case where no song is playing or lyrics couldn't be found.
                // The view will display a "No lyrics available" message.
            }

            // Render the Razor Page with the prepared data.
            return Page();
        }
    }
}
