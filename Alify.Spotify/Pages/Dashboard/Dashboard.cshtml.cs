#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyAPI.Web;
using Microsoft.Extensions.Caching.Memory;
using Alify.Core.Models;
using Alify.Core.Infrastructure.Logging;
using Alify.Core.Services;
using Alify.Services;
using Alify.Features.Spotify.Services;
using System.Threading.Tasks;
using System.Linq;
using Alify.Core.Extensions;
using Alify.Core.Features.Analysis.Policy;

namespace Alify.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly SpotifyService _spotifyService;
        private readonly QueueService _queueService;    
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DashboardModel> _logger;

        public DashboardModel(
            SpotifyService spotifyService,
            QueueService queueService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<DashboardModel> logger)
        {
            _spotifyService = spotifyService;
            _queueService = queueService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string Lyric { get; set; } = string.Empty;
        public MusicQueue? Queue { get; private set; }
        public string AlbumArtUrl { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public SpotifyPlaybackInfo PlaybackInfo { get; set; } = new SpotifyPlaybackInfo();
        public PlaybackDashboardStats Stats { get; set; } = new PlaybackDashboardStats();
        public bool IsMonitoring { get; set; }
        public PrivateUser? CurrentUser { get; set; }

        [BindProperty]
        public UserFilterPreferences FilterPreferences { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _spotifyService.EnsureAuthenticatedAsync())
            {
                _logger.LogWarning("Dashboard accessed without authentication");
                return RedirectToPage("/Index");
            }

            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Dashboard accessed without authentication access token.");
                return RedirectToPage("/Index");
            }

            try
            {
                CurrentUser = await _spotifyService.CurrentUserAsync();
                string? userId = CurrentUser?.Id ?? _httpContextAccessor.HttpContext?.Session.GetString("SpotifyUserId");
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Dashboard accessed without user ID.");
                    StatusMessage = "Could not load Spotify profile. Please click Refresh Queue.";
                    return Page();
                }

                FilterPreferences = _queueService.GetUserFilterPreferences(userId);

                var spotifyConfig = this.ResolveService<SpotifyClientConfig>();
                var spotify = new SpotifyClient(spotifyConfig.WithToken(accessToken));

                var currentlyPlayingResponse = await this.WithServiceAsync<SpotifyRequestCache, CurrentlyPlaying?>(
                    async requestCache => await requestCache.GetCurrentlyPlayingAsync(spotify));
                var currentTrack = currentlyPlayingResponse?.Item as FullTrack;
                var currentTrackId = currentTrack?.Id;

                await this.WithServiceAsync<IMemoryCache>(async cache =>
                {
                    var lastTrackId = cache.Get<string>("LastTrackId");
                    if (currentTrackId != null && currentTrackId != lastTrackId)
                    {
                        cache.Set("LastTrackId", currentTrackId, TimeSpan.FromMinutes(10));
                        _queueService.InvalidateQueue(userId);
                        _logger.LogInformation("Track changed, refreshed playback info: {TrackId}", currentTrackId);
                    }
                    await Task.CompletedTask;
                });

                Queue = await _queueService.GetQueueAsync(userId, spotify, currentTrack);
                if (Queue != null)
                {
                    _logger.LogInformation("Queue for user {UserId}: {@QueueTracks}", userId, Queue.Tracks.Select(t => new { Name = t.FullTrack?.Name ?? "Unknown", Id = t.FullTrack?.Id ?? "Unknown" }));
                    if (Queue.IsEmpty)
                    {
                        StatusMessage = "Your queue is empty.";
                        _logger.LogInformation("Queue is empty for user {UserId}", userId);
                    }
                    else
                    {
                        PlaybackInfo = new SpotifyPlaybackInfo
                        {
                            CurrentlyPlaying = Queue.CurrentTrack,
                            Queue = [.. Queue.Tracks.Skip(Queue.CurrentIndex + 1)],
                            RemainingTimeMs = (currentlyPlayingResponse?.ProgressMs is not null && Queue.CurrentTrack?.FullTrack?.DurationMs > 0)
                                ? (Queue.CurrentTrack.FullTrack.DurationMs - currentlyPlayingResponse.ProgressMs)
                                : null
                        }; 
                    }
                }
                if (PlaybackInfo?.CurrentlyPlaying?.FullTrack != null)
                {
                    Track track = PlaybackInfo.CurrentlyPlaying;
                    TrackName = track.FullTrack.Name;
                    ArtistName = track.FullTrack.Artists.FirstOrDefault()?.Name ?? "Unknown Artist";
                    AlbumArtUrl = track.FullTrack.Album.Images.FirstOrDefault()?.Url ?? string.Empty;
                    Lyric = track.Lyrics ?? string.Empty;
                }

                IsMonitoring = this.ResolveService<SpotifyPlaybackMonitorService>().IsMonitoring;
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
            if (!await _spotifyService.EnsureAuthenticatedAsync())
            {
                StatusMessage = "Authentication required. Please login to Spotify first.";
                _logger.LogWarning("Start monitor attempted without authentication");
                return Page();
            }

            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                StatusMessage = "Authentication required. Please login to Spotify first.";
                _logger.LogWarning("Start monitor attempted without authentication access token");
                return Page();
            }

            // Store the access token in IMemoryCache for background service usage
            this.WithService<IMemoryCache>(cache =>
            {
                cache.Set("SpotifyAuthToken", accessToken, TimeSpan.FromMinutes(60));
            });

            // Start monitoring
            try
            {
                await this.WithServiceAsync<SpotifyPlaybackMonitorService>(async monitorService =>
                {
                    await monitorService.StartMonitoringAsync();
                    StatusMessage = "Playback monitoring started successfully.";
                    HighPerformanceLogging.LogMonitoringStarted(_logger);
                });
            }
            catch (Exception ex)
            {
                HighPerformanceLogging.LogMonitoringError(_logger, ex, "SpotifyQueueMonitorService");
                StatusMessage = "Error starting monitoring. Please try again.";
            }

            try
            {
                await OnGetAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard state after starting monitor");
                IsMonitoring = this.ResolveService<SpotifyPlaybackMonitorService>().IsMonitoring;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostStopMonitorAsync()
        {
            // Only allow stopping monitor if authenticated
            if (!await _spotifyService.EnsureAuthenticatedAsync())
            {
                StatusMessage = "Authentication required. Please login to Spotify first.";
                _logger.LogWarning("Stop monitor attempted without authentication");
                return Page();
            }

            try
            {
                var monitorService = this.ResolveService<SpotifyPlaybackMonitorService>();
                if (monitorService.IsMonitoring)
                {
                    await monitorService.StopMonitoringAsync();
                    StatusMessage = "Playback monitoring stopped successfully.";
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
                HighPerformanceLogging.LogMonitoringError(_logger, ex, "SpotifyPlaybackMonitorService");
                StatusMessage = "Error stopping monitoring. Please try again.";
            }

            try
            {
                await OnGetAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard state after stopping monitor");
                IsMonitoring = this.ResolveService<SpotifyPlaybackMonitorService>().IsMonitoring;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostRefreshDataAsync()
        {
            // Only allow refresh if authenticated
            if (!await _spotifyService.EnsureAuthenticatedAsync())
            {
                StatusMessage = "Authentication required. Please login to Spotify first.";
                _logger.LogWarning("Refresh data attempted without authentication");
                return Page();
            }

            try
            {
                CurrentUser = await _spotifyService.CurrentUserAsync();
                string? userId = CurrentUser?.Id;
                if (string.IsNullOrEmpty(userId))
                {
                    StatusMessage = "Unable to retrieve user information. Please login to Spotify first.";
                    _logger.LogWarning("Refresh data attempted without valid user ID");
                    return Page();
                }

                var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
                if (string.IsNullOrEmpty(accessToken))
                {
                    StatusMessage = "Authentication required. Please login to Spotify first.";
                    return Page();
                }

                var spotifyConfig = this.ResolveService<SpotifyClientConfig>();
                var spotify = new SpotifyClient(spotifyConfig.WithToken(accessToken));

                MusicQueue? existingQueue = null;
                this.WithService<IMemoryCache>(cache =>
                {
                    var cacheKey = $"queue_{userId}";
                    cache.TryGetValue(cacheKey, out existingQueue);
                });

                var currentlyPlayingResponse = await this.WithServiceAsync<SpotifyRequestCache, CurrentlyPlaying?>(
                    async requestCache => await requestCache.GetCurrentlyPlayingAsync(spotify));

                if (currentlyPlayingResponse?.Item is not FullTrack currentTrack)
                {
                    StatusMessage = "No Track is currently Playing";
                    return Page();
                }

                bool currentTrackChanged = existingQueue == null || !existingQueue.MatchesCurrentPlayback(currentTrack.Id);

                if (currentTrackChanged) 
                {
                    _logger.LogInformation("Current track changed, validating queue");

                    var isValid = await _queueService.ValidateQueueAsync(userId, spotify, currentTrack.Id);
                    if (!isValid)
                    {
                        _queueService.InvalidateQueue(userId);
                        var cacheKey = $"queue_{userId}";
                        this.WithService<IMemoryCache>(cache =>
                        {
                            cache.Remove(cacheKey);
                            StatusMessage = "Queue was out of sync. Refreshed all data.";
                            HighPerformanceLogging.LogMemoryCacheCleared(_logger);
                        });
                    }
                    else
                    {
                        // Track changed but queue is valid - sync queue with current playback
                        var queueResponse = await this.WithServiceAsync<SpotifyRequestCache, QueueResponse?>(
                            async requestCache => await requestCache.GetQueueAsync(spotify));

                        var queueTracks = new List<Track>();
                        if (queueResponse?.Queue != null)
                        {
                            foreach (var item in queueResponse.Queue.OfType<FullTrack>())
                            {
                                var track = await _queueService.GetTrackFromQueueAsync(userId, item.Id, spotify);
                                if (track != null)
                                {
                                    queueTracks.Add(track);
                                }
                            }
                        }
                        
                        var currentTrackAsTrackObject = await _queueService.GetTrackFromQueueAsync(userId, currentTrack.Id, spotify);

                        if (existingQueue != null && currentTrackAsTrackObject != null)
                        {
                            existingQueue = _queueService.SyncWithCurrentPlayback(userId, existingQueue, currentTrackAsTrackObject, queueTracks);

                            // Update the cache with synced queue
                            this.WithService<IMemoryCache>(cache =>
                            {
                                var cacheKey = $"queue_{userId}";
                                cache.Set(cacheKey, existingQueue, TimeSpan.FromMinutes(30));
                            });
                        }

                        StatusMessage = "Queue synchronized with current playback.";
                    }
                }
                else
                {
                    StatusMessage = "Queue is up to date. No changes detected.";
                    _logger.LogInformation("Queue validation: Current track unchanged");
                }

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

            try
            {
                await OnGetAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard state after data refresh");
            }
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
                CurrentUser = await _spotifyService.CurrentUserAsync();
                string? userId = CurrentUser?.Id;
                if (string.IsNullOrEmpty(userId))
                {
                    StatusMessage = "Unable to retrieve user information for lyrics analysis.";
                    return Page();
                }

                var analysisResult = await this.WithServiceAsync<FreeLyricsProviderService, string>(async freeLyricsProvider =>
                {
                    var spotifyConfig = this.ResolveService<SpotifyClientConfig>();
                    var spotify = new SpotifyClient(spotifyConfig.WithToken(accessToken));
                    var queue = await _queueService.GetQueueAsync(userId,spotify);

                    if (queue == null || queue.IsEmpty)  return "Your queue is empty. Please add tracks to analyze lyrics.";


                    var track = queue.CurrentTrack?.FullTrack;
                    if (track == null)
                    {
                        return "No current track available.";
                    }
                    var artistName = track.Artists.FirstOrDefault()?.Name ?? "Unknown";
                    var lyrics = await freeLyricsProvider.GetLyricsAsync(artistName, track.Name);
                    if (string.IsNullOrEmpty(lyrics))
                    {
                        return "No lyrics found for current track.";
                    }

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

        public async Task<IActionResult> OnPostSaveFilterPreferencesAsync()
        {
            if (!await _spotifyService.EnsureAuthenticatedAsync())
            {
                StatusMessage = "Authentication required. Please login to Spotify first.";
                _logger.LogWarning("Save filter preferences attempted without authentication");
                return RedirectToPage("/Index");
            }

            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                StatusMessage = "Authentication required. Please login to Spotify first.";
                return RedirectToPage("/Index");
            }

            try
            {
                CurrentUser = await _spotifyService.CurrentUserAsync();
                string? userId = CurrentUser?.Id ?? _httpContextAccessor.HttpContext?.Session.GetString("SpotifyUserId");
                if (string.IsNullOrEmpty(userId))
                {
                    StatusMessage = "Authentication required to update filter preferences.";
                    _logger.LogWarning("Save filter preferences attempted without valid user ID");
                    return Page();
                }

                _queueService.SaveUserFilterPreferences(userId, FilterPreferences);
                StatusMessage = "Filter preferences saved successfully.";
                _logger.LogInformation("Saved filter preferences for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving filter preferences");
                StatusMessage = "Error saving filter preferences. Please try again.";
            }

            await OnGetAsync();
            return Page();
        }

        private async Task<string> GetUserSpecificCacheKey(string baseKey)
        { 
            CurrentUser = await _spotifyService.CurrentUserAsync();
            var userId = CurrentUser?.Id;
            return $"{userId}_{baseKey}";
        }
    }
}