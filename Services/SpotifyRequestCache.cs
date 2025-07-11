using SpotifyAPI.Web;

namespace Alify.Services
{
    public class SpotifyRequestCache
    {
        private CurrentlyPlaying? _currentlyPlaying;
        private DateTime? _currentlyPlayingCacheTime;
        private QueueResponse? _queue;
        private DateTime? _queueCacheTime;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(30); // Cache for 30 seconds within a request

        public async Task<CurrentlyPlaying?> GetCurrentlyPlayingAsync(SpotifyClient spotify)
        {
            if (_currentlyPlaying != null && _currentlyPlayingCacheTime.HasValue && DateTime.UtcNow - _currentlyPlayingCacheTime.Value < _cacheTimeout)
            {
                return _currentlyPlaying;
            }

            try
            {
                _currentlyPlaying = await spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                _currentlyPlayingCacheTime = DateTime.UtcNow;
                return _currentlyPlaying;
            }
            catch (APIException ex)
            {
                Console.WriteLine($"Error getting currently playing: {ex.Message}");
                return null;
            }
        }

        public async Task<QueueResponse?> GetQueueAsync(SpotifyClient spotify)
        {
            if (_queue != null && _queueCacheTime.HasValue && DateTime.UtcNow - _queueCacheTime.Value < _cacheTimeout)
            {
                return _queue;
            }

            try
            {
                _queue = await spotify.Player.GetQueue();
                _queueCacheTime = DateTime.UtcNow;
                return _queue;
            }
            catch (APIException ex)
            {
                Console.WriteLine($"Error getting queue: {ex.Message}");
                return null;
            }
        }

        public void ClearCache()
        {
            _currentlyPlaying = null;
            _currentlyPlayingCacheTime = null;
            _queue = null;
            _queueCacheTime = null;
        }
    }
}