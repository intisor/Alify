using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyAPI.Web;
using Alify.Services;
using Alify.Models;
using Microsoft.Extensions.Caching.Memory;
using Alify.Extensions;

namespace Alify.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly SpotifyService _spotifyService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DashboardModel> _logger;

        public DashboardModel(
            SpotifyService spotifyService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<DashboardModel> logger)
        {
            _spotifyService = spotifyService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string Lyric { get; set; } = string.Empty;
        public string AlbumArtUrl { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public SpotifyPlaybackInfo PlaybackInfo { get; set; } = new SpotifyPlaybackInfo();
        public bool IsMonitoring { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Dashboard accessed without authentication");
                return RedirectToPage("/Index");
            }

            try
            {
                var spotifyConfig = this.ResolveService<SpotifyClientConfig>();
                var spotify = new SpotifyClient(spotifyConfig.WithToken(accessToken));

                await this.WithServiceAsync<IMemoryCache>(async cache =>
                {
                    await this.WithServiceAsync<SpotifyRequestCache>(async requestCache =>
                    {
                        var currentlyPlayingResponse = await requestCache.GetCurrentlyPlayingAsync(spotify);
                        var currentTrackId = (currentlyPlayingResponse?.Item as FullTrack)?.Id;
                        var lastTrackId = cache.Get<string>("LastTrackId");

                        if (currentTrackId != null && currentTrackId != lastTrackId)
                        {
                            PlaybackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                            cache.Set("LastTrackId", currentTrackId, TimeSpan.FromMinutes(10));
                            _logger.LogInformation("Track changed, refreshed playback info: {TrackId}", currentTrackId);
                        }
                        else
                        {
                            PlaybackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                        }
                    });
                });

                if (PlaybackInfo?.CurrentlyPlaying?.FullTrack != null)
                {
                    var track = PlaybackInfo.CurrentlyPlaying;
                    TrackName = track.FullTrack.Name;
                    ArtistName = track.FullTrack.Artists.FirstOrDefault()?.Name ?? "Unknown Artist";
                    AlbumArtUrl = track.FullTrack.Album.Images.FirstOrDefault()?.Url ?? string.Empty;
                    Lyric = track.Lyrics ?? string.Empty;
                }

                IsMonitoring = this.ResolveService<SpotifyQueueMonitorService>().IsMonitoring;
                StatusMessage = IsMonitoring ? "Monitoring active" : "Monitoring stopped";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                StatusMessage = "Error loading dashboard data. Please try again.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostStartMonitorAsync()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                StatusMessage = "Authentication required. Please login to Spotify first.";
                _logger.LogWarning("Start monitor attempted without authentication");
                return Page();
            }

            try
            {
                await this.WithServiceAsync<SpotifyQueueMonitorService>(async monitorService =>
                {
                    monitorService.StartMonitoring();
                    StatusMessage = "Queue monitoring started successfully.";
                    HighPerformanceLogging.LogMonitoringStarted(_logger);
                    await Task.CompletedTask;
                });
            }
            catch (Exception ex)
            {
                HighPerformanceLogging.LogMonitoringError(_logger, ex, "SpotifyQueueMonitorService");
                StatusMessage = "Error starting monitoring. Please try again.";
            }

            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostStopMonitorAsync()
        {
            try
            {
                var monitorService = this.ResolveService<SpotifyQueueMonitorService>();
                if (monitorService.IsMonitoring)
                {
                    monitorService.StopMonitoring();
                    StatusMessage = "Queue monitoring stopped successfully.";
                    HighPerformanceLogging.LogMonitoringStopped(_logger);
                }
                else
                {
                    StatusMessage = "Monitoring was already stopped.";
                    HighPerformanceLogging.LogMonitoringStopped(_logger);
                }
            }
            catch (Exception ex)
            {
                HighPerformanceLogging.LogMonitoringError(_logger, ex, "SpotifyQueueMonitorService");
                StatusMessage = "Error stopping monitoring. Please try again.";
            }

            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostRefreshDataAsync()
        {
            try
            {
                this.WithService<IMemoryCache>(cache =>
                {
                    var cacheKey = GetUserSpecificCacheKey("PlaybackInfo");
                    cache.Remove(cacheKey);
                    HighPerformanceLogging.LogMemoryCacheCleared(_logger);
                });

                this.WithService<SpotifyRequestCache>(requestCache =>
                {
                    requestCache.ClearCache();
                    HighPerformanceLogging.LogSpotifyCacheCleared(_logger);
                });

                StatusMessage = "Data refreshed successfully.";
            }
            catch (Exception ex)
            {
                HighPerformanceLogging.LogMonitoringError(_logger, ex, "RefreshData");
                StatusMessage = "Error refreshing data. Please try again.";
            }

            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAnalyzeLyricsAsync()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                StatusMessage = "Authentication required for lyrics analysis.";
                return Page();
            }

            try
            {
                var analysisResult = await this.WithServiceAsync<LyricService, string>(async lyricService =>
                {
                    var spotifyConfig = this.ResolveService<SpotifyClientConfig>();
                    var spotify = new SpotifyClient(spotifyConfig.WithToken(accessToken));
                    var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                    if (playbackInfo?.CurrentlyPlaying?.FullTrack == null)
                        return "No track currently playing for analysis.";

                    var track = playbackInfo.CurrentlyPlaying.FullTrack;
                    var artistName = track.Artists.FirstOrDefault()?.Name ?? "Unknown";
                    var lyrics = await lyricService.GetLyricsAsync(artistName, track.Name);
                    if (string.IsNullOrEmpty(lyrics))
                        return "No lyrics found for current track.";

                    return await this.WithServiceAsync<ArtistLyricService, string>(async artistLyricService =>
                    {
                        var mapping = artistLyricService.ParseLyricsWithArtistMapping(lyrics, artistName);
                        var artists = mapping.GetArtists();
                        var sections = mapping.GetSections();
                        HighPerformanceLogging.LogLyricsAnalysisCompleted(_logger, artists.Count, sections.Count);
                        await Task.CompletedTask;
                        return $"Analysis: {artists.Count} artists, {sections.Count} sections, {mapping.Lines.Count} lines";
                    });
                });

                StatusMessage = analysisResult;
            }
            catch (Exception ex)
            {
                HighPerformanceLogging.LogMonitoringError(_logger, ex, "AnalyzeLyrics");
                StatusMessage = "Error analyzing lyrics. Please try again.";
            }

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
