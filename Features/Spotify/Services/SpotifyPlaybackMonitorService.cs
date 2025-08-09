using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Alify.Features.Spotify.Events;
using Alify.Features.Spotify.Services;

namespace Alify.Services
{
    /// <summary>
    /// A comprehensive service that monitors Spotify playback in the background,
    /// automatically skips flagged content, and broadcasts real-time updates via SSE.
    /// Combines background monitoring with content filtering functionality.
    /// </summary>
    [DebuggerDisplay("IsMonitoring: {_isMonitoring}, IsRunning: {_isRunning}, AuthWarningCooldown: {_authWarningCooldown.TotalMinutes}min")]
    public class SpotifyPlaybackMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SpotifyPlaybackMonitorService> _logger;
        private readonly IMemoryCache _cache;
        private readonly ISpotifySubject _spotifySubject;
        private readonly QueueService _queueService;
        private readonly PlaybackMonitorOptions _options;
        
        // Monitoring state
        private bool _isMonitoring;
        private bool _isRunning;
        
        // Authentication and timing
        private DateTime _lastAuthWarning = DateTime.MinValue;
        private readonly TimeSpan _authWarningCooldown = TimeSpan.FromMinutes(5);
        
        // Adaptive timing state
        private int _consecutiveErrorCount = 0;
        private bool _wasPlayingLastCheck = false;
        private bool _justSkippedTrack = false;
        private DateTime _lastSkipTime = DateTime.MinValue;
        private DateTime _lastPlaybackInfoUpdate = DateTime.MinValue;
        private string _lastTrackId = string.Empty;

        public SpotifyPlaybackMonitorService(
            IServiceProvider serviceProvider,
            ILogger<SpotifyPlaybackMonitorService> logger,
            IMemoryCache cache,
            ISpotifySubject spotifySubject,
            QueueService queueService,
            IOptions<PlaybackMonitorOptions> options = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cache = cache;
            _spotifySubject = spotifySubject;
            _queueService = queueService;
            _options = options?.Value ?? new PlaybackMonitorOptions();
            
            // Log the configured polling intervals for debugging
            _logger.LogDebug("Playback Monitor configured with: Active={Active}ms, Paused={Paused}ms, PostSkip={PostSkip}ms",
                _options.ActivePlaybackPollingIntervalMs,
                _options.PausedPlaybackPollingIntervalMs,
                _options.PostSkipCheckDelayMs);
        }

        /// <summary>
        /// Gets a value indicating whether monitoring is currently enabled.
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        /// <summary>
        /// Gets a value indicating whether the background service is running.
        /// </summary>
        public bool IsRunning => _isRunning;

        #region Background Service Implementation

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _isRunning = true;
            _logger.LogInformation("Spotify Playback Monitor Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var nextDelayMs = await MonitorPlaybackAsync();
                    
                    // Calculate appropriate delay for next check
                    var delayMs = CalculateNextDelay(nextDelayMs);
                    
                    await Task.Delay(delayMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Spotify Playback Monitor Service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Spotify Playback Monitor Service");
                    _consecutiveErrorCount++;
                    await Task.Delay(CalculateErrorBackoffDelay(), stoppingToken);
                }
            }

            _isRunning = false;
            _logger.LogInformation("Spotify Playback Monitor Service stopped");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Spotify Playback Monitor Service is stopping");
            _isRunning = false;
            await base.StopAsync(cancellationToken);
        }

        #endregion

        #region Public Control Methods

        /// <summary>
        /// Starts monitoring Spotify playback for content filtering and real-time updates.
        /// </summary>
        public Task StartMonitoringAsync()
        {
            _isMonitoring = true;
            _cache.Set("MonitoringStatus", "Running", TimeSpan.FromHours(1));
            _logger.LogInformation("Spotify playback monitoring enabled");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops monitoring Spotify playback.
        /// </summary>
        public Task StopMonitoringAsync()
        {
            _isMonitoring = false;
            _cache.Set("MonitoringStatus", "Stopped", TimeSpan.FromHours(1));
            _logger.LogInformation("Spotify playback monitoring disabled");
            return Task.CompletedTask;
        }

        #endregion

        #region Core Monitoring Logic

        private async Task<int?> MonitorPlaybackAsync()
        {
            // Check if we need to reset the post-skip flag
            if (_justSkippedTrack && (DateTime.UtcNow - _lastSkipTime).TotalMilliseconds > _options.PostSkipWindowMs)
            {
                _justSkippedTrack = false;
                _logger.LogDebug("Exited post-skip monitoring mode");
            }
            
            // Check if monitoring is enabled
            if (!_isMonitoring)
            {
                return _options.InactivePollingIntervalMs;
            }

            // Check for auth token
            if (!_cache.TryGetValue("SpotifyAuthToken", out string? spotifyToken) || string.IsNullOrEmpty(spotifyToken))
            {
                if (DateTime.UtcNow - _lastAuthWarning > _authWarningCooldown)
                {
                    _logger.LogWarning("Spotify authentication token not found. Authentication may be required.");
                    _lastAuthWarning = DateTime.UtcNow;
                }
                return _options.NoAuthPollingIntervalMs;
            }

            using var scope = _serviceProvider.CreateScope();
            var spotifyService = scope.ServiceProvider.GetRequiredService<SpotifyService>();

            var spotify = await spotifyService.GetSpotifyClientAsync(spotifyToken);
            if (spotify == null)
            {
                if (DateTime.UtcNow - _lastAuthWarning > _authWarningCooldown)
                {
                    _logger.LogWarning("Spotify client not available. Authentication may be required.");
                    _lastAuthWarning = DateTime.UtcNow;
                }
                return _options.NoAuthPollingIntervalMs;
            }

            try
            {
                // Reset error count on successful API call
                _consecutiveErrorCount = 0;
                
                // Get current playback info
                var playbackInfo = await spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                bool trackChanged = false;
                
                if (playbackInfo?.CurrentlyPlaying != null)
                {
                    string currentTrackId = playbackInfo.CurrentlyPlaying.FullTrack.Id;
                    trackChanged = currentTrackId != _lastTrackId;
                    _lastTrackId = currentTrackId;
                    
                    // Track state for adaptive polling
                    _wasPlayingLastCheck = true;
                }
                else
                {
                    _wasPlayingLastCheck = false;
                }

                // Send SSE updates if we have new information or if sufficient time has passed
                bool shouldSendUpdate = playbackInfo != null && 
                    (trackChanged || 
                     (DateTime.UtcNow - _lastPlaybackInfoUpdate).TotalMilliseconds > _options.MinimumSSEUpdateIntervalMs);
                
                if (shouldSendUpdate)
                {
                    await _spotifySubject.NotifyPlaybackInfoAsync(playbackInfo);
                    _lastPlaybackInfoUpdate = DateTime.UtcNow;
                    _logger.LogDebug("Sent playback info SSE update");
                }

                // Prioritize skip detection - critical for content filtering
                var currentUser = await spotify.UserProfile.Current();
                string userId = currentUser?.Id;
                
                if (!string.IsNullOrEmpty(userId))
                {
                    // Check for flagged songs that need to be skipped
                    var skippedTrack = await _queueService.SkipFlaggedSongsAsync(userId, spotify);
                    
                    if (skippedTrack != null && skippedTrack.IsFlagged)
                    {
                        // Track was skipped - set flags to ensure quick follow-up check
                        _justSkippedTrack = true;
                        _lastSkipTime = DateTime.UtcNow;
                        
                        // Notify about the skip via SSE
                        await _spotifySubject.NotifyTrackSkippedEventAsync(skippedTrack);
                        _logger.LogInformation("Track skipped and observers notified: {TrackName} by {Artist}",
                            skippedTrack.FullTrack.Name,
                            skippedTrack.FullTrack.Artists.FirstOrDefault()?.Name ?? "Unknown");
                            
                        // Return very short delay to quickly check the next track
                        return _options.PostSkipCheckDelayMs;
                    }
                }
                else if (trackChanged)
                {
                    // User ID missing but track changed - may need to re-authenticate
                    _logger.LogWarning("Spotify userId not found but track changed. Cannot check queue for flagged songs.");
                }

                // Calculate intelligent delay based on remaining time
                if (playbackInfo?.CurrentlyPlaying != null && playbackInfo.RemainingTimeMs > 0)
                {
                    // Calculate adaptive buffer based on track length
                    int bufferMs = Math.Max(
                        (int)(playbackInfo.RemainingTimeMs * _options.TrackEndBufferPercentage),
                        _options.MinimumTrackEndBufferMs);
                    
                    // Ensure we have at least minimum polling interval
                    var nextCheckMs = Math.Max(
                        (byte)(playbackInfo.RemainingTimeMs - bufferMs),
                        _options.MinimumPollingIntervalMs);
                    
                    // Cap the max delay
                    nextCheckMs = Math.Min(nextCheckMs, _options.MaximumPollingIntervalMs);
                    
                    _logger.LogDebug("Next check in {NextCheckSeconds}s based on track remaining time", nextCheckMs / 1000.0);
                    return (int)nextCheckMs;
                }

                // Reset auth warning on successful requests
                if (_lastAuthWarning != DateTime.MinValue)
                    _lastAuthWarning = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring Spotify playback");
                throw; // Let the main loop handle this exception with backoff
            }

            return null; // Use default state-based interval
        }
        
        #endregion
        
        #region Timing Calculation Methods
        
        /// <summary>
        /// Calculates the appropriate delay until the next poll based on various factors
        /// </summary>
        private int CalculateNextDelay(int? suggestedDelay)
        {
            // After skip - use very short delay to quickly check next track
            if (_justSkippedTrack)
            {
                return _options.PostSkipCheckDelayMs;
            }
            
            // If we have a calculated delay based on track time, use it
            if (suggestedDelay.HasValue)
            {
                return suggestedDelay.Value;
            }
            
            // Use different intervals based on playback state
            return _wasPlayingLastCheck
                ? _options.ActivePlaybackPollingIntervalMs
                : _options.PausedPlaybackPollingIntervalMs;
        }
        
        /// <summary>
        /// Implements exponential backoff for error conditions
        /// </summary>
        private int CalculateErrorBackoffDelay()
        {
            // Apply exponential backoff with a cap to prevent integer overflow
            int backoffFactor = Math.Min(_consecutiveErrorCount, 6); // 2^6 = 64
            int backoffDelay = _options.BaseErrorBackoffIntervalMs * (1 << backoffFactor);
            
            return Math.Min(backoffDelay, _options.MaximumPollingIntervalMs);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Configuration options for Spotify playback monitoring
    /// </summary>
    public class PlaybackMonitorOptions
    {
        /// <summary>
        /// Polling interval during active playback (default: 5000ms)
        /// </summary>
        public int ActivePlaybackPollingIntervalMs { get; set; } = 5000;
        
        /// <summary>
        /// Polling interval when playback is paused (default: 15000ms)
        /// </summary>
        public int PausedPlaybackPollingIntervalMs { get; set; } = 15000;
        
        /// <summary>
        /// Polling interval when monitoring is disabled (default: 30000ms)
        /// </summary>
        public int InactivePollingIntervalMs { get; set; } = 30000;
        
        /// <summary>
        /// Polling interval when no auth token is available (default: 10000ms)
        /// </summary>
        public int NoAuthPollingIntervalMs { get; set; } = 10000;
        
        /// <summary>
        /// Minimum allowable polling interval (default: 1000ms)
        /// </summary>
        public int MinimumPollingIntervalMs { get; set; } = 1000;
        
        /// <summary>
        /// Maximum polling interval regardless of conditions (default: 2 minutes)
        /// </summary>
        public int MaximumPollingIntervalMs { get; set; } = 120000;
        
        /// <summary>
        /// Minimum time between SSE playback info updates, even if no track changes (default: 10s)
        /// </summary>
        public int MinimumSSEUpdateIntervalMs { get; set; } = 10000;
        
        /// <summary>
        /// Base interval for exponential backoff during errors (default: 1000ms)
        /// </summary>
        public int BaseErrorBackoffIntervalMs { get; set; } = 1000;
        
        /// <summary>
        /// Delay after skipping a track before checking again (default: 800ms)
        /// Critical for quick response to skip flagged content
        /// </summary>
        public int PostSkipCheckDelayMs { get; set; } = 800;
        
        /// <summary>
        /// Time window after a skip where we still consider ourselves in "post-skip" mode (default: 5000ms)
        /// </summary>
        public int PostSkipWindowMs { get; set; } = 5000;
        
        /// <summary>
        /// Percentage of remaining track time to use as buffer (default: 0.05 or 5%)
        /// </summary>
        public double TrackEndBufferPercentage { get; set; } = 0.05;
        
        /// <summary>
        /// Minimum buffer time before track end (default: 2000ms)
        /// </summary>
        public int MinimumTrackEndBufferMs { get; set; } = 2000;
    }
}