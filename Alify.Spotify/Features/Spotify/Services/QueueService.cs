using Alify.Core.Models;
using Alify.Core.Services;
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
		public async Task<MusicQueue?> GetQueueAsync(string userId,SpotifyClient spotify)
		{
			_cache.Set("SpotifyUserId", userId);
			var cacheKey = $"queue_{userId}";

			if (_cache.TryGetValue(cacheKey, out MusicQueue? cachedQueue) && cachedQueue != null && !cachedQueue.IsExpired)
				return cachedQueue;

			var currentlyPlaying = await _spotifyRequest.GetCurrentlyPlayingAsync(spotify);
			var queueResponse = await _spotifyRequest.GetQueueAsync(spotify);

			if (currentlyPlaying is null || queueResponse is null || queueResponse.Queue is null)
				return null;

			// Handle both tracks and episodes as the currently playing item
			Track? currentItem = currentlyPlaying.Item switch
			{
				FullTrack track => await BuildTrackAsync(track),
				FullEpisode episode => BuildEpisode(episode),
				_ => null
			};

			if (currentItem == null) return null;

			var queue = queueResponse.Queue;

            var newQueue = new MusicQueue();

			newQueue.Tracks.Add(currentItem);

			// Process queue items — can be a mix of tracks and episodes
			foreach (var item in queue.Take(9))
			{
				switch (item)
				{
					case FullTrack track:
						newQueue.EnqueueTrack(await BuildTrackAsync(track));
						break;
					case FullEpisode episode:
						newQueue.EnqueueTrack(BuildEpisode(episode));
						break;
				}
			}
			_cache.Set(cacheKey, newQueue, TimeSpan.FromMinutes(11));
			newQueue.CachedAt = DateTime.UtcNow;
			_logger.LogInformation("Queue for user {UserId} built with {ItemCount} items ({TrackCount} tracks, {EpisodeCount} episodes).", 
				userId, newQueue.Tracks.Count, 
				newQueue.Tracks.Count(t => !t.IsEpisode), 
				newQueue.Tracks.Count(t => t.IsEpisode));
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

		/// <summary>
		/// Builds a Track object from a FullEpisode. Episodes don't have lyrics so this is synchronous.
		/// </summary>
		private static Track BuildEpisode(FullEpisode episode)
		{
			return new Track
			{
				FullEpisode = episode,
				ResumePositionMs = episode.ResumePoint?.ResumePositionMs,
				FullyPlayed = episode.ResumePoint?.FullyPlayed,
				Lyrics = null,
				IsFlagged = false
			};
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

			// Handle both tracks and episodes for validation
			string? currentItemId = currentPlaybackResponse?.Item switch
			{
				FullTrack track => track.Id,
				FullEpisode episode => episode.Id,
				_ => null
			};

			if (currentItemId == null) return false;

			var isValid = queue.MatchesCurrentPlayback(currentItemId);
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
		public async Task<Track?> GetTrackFromQueueAsync(string userId, string itemId, SpotifyClient spotify)
		{
			var queue = await GetQueueAsync(userId, spotify);
			return queue?.Tracks.FirstOrDefault(t => t.ItemId == itemId);
		}
        public async Task<Track?> SkipFlaggedSongsAsync(string userId, SpotifyClient spotify)
        {
            await _queueSemaphore.WaitAsync();
            try
            {
                var queue = await GetQueueAsync(userId, spotify);
                if (queue?.CurrentTrack == null)
                {
                    _logger.LogDebug("Queue is empty or no current track is playing for user {UserId}.", userId);
                    return null;
                }

                // Skip content filtering for episodes — they don't have the same explicit flag semantics
                if (queue.CurrentTrack.IsEpisode)
                {
                    return queue.CurrentTrack;
                }

                if (queue.CurrentTrack.IsFlagged || queue.CurrentTrack.FullTrack?.Explicit == true)
                {
                    _logger.LogInformation("Skipping flagged track: {TrackName}", queue.CurrentTrack.FullTrack?.Name ?? "Unknown");
                    await spotify.Player.SkipNext();

                    queue.DequeueTrack();
                    var cacheKey = $"queue_{userId}";
                    _cache.Set(cacheKey, queue, TimeSpan.FromMinutes(5));
                }
                return queue.CurrentTrack;
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }
		public MusicQueue SyncWithCurrentPlayback(string userId, MusicQueue? existingQueue, Track currentTrack, List<Track> queueTracks)
		{
            if (existingQueue == null)
            {
                existingQueue = new MusicQueue { Tracks = [currentTrack], CurrentIndex = 0, CachedAt = DateTime.UtcNow };
                existingQueue.Tracks.AddRange(queueTracks);
                return existingQueue;
            }

            // Use unified ItemId for comparison (works for both tracks and episodes)
            if (existingQueue.CurrentTrack?.ItemId == currentTrack.ItemId)
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
                if (existingQueue.Tracks[i].ItemId == currentTrack.ItemId)
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
