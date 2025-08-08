using Alify.Core.Models;
using Alify.Features.Spotify.Services;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace Alify.Pages
{
    public class LyricsViewModel : PageModel
    {
        private readonly QueueService _queueService;
        private readonly SpotifyService _spotifyService;

        public LyricsViewModel(QueueService queueService,SpotifyService spotifyService)
        {
            _queueService = queueService;
            _spotifyService = spotifyService;
        }

        public LyricMapping LyricMapping { get; set; }
        public string MainArtist { get; set; }
        public string TrackName { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var spotifyClient = await _spotifyService.GetSpotifyClientAsync();
            if (spotifyClient != null)
            {
                var userId = HttpContext.Session.Id; // Assuming user ID is stored in session
                var queue = await _queueService.GetQueueAsync(userId,spotifyClient);
                if (queue == null || queue.IsEmpty) return RedirectToPage("/Index");

                var track = queue.CurrentTrack;

                if (track?.MappedLyrics is not null)
                {
                    LyricMapping = track.MappedLyrics;
                    MainArtist = track.FullTrack?.Artists?.FirstOrDefault()?.Name;
                    TrackName = track.FullTrack?.Name;
                }
            }

            return Page();
        }
    }
}