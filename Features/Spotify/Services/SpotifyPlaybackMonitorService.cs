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
    /// Enhanced with edge case handling for improved reliability and responsiveness.
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
        
        // Edge case handling state
        private int _consecutiveShortRemainingCount = 0;
        private bool _wasPausedLastCheck = false;
        private DateTime _lastTrackChangeDetection = DateTime.MinValue;
        private int _pausedPollingAttempts = 0;
        private DateTime _lastSuccessfulPoll = DateTime.MinValue;
        // User skip detection
        private int _previousTrackRemainingMs = 0;

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
            _logger.LogDebug("Enhanced Playback Monitor configured with: Active={Active}ms, Paused={Paused}ms, PostSkip={PostSkip}ms, ShortRemaining={ShortRemaining}ms",
                _options.ActivePlaybackPollingIntervalMs,
                _options.PausedPlaybackPollingIntervalMs,
                _options.PostSkipCheckDelayMs,
                _options.ShortRemainingTrackPollingMs);
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
            _logger.LogInformation("Enhanced Spotify Playback Monitor Service started");

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
                    _logger.LogInformation("Enhanced Spotify Playback Monitor Service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Enhanced Spotify Playback Monitor Service");
                    _consecutiveErrorCount++;
                    await Task.Delay(CalculateErrorBackoffDelay(), stoppingToken);
                }
            }

            _isRunning = false;
            _logger.LogInformation("Enhanced Spotify Playback Monitor Service stopped");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Enhanced Spotify Playback Monitor Service is stopping");
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
                _lastSuccessfulPoll = DateTime.UtcNow;
                
                // Get current playback info
                var playbackInfo = await spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                bool trackChanged = false;
                bool playbackStateChanged = false;
                
                if (playbackInfo?.CurrentlyPlaying != null)
                {
                    string currentTrackId = playbackInfo.CurrentlyPlaying.FullTrack.Id;
                    trackChanged = currentTrackId != _lastTrackId;
                    _lastTrackId = currentTrackId;
                    
                    // User skip detection logic
                    if (trackChanged)
                    {
                        // If previous track had significant time left, treat as skip
                        if (_previousTrackRemainingMs > _options.SkipDetectionRemainingTimeThresholdMs)
                        {
                            _logger.LogInformation("Detected possible manual skip (remaining ms: {Remaining})", (object)_previousTrackRemainingMs);
                            // Optionally notify observers here, e.g.:
                            // await _spotifySubject.NotifyTrackSkippedEventAsync(playbackInfo.CurrentlyPlaying);
                        }
                        _previousTrackRemainingMs = playbackInfo.RemainingTimeMs ?? 0;
                    }
                    else if (playbackInfo?.RemainingTimeMs != null)
                    {
                        _previousTrackRemainingMs = playbackInfo.RemainingTimeMs.Value;
                    }
                    
                    // Detect track changes for edge case handling
                    if (trackChanged)
                    {
                        _lastTrackChangeDetection = DateTime.UtcNow;
                        _consecutiveShortRemainingCount = 0; // Reset short remaining counter on track change
                        _pausedPollingAttempts = 0; // Reset paused polling attempts
                        _logger.LogDebug("Track changed detected: {TrackName}", playbackInfo.CurrentlyPlaying.FullTrack.Name);
                    }
                    
                    // Track playback state changes
                    bool isCurrentlyPlaying = playbackInfo.RemainingTimeMs > 0 && playbackInfo.CurrentlyPlaying != null;
                    playbackStateChanged = isCurrentlyPlaying != _wasPlayingLastCheck;
                    _wasPlayingLastCheck = isCurrentlyPlaying;
                    
                    // Handle pause state transitions
                    if (playbackStateChanged)
                    {
                        if (!isCurrentlyPlaying)
                        {
                            _wasPausedLastCheck = true;
                            _pausedPollingAttempts = 0;
                            _logger.LogDebug("Playback paused detected");
                        }
                        else
                        {
                            _wasPausedLastCheck = false;
                            _pausedPollingAttempts = 0;
                            _logger.LogDebug("Playback resumed detected");
                        }
                    }
                }
                else
                {
                    // No current playback
                    if (_wasPlayingLastCheck)
                    {
                        playbackStateChanged = true;
                        _logger.LogDebug("Playback stopped detected");
                    }
                    _wasPlayingLastCheck = false;
                    _wasPausedLastCheck = false;
                }

                // Send SSE updates with enhanced logic
                bool shouldSendUpdate = playbackInfo != null && 
                    (trackChanged || 
                     playbackStateChanged ||
                     (DateTime.UtcNow - _lastPlaybackInfoUpdate).TotalMilliseconds > _options.MinimumSSEUpdateIntervalMs);
                
                if (shouldSendUpdate)
                {
                    await _spotifySubject.NotifyPlaybackInfoAsync(playbackInfo);
                    _lastPlaybackInfoUpdate = DateTime.UtcNow;
                    _logger.LogDebug("Sent enhanced playback info SSE update (trackChanged: {TrackChanged}, stateChanged: {StateChanged})", 
                        trackChanged, playbackStateChanged);
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

                // Enhanced intelligent delay calculation with edge case handling
                if (playbackInfo?.CurrentlyPlaying != null && playbackInfo.RemainingTimeMs > 0)
                {
                    return CalculateIntelligentDelay(playbackInfo);
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
        
        #region Enhanced Timing Calculation Methods
        
        /// <summary>
        /// Enhanced intelligent delay calculation with comprehensive edge case handling
        /// </summary>
        private int? CalculateIntelligentDelay(dynamic playbackInfo)
        {
            var remainingTimeMs = (int)(playbackInfo.RemainingTimeMs ?? 0);
            // Use the same logic as above for isPlaying
            bool isPlaying = remainingTimeMs > 0 && playbackInfo.CurrentlyPlaying != null;
            
            // Edge Case 1: Service starts near end of track (< ShortRemainingThresholdMs)
            if (remainingTimeMs < _options.ShortRemainingThresholdMs)
            {
                _consecutiveShortRemainingCount++;
                
                // If we've hit short remaining time multiple times in a row, use frequent polling
                if (_consecutiveShortRemainingCount >= _options.MaxConsecutiveShortRemainingAttempts)
                {
                    _logger.LogDebug("Consecutive short remaining time detected ({Count} times), using frequent polling", 
                        (object)_consecutiveShortRemainingCount);
                    return _options.ShortRemainingTrackPollingMs;
                }
                
                // Otherwise, use a very short delay to catch the track transition quickly
                var shortDelay = Math.Min(remainingTimeMs + _options.TrackTransitionBufferMs, _options.ShortRemainingTrackPollingMs);
                _logger.LogDebug("Short remaining time ({RemainingMs}ms), using short delay: {DelayMs}ms", 
                    (object)remainingTimeMs, (object)shortDelay);
                return shortDelay;
            }
            else
            {
                // Reset short remaining counter when we have sufficient time
                _consecutiveShortRemainingCount = 0;
            }
            
            // Edge Case 2: Paused playback handling
            if (!isPlaying)
            {
                _pausedPollingAttempts++;
                
                // Use progressive backoff for paused state to save resources
                var pausedDelay = Math.Min(
                    _options.PausedPlaybackPollingIntervalMs * Math.Min(_pausedPollingAttempts, _options.MaxPausedBackoffMultiplier),
                    _options.MaximumPollingIntervalMs);
                
                _logger.LogDebug("Paused playback detected (attempt {Attempts}), using paused interval: {DelayMs}ms", 
                    (object)_pausedPollingAttempts, (object)pausedDelay);
                return (int)pausedDelay;
            }
            else
            {
                // Reset paused attempts when playing
                _pausedPollingAttempts = 0;
            }
            
            // Edge Case 3: Recent track change detection for missed transitions
            var timeSinceLastTrackChange = DateTime.UtcNow - _lastTrackChangeDetection;
            if (timeSinceLastTrackChange.TotalMilliseconds < _options.RecentTrackChangeWindowMs)
            {
                // Use more frequent polling after recent track changes to ensure we don't miss rapid changes
                var recentChangeDelay = Math.Min(_options.RecentTrackChangePollingMs, remainingTimeMs / 2);
                _logger.LogDebug("Recent track change detected ({TimeSinceMs}ms ago), using frequent polling: {DelayMs}ms", 
                    (object)timeSinceLastTrackChange.TotalMilliseconds, (object)recentChangeDelay);
                return recentChangeDelay;
            }
            
            // Standard intelligent timing calculation
            // Calculate adaptive buffer based on track length
            int bufferMs = Math.Max(
                (int)(remainingTimeMs * _options.TrackEndBufferPercentage),
                _options.MinimumTrackEndBufferMs);
            
            // Ensure we have at least minimum polling interval
            var nextCheckMs = Math.Max(
                (int)(remainingTimeMs - bufferMs),
                _options.MinimumPollingIntervalMs);
            
            // Cap the max delay
            nextCheckMs = Math.Min(nextCheckMs, _options.MaximumPollingIntervalMs);
            
            // Additional safety check: if calculated delay would extend past successful poll window, reduce it
            var maxAllowableDelay = (int)Math.Max(
                _options.MaxTimeBetweenSuccessfulPollsMs - (DateTime.UtcNow - _lastSuccessfulPoll).TotalMilliseconds,
                _options.MinimumPollingIntervalMs);
            
            nextCheckMs = Math.Min(nextCheckMs, maxAllowableDelay);
            
            _logger.LogDebug("Standard intelligent timing: remaining={RemainingMs}ms, buffer={BufferMs}ms, nextCheck={NextCheckMs}ms", 
                (object)remainingTimeMs, (object)bufferMs, (object)nextCheckMs);
            return nextCheckMs;
        }
        
        /// <summary>
        /// Calculates the appropriate delay until the next poll based on various factors with edge case enhancements
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
            
            // Enhanced state-based intervals with paused state handling
            if (_wasPausedLastCheck)
            {
                // Progressive backoff for paused state
                var pausedDelay = _options.PausedPlaybackPollingIntervalMs * Math.Min(_pausedPollingAttempts + 1, _options.MaxPausedBackoffMultiplier);
                return Math.Min((int)pausedDelay, _options.MaximumPollingIntervalMs);
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
    /// Enhanced configuration options for Spotify playback monitoring with edge case handling
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
        
        // Edge Case Handling Options
        
        /// <summary>
        /// Threshold for considering remaining time as "short" requiring special handling (default: 3000ms)
        /// </summary>
        public int ShortRemainingThresholdMs { get; set; } = 3000;
        
        /// <summary>
        /// Polling interval when track has short remaining time (default: 2000ms)
        /// </summary>
        public int ShortRemainingTrackPollingMs { get; set; } = 2000;
        
        /// <summary>
        /// Maximum consecutive short remaining attempts before switching to frequent polling (default: 3)
        /// </summary>
        public int MaxConsecutiveShortRemainingAttempts { get; set; } = 3;
        
        /// <summary>
        /// Buffer time added when remaining time is very short to catch track transitions (default: 500ms)
        /// </summary>
        public int TrackTransitionBufferMs { get; set; } = 500;
        
        /// <summary>
        /// Maximum multiplier for paused playback backoff (default: 4, so max = 15000 * 4 = 60000ms)
        /// </summary>
        public int MaxPausedBackoffMultiplier { get; set; } = 4;
        
        /// <summary>
        /// Time window after track change to use more frequent polling (default: 10000ms)
        /// </summary>
        public int RecentTrackChangeWindowMs { get; set; } = 10000;
        
        /// <summary>
        /// Polling interval after recent track changes to catch rapid transitions (default: 3000ms)
        /// </summary>
        public int RecentTrackChangePollingMs { get; set; } = 3000;
        
        /// <summary>
        /// Maximum time allowed between successful polls before forcing more frequent checks (default: 30000ms)
        /// </summary>
        public int MaxTimeBetweenSuccessfulPollsMs { get; set; } = 30000;

        /// <summary>
        /// Minimum remaining time (ms) to consider a track change as a skip (default: 2000ms)
        /// </summary>
        public int SkipDetectionRemainingTimeThresholdMs { get; set; } = 2000;
    }
}