using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyAPI.Web;
using Alify.Services;
using Alify.Models;
using Microsoft.Extensions.Caching.Memory;
using Alify.Extensions; // Add Method Injection support

namespace Alify.Pages
{
    /// <summary>
    /// Dashboard Page Model demonstrating hybrid dependency injection:
    /// - Constructor injection for core, always-needed dependencies
    /// - Method injection for optional, context-specific dependencies
    /// 
    /// Benefits of this approach:
    /// 1. Reduced constructor complexity (from 8 to 3 core dependencies)
    /// 2. Services resolved only when actually needed (memory efficient)
    /// 3. Better separation of concerns per action method
    /// 4. Improved testability - can mock specific services per test scenario
    /// </summary>
    public class DashboardModel : PageModel
    {
        // CORE DEPENDENCIES: Constructor injection for services needed in multiple methods
        // These are injected via constructor because they're used frequently
        private readonly SpotifyService _spotifyService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DashboardModel> _logger;

        /// <summary>
        /// Constructor now only takes core dependencies that are used across multiple methods.
        /// This reduces constructor bloat and makes the class more maintainable.
        /// </summary>
        public DashboardModel(
            SpotifyService spotifyService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<DashboardModel> logger)
        {
            _spotifyService = spotifyService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        // PUBLIC PROPERTIES: Data for the view
        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string Lyric { get; set; } = string.Empty;
        public string AlbumArtUrl { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public SpotifyPlaybackInfo PlaybackInfo { get; set; } = new SpotifyPlaybackInfo();
        public bool IsMonitoring { get; set; }

        /// <summary>
        /// Main GET handler using hybrid injection approach.
        /// Uses constructor-injected core services and method injection for cache operations.
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            // Check authentication using core service
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Dashboard accessed without authentication");
                return RedirectToPage("/Index");
            }

            try
            {
                // METHOD INJECTION: Resolve SpotifyClientConfig only when needed
                // Benefits: Not injected in constructor, only resolved when actually used
                var spotifyConfig = this.ResolveService<SpotifyClientConfig>();
                var spotify = new SpotifyClient(spotifyConfig.WithToken(accessToken));

                // METHOD INJECTION: Use cache services via method injection
                // Benefits: Cache operations are isolated, easier to test individual caching logic
                await this.WithServiceAsync<IMemoryCache>(async cache =>
                {
                    await this.WithServiceAsync<SpotifyRequestCache>(async requestCache =>
                    {
                        // Use request cache to get currently playing
                        var currentlyPlayingResponse = await requestCache.GetCurrentlyPlayingAsync(spotify);
                        var currentTrackId = (currentlyPlayingResponse?.Item as FullTrack)?.Id;
                        var lastTrackId = cache.Get<string>("LastTrackId");

                        // Only fetch new data if track changed (performance optimization)
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

                // Populate view properties
                if (PlaybackInfo?.CurrentlyPlaying?.FullTrack != null)
                {
                    var track = PlaybackInfo.CurrentlyPlaying;
                    TrackName = track.FullTrack.Name;
                    ArtistName = track.FullTrack.Artists.FirstOrDefault()?.Name ?? "Unknown Artist";
                    AlbumArtUrl = track.FullTrack.Album.Images.FirstOrDefault()?.Url ?? string.Empty;
                    Lyric = track.Lyrics ?? string.Empty;
                }

                // METHOD INJECTION: Get monitoring status only when displaying dashboard
                // Benefits: Service resolved on-demand, not held in memory unnecessarily
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

        /// <summary>
        /// Start monitoring handler using method injection for the monitor service.
        /// Benefits: Monitor service only resolved when actually starting monitoring.
        /// </summary>
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
                // METHOD INJECTION: Resolve monitor service only when starting monitoring
                // Benefits: Service lifecycle tied to the specific operation
                await this.WithServiceAsync<SpotifyQueueMonitorService>(async monitorService =>
                {
                    monitorService.StartMonitoring();
                    StatusMessage = "Queue monitoring started successfully.";
                    _logger.LogInformation("Queue monitoring started by user");
                    await Task.CompletedTask; // Placeholder for any async initialization
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting monitor service");
                StatusMessage = "Error starting monitoring. Please try again.";
            }

            await OnGetAsync();
            return Page();
        }

        /// <summary>
        /// Stop monitoring handler demonstrating conditional method injection.
        /// Benefits: Service only resolved if actually needed to stop monitoring.
        /// </summary>
        public async Task<IActionResult> OnPostStopMonitorAsync()
        {
            try
            {
                // METHOD INJECTION: Resolve service to check and stop monitoring
                var monitorService = this.ResolveService<SpotifyQueueMonitorService>();
                
                // Conditional operation: Only stop if currently monitoring
                if (monitorService.IsMonitoring)
                {
                    monitorService.StopMonitoring();
                    StatusMessage = "Queue monitoring stopped successfully.";
                    _logger.LogInformation("Queue monitoring stopped by user");
                }
                else
                {
                    StatusMessage = "Monitoring was already stopped.";
                    _logger.LogInformation("Stop monitor called but monitoring was already stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping monitor service");
                StatusMessage = "Error stopping monitoring. Please try again.";
            }

            await OnGetAsync();
            return Page();
        }

        /// <summary>
        /// Refresh data handler demonstrating multiple service method injection.
        /// Benefits: Cache services only resolved when actually clearing caches.
        /// </summary>
        public async Task<IActionResult> OnPostRefreshDataAsync()
        {
            try
            {
                // METHOD INJECTION: Resolve cache services only for cache operations
                // Benefits: Cache dependencies isolated to this specific operation
                this.WithService<IMemoryCache>(cache =>
                {
                    var cacheKey = GetUserSpecificCacheKey("PlaybackInfo");
                    cache.Remove(cacheKey);
                    _logger.LogInformation("Memory cache cleared for refresh");
                });

                // Chain method injection for multiple cache services
                this.WithService<SpotifyRequestCache>(requestCache =>
                {
                    requestCache.ClearCache();
                    _logger.LogInformation("Spotify request cache cleared for refresh");
                });

                StatusMessage = "Data refreshed successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing data");
                StatusMessage = "Error refreshing data. Please try again.";
            }

            await OnGetAsync();
            return Page();
        }

        /// <summary>
        /// New method demonstrating advanced method injection with lyrics processing.
        /// Benefits: LyricService and ArtistLyricService only resolved when processing lyrics.
        /// </summary>
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
                // METHOD INJECTION: Chain multiple services for complex operation
                // Benefits: Services resolved only for this specific lyrics analysis operation
                var analysisResult = await this.WithServiceAsync<LyricService, string>(async lyricService =>
                {
                    var spotifyConfig = this.ResolveService<SpotifyClientConfig>();
                    var spotify = new SpotifyClient(spotifyConfig.WithToken(accessToken));
                    
                    var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                    if (playbackInfo?.CurrentlyPlaying?.FullTrack == null)
                        return "No track currently playing for analysis.";

                    var track = playbackInfo.CurrentlyPlaying.FullTrack;
                    var artistName = track.Artists.FirstOrDefault()?.Name ?? "Unknown";

                    // Get lyrics using method-injected service
                    var lyrics = await lyricService.GetLyricsAsync(artistName, track.Name);
                    if (string.IsNullOrEmpty(lyrics))
                        return "No lyrics found for current track.";

                    // METHOD INJECTION: Resolve ArtistLyricService for parsing
                    return await this.WithServiceAsync<ArtistLyricService, string>(async artistLyricService =>
                    {
                        var mapping = artistLyricService.ParseLyricsWithArtistMapping(lyrics, artistName);
                        var artists = mapping.GetArtists();
                        var sections = mapping.GetSections();

                        _logger.LogInformation("Lyrics analyzed: {ArtistCount} artists, {SectionCount} sections", 
                                               artists.Count, sections.Count);

                        await Task.CompletedTask; // Placeholder for any async analysis
                        return $"Analysis: {artists.Count} artists, {sections.Count} sections, {mapping.Lines.Count} lines";
                    });
                });

                StatusMessage = analysisResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing lyrics");
                StatusMessage = "Error analyzing lyrics. Please try again.";
            }

            await OnGetAsync();
            return Page();
        }

        /// <summary>
        /// Helper method that doesn't need dependency injection.
        /// Kept as-is to show mixed approaches in the same class.
        /// </summary>
        private string GetUserSpecificCacheKey(string baseKey)
        {
            var userId = _httpContextAccessor.HttpContext?.Session.Id ?? "anonymous";
            return $"{baseKey}_{userId}";
        }
    }
}
