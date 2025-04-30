using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyAPI.Web;
using Alify.Services;

namespace Alify.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly SpotifyClientConfig _spotifyClientConfig;
        private readonly LyricService _lyricService;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public string TrackName { get; set; }
        public string ArtistName { get; set; }
        public string Lyric { get; set; }
        public string AlbumArtUrl { get; set; }
        public string Message { get; set; }
        public DashboardModel(IHttpContextAccessor httpContextAccessor, SpotifyClientConfig spotifyClientConfig,LyricService lyricService)
        {
            _httpContextAccessor = httpContextAccessor;
            _spotifyClientConfig = spotifyClientConfig;
            _lyricService = lyricService;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToPage("/Index");
            }
            var spotify = new SpotifyClient(_spotifyClientConfig.WithToken(accessToken));

            var playback = await spotify.Player.GetCurrentPlayback();
            var currentPlaying = await spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());

            if (currentPlaying != null && currentPlaying.Item is FullTrack track)
            {
                TrackName = track.Name;
                ArtistName = track.Artists.FirstOrDefault()?.Name;
                AlbumArtUrl = track.Album.Images.FirstOrDefault()?.Url;
                Lyric = await _lyricService.GetLyricsAsync(ArtistName,TrackName);
                var moderation = await _lyricService.ModerateLyricsAsync(Lyric);

                if (moderation?.suitable_for_kids == false)
                {
                    Message = "Song contains harmful content. Consider skipping.";
                }
            }
            return Page();
        }
    }
}
