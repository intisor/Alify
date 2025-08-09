using Alify.Core.Models;
using Alify.Services;
using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;
using System.Linq;

namespace Alify.Features.Spotify.Services
{
	public class QueueService
	{
		private readonly IMemoryCache _cache;
		private readonly SpotifyRequestCache _spotifyRequest;
		private readonly ArtistLyricService _artistLyricService;
		private readonly ILogger<QueueService> _logger;
        private readonly SemaphoreSlim _queueSemaphore = new(1, 1);
        public QueueService(IMemoryCache cache, SpotifyRequestCache spotifyRequest, ArtistLyricService artistLyricService, ILogger<QueueService> logger)
		{
			_cache = cache;
			_spotifyRequest = spotifyRequest;
			_artistLyricService = artistLyricService;
			_logger = logger;
		}
		public async Task<MusicQueue> GetQueueAsync(string userId,SpotifyClient spotify)
		{
			_cache.Set("SpotifyUserId", userId);
			var cacheKey = $"queue_{userId}";

			if (_cache.TryGetValue(cacheKey, out MusicQueue cachedQueue) && !cachedQueue.IsExpired)
				return cachedQueue;

			var currentlyPlaying = await _spotifyRequest.GetCurrentlyPlayingAsync(spotify);
			var queueResponse = await _spotifyRequest.GetQueueAsync(spotify);

			if (currentlyPlaying?.Item is not FullTrack currentTrack || queueResponse.Queue is null) return null;


            var newQueue = new MusicQueue();

			newQueue.Tracks.Add(await BuildTrackAsync(currentTrack));

			foreach (var item in queueResponse.Queue.Take(9))
			{
				if (item is FullTrack track)
				{
					newQueue.EnqueueTrack(await BuildTrackAsync(track));
				}
			}
			_cache.Set(cacheKey, newQueue, TimeSpan.FromMinutes(7));
			newQueue.CachedAt = DateTime.UtcNow;
			_logger.LogInformation("Queue for user {UserId} built with {TrackCount} tracks.", userId, newQueue.Tracks.Count);
			return newQueue;
		}
		private async Task<Track> BuildTrackAsync(FullTrack track)
		{
			var artist = track.Artists.FirstOrDefault()?.Name ?? string.Empty;
			var lyrics = await _spotifyRequest.GetTrackLyricsAsync(artist, track.Name) ?? string.Empty;

			var queueTrack = new Track
			{
				FullTrack = track,
				Lyrics = lyrics
			};

			queueTrack.MappedLyrics = await Task.Run(() =>
				_artistLyricService.ParseLyricsWithArtistMapping(lyrics, artist));

			return queueTrack;
		}
		public async Task<bool> ValidateQueueAsync(string userId, SpotifyClient spotify)
		{
			var queue = await GetQueueAsync(userId, spotify);
			if (queue == null || queue.IsEmpty)
			{
				_logger.LogWarning("Queue for user {UserId} is empty or null.", userId);
				return false;
			}
			var currentPlaybackResponse = await spotify.Player.GetCurrentPlayback();
			if (currentPlaybackResponse?.Item is not FullTrack currentTrack) return false;

			var isValid = queue.MatchesCurrentPlayback(currentTrack.Id);
			if (!isValid)
			{
				_logger.LogInformation("Queue out of sync - invalidating cache");
				InvalidateQueue(userId);
			}

			return isValid;
		}
		public void InvalidateQueue(string userId)
		{
			var cacheKey = $"queue_{userId}";
			_cache.Remove(cacheKey);
		}
		public async Task<Track> GetTrackFromQueueAsync(string userId, string trackId, SpotifyClient spotify)
		{
			var queue = await GetQueueAsync(userId, spotify);
			return queue?.Tracks.FirstOrDefault(t => t.FullTrack.Id == trackId);
		}
        public async Task<Track> SkipFlaggedSongsAsync(string userId, SpotifyClient spotify)
        {
            await _queueSemaphore.WaitAsync();
            try
            {
                var queue = await GetQueueAsync(userId, spotify);
                if (queue == null || queue.IsEmpty) return null;

                if (queue.CurrentTrack.IsFlagged)
                {
                    _logger.LogInformation("Skipping flagged track: {TrackName}", queue.CurrentTrack.FullTrack.Name);
                    await spotify.Player.SkipNext();

                    queue.DequeueTrack();
                    var cacheKey = $"queue_{userId}";
                    _cache.Set(cacheKey, queue, TimeSpan.FromMinutes(5));
                }
                return queue?.CurrentTrack;
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }
		public async Task<MusicQueue> SyncWithCurrentPlaybackAsync(string userId,MusicQueue existingQueue,Track currentTrack, List<Track> queueTracks)
		{
            if (existingQueue == null)
            {
                // Create a new queue if none exists
                existingQueue = new MusicQueue { Tracks = [currentTrack], CurrentIndex = 0, CachedAt = DateTime.UtcNow };
                existingQueue.Tracks.AddRange(queueTracks);
                return existingQueue;
            }

            if (existingQueue.CurrentTrack.FullTrack.Id == currentTrack.FullTrack.Id)
            {
        
                var updatedTracks = new List<Track> { currentTrack };
                updatedTracks.AddRange(queueTracks);
				existingQueue.Tracks = updatedTracks;
                existingQueue.CurrentIndex = 0;

                return existingQueue;
            }
            // adjusting queue
            for (int i = 0; i < existingQueue.Tracks.Count; i++)
            {
                if (existingQueue.Tracks[i].FullTrack.Id == currentTrack.FullTrack.Id)
                {
                    // Found the current track in the existing queue
                    existingQueue.CurrentIndex = i;

					existingQueue.Tracks[i] = currentTrack; // Update the current track

                    if (queueTracks.Count > 0)
                    {
                        // The current track is at index 'i'. The tracks after it might be out of sync.
                        // We'll remove the old upcoming tracks and add the new ones from the fresh API call.

                        // Check if there are any tracks after the current one in the existing queue.
                        if (i < existingQueue.Tracks.Count - 1)
                        {
                            // Remove all tracks from the one after the current track to the end.
                            existingQueue.Tracks.RemoveRange(i + 1, existingQueue.Tracks.Count - (i + 1));
                        }

                        // Now, add the fresh list of upcoming tracks.
                        existingQueue.Tracks.AddRange(queueTracks);
                    }
                    return existingQueue;
                }
            }

            var newQueue = new MusicQueue
            {
                Tracks = new List<Track> { currentTrack },
                CurrentIndex = 0,
                CachedAt = DateTime.UtcNow
            };
            newQueue.Tracks.AddRange(queueTracks);

			return newQueue;
        }
    }
}
