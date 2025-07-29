using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Alify.Services
{
    public class SpotifyQueueMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SpotifyQueueMonitorService> _logger;
        private readonly IMemoryCache _cache;
        private bool _isMonitoring;
        private DateTime _lastAuthWarning = DateTime.MinValue;
        private readonly TimeSpan _authWarningCooldown = TimeSpan.FromMinutes(5);

        public SpotifyQueueMonitorService(
            IServiceProvider serviceProvider,
            ILogger<SpotifyQueueMonitorService> logger,
            IMemoryCache cache)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cache = cache;
        }

        public void StartMonitoring() => SetMonitoringStatus(true);
        public void StopMonitoring() => SetMonitoringStatus(false);
        public bool IsMonitoring => _isMonitoring;

        private void SetMonitoringStatus(bool isRunning)
        {
            _isMonitoring = isRunning;
            _cache.Set("MonitoringStatus", isRunning ? "Running" : "Stopped", TimeSpan.FromHours(1));
            _logger.LogInformation($"Spotify queue monitoring {(isRunning ? "started" : "stopped")}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Spotify Queue Monitor Service started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_isMonitoring)
                {
                    await CheckQueueAsync();
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                else
                {
                    // Exit the loop if not monitoring
                    break;
                }
            }
            _logger.LogInformation("Spotify Queue Monitor Service stopped.");
        }

        private async Task CheckQueueAsync()
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