using System.Diagnostics;
using Alify.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;

namespace Alify.Services
{
    [DebuggerDisplay("CacheTimeout: {_cacheTimeout.TotalSeconds}s")]
    public class SpotifyRequestCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(50);
        private readonly ILogger<SpotifyRequestCache> _logger;
        private readonly LyricService _lyricService;

        public SpotifyRequestCache(IMemoryCache memoryCache, ILogger<SpotifyRequestCache> logger, LyricService lyricService)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _lyricService = lyricService;
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
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Network error connecting to Spotify API. Check DNS and network connectivity.");
                    return null;
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

        //public async Task<string?> GetCurrentlyPlayingLyricsAsync(SpotifyClient spotify)
        //{
        //    return await _memoryCache.GetOrCreateAsync("CurrentlyPlayingLyrics", async entry =>
        //    {
        //        entry.AbsoluteExpirationRelativeToNow = _cacheTimeout;
                
        //        try
        //        {
        //            var currentlyPlaying = await GetCurrentlyPlayingAsync(spotify);
                    
        //            if (currentlyPlaying?.Item == null || currentlyPlaying.Item.Type != ItemType.Track)
        //            {
        //                return null;
        //            }
                    
        //            var track = (FullTrack)currentlyPlaying.Item;
        //            var artist = track.Artists.FirstOrDefault()?.Name;
        //            var title = track.Name;
                    
        //            if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title))
        //            {
        //                return null;
        //            }
                    
        //            return await _lyricService.GetLyricsAsync(artist, title);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error getting lyrics for currently playing song");
        //            return null;
        //        }
        //    });
        //}

        //public async Task<LyricsModerationResult?> GetCurrentlyPlayingLyricsModerationAsync(SpotifyClient spotify)
        //{
        //    return await _memoryCache.GetOrCreateAsync("CurrentlyPlayingLyricsModeration", async entry =>
        //    {
        //        entry.AbsoluteExpirationRelativeToNow = _cacheTimeout;
                
        //        try
        //        {
        //            var lyrics = await GetCurrentlyPlayingLyricsAsync(spotify);
                    
        //            if (string.IsNullOrEmpty(lyrics))
        //            {
        //                return null;
        //            }
                    
        //            return await _lyricService.ModerateLyricsAsync(lyrics);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error getting lyrics moderation for currently playing song");
        //            return null;
        //        }
        //    });
        //}

       
        public async Task<string?> GetTrackLyricsAsync(string artist, string title)
        {
            if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title))
                return null;

            var cacheKey = $"TrackLyrics_{artist}_{title}";
            return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheTimeout;
                try
                {
                    return await _lyricService.GetLyricsAsync(artist, title);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting lyrics for track {Artist} - {Title}", artist, title);
                    return null;
                }
            });
        }

        public void ClearCache()
        {
            _memoryCache.Remove("CurrentlyPlaying");
            _memoryCache.Remove("Queue");
            _memoryCache.Remove("CurrentlyPlayingLyrics");
            _memoryCache.Remove("CurrentlyPlayingLyricsModeration");
        }
    }
}