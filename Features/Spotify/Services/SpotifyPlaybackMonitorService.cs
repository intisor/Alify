using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Alify.Features.Spotify.Events;

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
        
        // Monitoring state
        private bool _isMonitoring;
        private bool _isRunning;
        
        // Authentication and timing
        private DateTime _lastAuthWarning = DateTime.MinValue;
        private readonly TimeSpan _authWarningCooldown = TimeSpan.FromMinutes(5);
        private int _basePollingIntervalMs = 5000; // Default 5 seconds

        public SpotifyPlaybackMonitorService(
            IServiceProvider serviceProvider,
            ILogger<SpotifyPlaybackMonitorService> logger,
            IMemoryCache cache,
            ISpotifySubject spotifySubject)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cache = cache;
            _spotifySubject = spotifySubject;
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
                    
                    // Use intelligent delay based on track remaining time or fallback to base interval
                    var delayMs = nextDelayMs ?? _basePollingIntervalMs;
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
                    await Task.Delay(_basePollingIntervalMs, stoppingToken);
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
            // Check if monitoring is enabled
            if (!_isMonitoring)
            {
                // Still run but less frequently when monitoring is disabled
                return _basePollingIntervalMs * 2;
            }

            // Check for auth token
            if (!_cache.TryGetValue("SpotifyAuthToken", out string? spotifyToken) || string.IsNullOrEmpty(spotifyToken))
            {
                if (DateTime.UtcNow - _lastAuthWarning > _authWarningCooldown)
                {
                    _logger.LogWarning("Spotify authentication token not found. Authentication may be required.");
                    _lastAuthWarning = DateTime.UtcNow;
                }
                return _basePollingIntervalMs;
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
                return _basePollingIntervalMs;
            }

            try
            {
                // Get current playback info and notify observers
                var playbackInfo = await spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                if (playbackInfo != null)
                {
                    await _spotifySubject.NotifyPlaybackInfoAsync(playbackInfo);
                }

                // Check for flagged content and skip if necessary
                await CheckAndSkipFlaggedContentAsync(spotify);

                // Calculate intelligent delay based on remaining time
                if (playbackInfo?.CurrentlyPlaying != null && playbackInfo.RemainingTimeMs > 2000)
                {
                    var nextCheckMs = playbackInfo.RemainingTimeMs - 2000; // 2s buffer
                    _logger.LogDebug("Next check in {NextCheckSeconds} seconds based on track remaining time", nextCheckMs / 1000.0);
                    return (int)nextCheckMs;
                }

                // Reset auth warning if we successfully got data
                if (_lastAuthWarning != DateTime.MinValue)
                    _lastAuthWarning = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring Spotify playback");
            }

            return null; // Use base interval
        }

        /// <summary>
        /// Checks the currently playing track and skips it if flagged as inappropriate.
        /// Notifies observers when a track is skipped.
        /// </summary>
        private async Task CheckAndSkipFlaggedContentAsync(SpotifyAPI.Web.SpotifyClient spotify)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var spotifyService = scope.ServiceProvider.GetRequiredService<SpotifyService>();

                var skippedTrack = await spotifyService.SkipIfFlaggedAsync(spotify);
                if (skippedTrack != null)
                {
                    await _spotifySubject.NotifyTrackSkippedEventAsync(skippedTrack);
                    _logger.LogInformation("Track skipped and observers notified: {TrackName} by {Artist}", 
                        skippedTrack.FullTrack.Name, 
                        skippedTrack.FullTrack.Artists.FirstOrDefault()?.Name ?? "Unknown");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and skipping flagged content");
            }
        }

        #endregion
    }
}