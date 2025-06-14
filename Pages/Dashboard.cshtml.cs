using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyAPI.Web;
using Alify.Services;
using Alify.Models;
using System.Net.Http;
using Alify.Controllers;

namespace Alify.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly SpotifyClientConfig _spotifyClientConfig;
        private readonly LyricService _lyricService;
        private readonly SpotifyService spotifyService;
        private readonly IHttpClientFactory _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly SpotifyController _spotifyController;

        public string TrackName { get; set; }
        public string ArtistName { get; set; }
        public string Lyric { get; set; }
        public string AlbumArtUrl { get; set; }
        public string StatusMessage { get; set; }
        public SpotifyPlaybackInfo PlaybackInfo { get; set; }
        public DashboardModel(IHttpContextAccessor httpContextAccessor, SpotifyClientConfig spotifyClientConfig,LyricService lyricService, IHttpClientFactory httpClient, SpotifyService spotifyService,SpotifyController spotifyController)
        {
            _httpContextAccessor = httpContextAccessor;
            _spotifyClientConfig = spotifyClientConfig;
            _lyricService = lyricService;
            _httpClient = httpClient;
            this.spotifyService = spotifyService;
            _spotifyController = spotifyController;
            PlaybackInfo = new SpotifyPlaybackInfo();
        }
        public async Task<IActionResult> OnGetAsync()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToPage("/Index");
            }
            var spotify = new SpotifyClient(_spotifyClientConfig.WithToken(accessToken));
            PlaybackInfo = await spotifyService.GetCurrentPlaybackAsync(spotify) ?? new SpotifyPlaybackInfo();
            var currentPlaying = await spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());

            if (currentPlaying != null && currentPlaying.Item is FullTrack track)
            {
                TrackName = track.Name;
                ArtistName = track.Artists.FirstOrDefault()?.Name;
                AlbumArtUrl = track.Album.Images.FirstOrDefault()?.Url;
                Lyric = await _lyricService.GetLyricsAsync(ArtistName,TrackName);
                
            }
            return Page();
        }
        public async Task<IActionResult> OnPostStartMonitorAsync()
        {
            var result = await _spotifyController.MonitorQueue();
            StatusMessage = result is OkObjectResult ok ? ok.Value.ToString() : "Failed to stop monitoring";
            await OnGetAsync(); // Refresh data
            return Page();
        }
        public async Task<IActionResult> OnPostStopMonitorAsync()
        {
            var result = await _spotifyController.MonitorQueue();
            StatusMessage = result is OkObjectResult ok ? ok.Value.ToString() : "Failed to stop monitoring";
            await OnGetAsync();
            return Page();
        }
    }
}
