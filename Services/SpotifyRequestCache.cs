namespace Alify.Services
{
    [DebuggerDisplay("CacheTimeout: {_cacheTimeout.TotalSeconds}s")]
    public class SpotifyRequestCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(30);

        public SpotifyRequestCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
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
                    Console.WriteLine($"Error getting currently playing: {ex.Message}");
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
                    Console.WriteLine($"Error getting queue: {ex.Message}");
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