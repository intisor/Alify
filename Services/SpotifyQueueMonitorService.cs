using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;


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

        // Fix cron expression to be valid for TickerQ (5 fields: minute, hour, day, month, day-of-week)
        [TickerFunction("CheckQueueAsync", "*/3 * * * *")]
        public async Task CheckQueueAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var spotifyService = scope.ServiceProvider.GetRequiredService<SpotifyService>();
                var spotify = await spotifyService.GetSpotifyClientAsync();

                if (spotify != null)
                {
                    await spotifyService.SkipIfFlaggedAsync(spotify);
                    _lastAuthWarning = DateTime.MinValue;
                }
                else if (DateTime.UtcNow - _lastAuthWarning > _authWarningCooldown)
                {
                    _logger.LogWarning("Spotify client not available. Authentication may be required.");
                    _lastAuthWarning = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while monitoring Spotify queue.");
            }
        }
    }
}