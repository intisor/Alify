using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyAPI.Web;
using Alify.Services;
using Alify.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Alify.Pages
{
    public class DashboardModel(
        IHttpContextAccessor httpContextAccessor,
        SpotifyClientConfig spotifyClientConfig,
        LyricService lyricService,
        IHttpClientFactory httpClient,
        SpotifyService spotifyService,
        SpotifyQueueMonitorService monitorService,
        IMemoryCache cache,
        SpotifyRequestCache requestCache) : PageModel
    {
        private readonly SpotifyClientConfig _spotifyClientConfig = spotifyClientConfig;
        private readonly LyricService _lyricService = lyricService;
        private readonly SpotifyService spotifyService = spotifyService;
        private readonly IHttpClientFactory _httpClient = httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly SpotifyQueueMonitorService _monitorService = monitorService;
        private readonly IMemoryCache _cache = cache;
        private readonly SpotifyRequestCache _requestCache = requestCache;

        public string TrackName { get; set; }
        public string ArtistName { get; set; }
        public string Lyric { get; set; }
        public string AlbumArtUrl { get; set; }
        public string StatusMessage { get; set; }
        public SpotifyPlaybackInfo PlaybackInfo { get; set; } = new SpotifyPlaybackInfo();
        public bool IsMonitoring { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToPage("/Index");
            }

            var spotify = new SpotifyClient(_spotifyClientConfig.WithToken(accessToken));

            // Use request cache to get currently playing - this will only make one API call per request
            var currentlyPlayingResponse = await _requestCache.GetCurrentlyPlayingAsync(spotify);
            var currentTrackId = (currentlyPlayingResponse?.Item as FullTrack)?.Id;
            var lastTrackId = _cache.Get<string>("LastTrackId");

            if (currentTrackId != null && currentTrackId != lastTrackId)
            {
                PlaybackInfo = await spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                _cache.Set("LastTrackId", currentTrackId, TimeSpan.FromMinutes(10));
            }
            else
            {
                PlaybackInfo = await spotifyService.GetCurrentPlaybackInfoAsync(spotify);
            }

            if (PlaybackInfo?.CurrentlyPlaying?.FullTrack != null)
            {
                var track = PlaybackInfo.CurrentlyPlaying;
                TrackName = track.FullTrack.Name;
                ArtistName = track.FullTrack.Artists.FirstOrDefault()?.Name;
                AlbumArtUrl = track.FullTrack.Album.Images.FirstOrDefault()?.Url;
                Lyric = track.Lyrics;
            }

            // Update monitoring status
            IsMonitoring = _monitorService.IsMonitoring;
            StatusMessage = IsMonitoring ? "Monitoring active" : "Monitoring stopped";

            return Page();
        }

        public async Task<IActionResult> OnPostStartMonitorAsync()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                StatusMessage = "Authentication required. Please login to Spotify first.";
                return Page();
            }

            _monitorService.StartMonitoring();
            StatusMessage = "Queue monitoring started successfully.";
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostStopMonitorAsync()
        {
            _monitorService.StopMonitoring();
            StatusMessage = "Queue monitoring stopped successfully.";
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostRefreshDataAsync()
        {
            // Clear caches to force fresh data
            var cacheKey = GetUserSpecificCacheKey("PlaybackInfo");
            _cache.Remove(cacheKey);
            _requestCache.ClearCache();
            
            StatusMessage = "Data refreshed successfully.";
            await OnGetAsync();
            return Page();
        }

        private string GetUserSpecificCacheKey(string baseKey)
        {
            var userId = _httpContextAccessor.HttpContext?.Session.Id ?? "anonymous";
            return $"{baseKey}_{userId}";
        }
    }
}
