using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;

namespace Alify.Services
{
    [DebuggerDisplay("CacheTimeout: {_cacheTimeout.TotalSeconds}s")]
    public class SpotifyRequestCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(30);
        private readonly ILogger<SpotifyRequestCache> _logger;

        public SpotifyRequestCache(IMemoryCache memoryCache, ILogger<SpotifyRequestCache> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<CurrentlyPlaying?> GetCurrentlyPlayingAsync(SpotifyClient spotify)
        {
            return await _memoryCache.GetOrCreateAsync("CurrentlyPlaying", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheTimeout;
                try
                {
                    return await spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                }
                catch (APIException ex)
                {
                    _logger.LogError(ex, "Error getting currently playing");
                    return null;
                }
            });
        }

        public async Task<QueueResponse?> GetQueueAsync(SpotifyClient spotify)
        {
            return await _memoryCache.GetOrCreateAsync("Queue", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheTimeout;
                try
                {
                    return await spotify.Player.GetQueue();
                }
                catch (APIException ex)
                {
                    _logger.LogError(ex, "Error getting queue");
                    return null;
                }
            });
        }

        public void ClearCache()
        {
            _memoryCache.Remove("CurrentlyPlaying");
            _memoryCache.Remove("Queue");
        }
    }
}