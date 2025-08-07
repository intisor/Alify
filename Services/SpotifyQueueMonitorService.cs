namespace Alify.Services
{
    [DebuggerDisplay("IsMonitoring: {_isMonitoring}, AuthWarningCooldown: {_authWarningCooldown.TotalMinutes}min")]
    public class SpotifyQueueMonitorService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SpotifyQueueMonitorService> _logger;
        private readonly IMemoryCache _cache;
        private bool _isMonitoring;
        private DateTime _lastAuthWarning = DateTime.MinValue;
        private readonly TimeSpan _authWarningCooldown = TimeSpan.FromMinutes(5);
        private readonly ITimeTickerManager<TimeTicker> _timeTickerManager;
        private Guid? _currentTickerId;

        public SpotifyQueueMonitorService(
            IServiceProvider serviceProvider,
            ILogger<SpotifyQueueMonitorService> logger,
            IMemoryCache cache)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cache = cache;
            _timeTickerManager = serviceProvider.GetRequiredService<ITimeTickerManager<TimeTicker>>();
        }

        public bool IsMonitoring => _isMonitoring;

        public async Task StartMonitoringAsync()
        {
            var ticker = new TimeTicker
            {
                Function = "CheckQueueAsync",
                ExecutionTime = DateTime.UtcNow.AddSeconds(3),
                Description = "Spotify Queue Monitoring",
                Retries = 3,
                RetryIntervals = new[] { 10, 20, 30 }
            };

            await _timeTickerManager.AddAsync(ticker);
            _currentTickerId = ticker.Id;
            _isMonitoring = true;
            _cache.Set("MonitoringStatus", "Running", TimeSpan.FromHours(1));
            _logger.LogInformation("Spotify queue monitoring started.");
        }

        public async Task StopMonitoringAsync()
        {
            if (_currentTickerId.HasValue)
            {
                await _timeTickerManager.DeleteAsync(_currentTickerId.Value);
                _currentTickerId = null;
            }
            _isMonitoring = false;
            _cache.Set("MonitoringStatus", "Stopped", TimeSpan.FromHours(1));
            _logger.LogInformation("Spotify queue monitoring stopped.");
        }

       
        [TickerFunction("CheckQueueAsync")]
        public async Task CheckQueueAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var spotifyService = scope.ServiceProvider.GetRequiredService<SpotifyService>();

                if (!_cache.TryGetValue("SpotifyAuthToken", out string? spotifyToken) || string.IsNullOrEmpty(spotifyToken))
                {
                    if (DateTime.UtcNow - _lastAuthWarning > _authWarningCooldown)
                    {
                        _logger.LogWarning("Spotify authentication token not found. Authentication may be required.");
                        _lastAuthWarning = DateTime.UtcNow;
                    }
                    return;
                }

                var spotify = await spotifyService.GetSpotifyClientAsync(spotifyToken);

                int? nextMs = null;
                if (spotify != null)
                {
                    try
                    {
                        await spotifyService.SkipIfFlaggedAsync(spotify);
                        // Get playback info to determine next check time
                        var playbackInfo = await spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                        if (playbackInfo?.CurrentlyPlaying != null && playbackInfo.RemainingTimeMs > 2000)
                        {
                            nextMs = playbackInfo.RemainingTimeMs - 2000; // 2s buffer
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in SkipIfFlaggedAsync during queue monitoring.");
                    }
                    if (_lastAuthWarning != DateTime.MinValue)
                        _lastAuthWarning = DateTime.MinValue;
                }
                else if (DateTime.UtcNow - _lastAuthWarning > _authWarningCooldown)
                {
                    _logger.LogWarning("Spotify client not available. Authentication may be required.");
                    _lastAuthWarning = DateTime.UtcNow;
                }

                // Dynamically schedule next check if possible
                if (nextMs.HasValue && nextMs.Value > 0)
                {
                    if (_currentTickerId.HasValue)
                    {
                        await _timeTickerManager.DeleteAsync(_currentTickerId.Value);
                        _currentTickerId = null;
                    }
                    var nextExecution = DateTime.UtcNow.AddMilliseconds(nextMs.Value);
                    var ticker = new TimeTicker
                    {
                        Function = "CheckQueueAsync",
                        ExecutionTime = nextExecution,
                        Description = "Dynamic Spotify Queue Monitoring",
                        Retries = 3,
                        RetryIntervals = new[] { 10, 20, 30 }
                    };
                    await _timeTickerManager.AddAsync(ticker);
                    _currentTickerId = ticker.Id;
                    _logger.LogInformation($"Next Spotify queue check scheduled for {nextExecution:O} (in {nextMs.Value / 1000.0:F1} seconds)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while monitoring Spotify queue.");
            }
        }
    }
}