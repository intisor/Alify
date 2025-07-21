using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;

namespace Alify.Services
{
    public class SpotifyQueueMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SpotifyQueueMonitorService> _logger;
        private readonly IMemoryCache _cache;
        private bool _isMonitoring = false;

        public SpotifyQueueMonitorService(
            IServiceProvider serviceProvider, 
            ILogger<SpotifyQueueMonitorService> logger,
            IMemoryCache cache)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cache = cache;
        }

        public void StartMonitoring()
        {
            _isMonitoring = true;
            _cache.Set("MonitoringStatus", "Running", TimeSpan.FromHours(1));
            _logger.LogInformation("Spotify queue monitoring started");
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _cache.Set("MonitoringStatus", "Stopped", TimeSpan.FromHours(1));
            _logger.LogInformation("Spotify queue monitoring stopped");
        }

        public bool IsMonitoring => _isMonitoring;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Spotify Queue Monitor Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_isMonitoring)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var spotifyService = scope.ServiceProvider.GetRequiredService<SpotifyService>();
                        var requestCache = scope.ServiceProvider.GetRequiredService<SpotifyRequestCache>();
                        
                        var spotify = spotifyService.GetSpotifyClient();
                        if (spotify != null)
                        {
                            await MonitorAndSkipFlaggedTracksAsync(spotifyService, spotify, requestCache);
                        }
                        else
                        {
                            _logger.LogWarning("Spotify client not available - authentication may be required");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while monitoring Spotify queue");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }

            _logger.LogInformation("Spotify Queue Monitor Service stopped");
        }

        private async Task MonitorAndSkipFlaggedTracksAsync(SpotifyService spotifyService, SpotifyClient spotify, SpotifyRequestCache requestCache)
        {
            try
            {
                var playbackInfo = await spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                if (playbackInfo == null) return;

                // Check if current track is flagged and skip if necessary
                if (playbackInfo.CurrentlyPlaying?.IsFlagged == true)
                {
                    await spotify.Player.SkipNext(new PlayerSkipNextRequest());
                    _logger.LogInformation("Skipped flagged current track: {TrackName} by {ArtistName}",
                        playbackInfo.CurrentlyPlaying.FullTrack.Name,
                        playbackInfo.CurrentlyPlaying.FullTrack.Artists[0].Name);
                    
                    // Clear request cache after skipping to get fresh data
                    requestCache.ClearCache();
                    return;
                }

                // Check if we're near the end of current track and next track is flagged
                if (playbackInfo.RemainingTimeMs <= 5000) // 5 seconds or less remaining
                {
                    var nextTrack = playbackInfo.Queue?.FirstOrDefault();
                    if (nextTrack?.IsFlagged == true)
                    {
                        await spotify.Player.SkipNext(new PlayerSkipNextRequest());
                        _logger.LogInformation("Pre-skipped flagged upcoming track: {TrackName} by {ArtistName}",
                            nextTrack.FullTrack.Name,
                            nextTrack.FullTrack.Artists[0].Name);
                        
                        // Clear request cache after skipping to get fresh data
                        requestCache.ClearCache();
                    }
                }
            }
            catch (APIException ex)
            {
                _logger.LogWarning("Spotify API error: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in MonitorAndSkipFlaggedTracksAsync");
            }
        }
    }
}