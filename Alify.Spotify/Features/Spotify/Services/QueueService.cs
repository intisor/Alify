using Alify.Core.Features.Analysis.Engine;
using Alify.Core.Features.Analysis.Engine.Gemini;
using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Analysis.Policy;
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
		private readonly FreeLyricsProviderService? _freeLyricsProvider;
		private readonly CanonicalLyricNormalizer _normalizer = new();
		private readonly HeuristicStanceEngine _heuristicEngine = new();
		private readonly GeminiStanceAnalysisEngine? _geminiEngine;
		private readonly ILogger<QueueService> _logger;
        private readonly SemaphoreSlim _queueSemaphore = new(1, 1);

        public QueueService(
            IMemoryCache cache, 
            SpotifyRequestCache spotifyRequest, 
            ArtistLyricService artistLyricService, 
            ILogger<QueueService> logger,
            FreeLyricsProviderService? freeLyricsProvider = null,
            GeminiStanceAnalysisEngine? geminiEngine = null)
		{
			_cache = cache;
			_spotifyRequest = spotifyRequest;
			_artistLyricService = artistLyricService;
			_logger = logger;
            _freeLyricsProvider = freeLyricsProvider;
            _geminiEngine = geminiEngine;
		}
		public async Task<MusicQueue?> GetQueueAsync(string userId, SpotifyClient spotify, FullTrack? knownCurrentTrack = null)
		{
			_cache.Set("SpotifyUserId", userId);
			var cacheKey = $"queue_{userId}";

			if (_cache.TryGetValue(cacheKey, out MusicQueue? cachedQueue) && cachedQueue != null && !cachedQueue.IsExpired)
				return cachedQueue;

			FullTrack? currentTrack = knownCurrentTrack;
			if (currentTrack == null)
			{
				var currentlyPlaying = await _spotifyRequest.GetCurrentlyPlayingAsync(spotify, userId);
				currentTrack = currentlyPlaying?.Item as FullTrack;
			}

			if (currentTrack == null)
			{
				return null;
			}

			var queueResponse = await _spotifyRequest.GetQueueAsync(spotify, userId);
			if (queueResponse?.Queue is null)
			{
				return null;
			}

			var userPrefs = GetUserFilterPreferences(userId);
			var queue = queueResponse.Queue;
			var newQueue = new MusicQueue();
			newQueue.Tracks.Add(BuildTrack(currentTrack, userPrefs));

			foreach (var item in queue.Take(9))
			{
				if (item is FullTrack track)
				{
					newQueue.EnqueueTrack(BuildTrack(track, userPrefs));
				}
			}

			// 1. Enrich currently playing track with lyrics and policy moderation immediately
			if (newQueue.CurrentTrack != null && _freeLyricsProvider != null)
			{
				await EnrichTrackWithLyricsAndPolicyAsync(newQueue.CurrentTrack, userId, userPrefs);
			}

			// 2. Pre-enrich the top upcoming queued tracks
			foreach (var qTrack in newQueue.Tracks.Skip(1).Take(4))
			{
				if (qTrack.FullTrack != null && !qTrack.IsFlagged)
				{
					await EnrichTrackWithLyricsAndPolicyAsync(qTrack, userId, userPrefs);
				}
				else
				{
					EnrichFromCache(qTrack, userId, userPrefs);
				}
			}

			foreach (var qTrack in newQueue.Tracks.Skip(5))
			{
				EnrichFromCache(qTrack, userId, userPrefs);
			}

			_cache.Set(cacheKey, newQueue, TimeSpan.FromSeconds(30));
			newQueue.CachedAt = DateTime.UtcNow;
			_logger.LogInformation("Queue for user {UserId} built with {TrackCount} tracks.", userId, newQueue.Tracks.Count);
			return newQueue;
		}

		private static Track BuildTrack(FullTrack track, UserFilterPreferences? prefs = null)
		{
			var policyPrefs = prefs?.ToPolicyPreferences();
			bool explicitOptIn = policyPrefs?.ExplicitFilterOptIn ?? prefs?.FilterSpotifyExplicit ?? true;
			bool isExplicitFlagged = explicitOptIn && track.Explicit;
			return new Track
			{
				FullTrack = track,
				IsFlagged = isExplicitFlagged,
				FlagReason = isExplicitFlagged ? (prefs != null && prefs.Profile != FilterProfile.General ? $"Explicit Content ({prefs.Profile} Ethos)" : "Spotify Explicit Tag [E]") : null,
				Lyrics = null,
				MappedLyrics = null
			};
		}

		public async Task EnrichTrackWithLyricsAndPolicyAsync(Track trackObj, string userId, UserFilterPreferences userPrefs)
		{
			if (trackObj.FullTrack == null || _freeLyricsProvider == null) return;

			var artist = trackObj.FullTrack.Artists?.FirstOrDefault()?.Name;
			var trackName = trackObj.FullTrack.Name;
			var trackId = trackObj.FullTrack.Id ?? $"{artist}_{trackName}";
			if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(trackName)) return;

			var lyricCacheKey = $"lyrics_{artist}_{trackName}".ToLowerInvariant();
			if (!_cache.TryGetValue(lyricCacheKey, out string? lyrics))
			{
				lyrics = await _freeLyricsProvider.GetLyricsAsync(artist, trackName);
				_cache.Set(lyricCacheKey, lyrics ?? string.Empty, TimeSpan.FromHours(12));
			}

			trackObj.Lyrics = lyrics;

			if (trackObj.IsFlagged && string.IsNullOrEmpty(trackObj.FlagReason))
			{
				trackObj.FlagReason = userPrefs.Profile != FilterProfile.General ? $"Explicit Content ({userPrefs.Profile} Ethos)" : "Spotify Explicit Tag [E]";
			}

			int policyVersion = _cache.TryGetValue($"policy_version_{userId}", out int pv) ? pv : 0;
			var stanceCacheKey = $"stance_{userId}_v{policyVersion}_{userPrefs.Profile}_{userPrefs.StrictnessLevel}_{trackId}".ToLowerInvariant();
			if (_cache.TryGetValue(stanceCacheKey, out StanceEvaluationResult? cachedResult) && cachedResult != null)
			{
				if (cachedResult.IsFlagged)
				{
					trackObj.IsFlagged = true;
					trackObj.FlagReason = cachedResult.SkipReason;
				}
				return;
			}

			if (!string.IsNullOrWhiteSpace(lyrics))
			{
				var policyPrefs = userPrefs.ToPolicyPreferences();
				var sourceVersion = _normalizer.Normalize(lyrics, sourceId: trackObj.FullTrack.Id);
				var analysis = await _heuristicEngine.AnalyzeAsync(sourceVersion);

				var policyEval = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(analysis, policyPrefs);
				if (policyEval.Decision == PolicyDecision.RecommendSkip)
				{
					trackObj.IsFlagged = true;
					var summary = string.Join("; ", policyEval.ReasonCodes);
					trackObj.FlagReason = $"Lyrics Violation: {summary}";
					_cache.Set(stanceCacheKey, new StanceEvaluationResult
					{
						IsFlagged = true,
						SkipReason = trackObj.FlagReason,
						Lyrics = lyrics
					}, TimeSpan.FromHours(1));
				}
				else if (userPrefs.EnableGeminiLLM && _geminiEngine != null)
				{
					try
					{
						var geminiAnalysis = await _geminiEngine.AnalyzeAsync(
							sourceVersion,
							targetLanguage: "en",
							profile: userPrefs.Profile,
							strictnessLevel: userPrefs.StrictnessLevel);

						if (geminiAnalysis.Status == AnalysisStatus.Completed)
						{
							var geminiPolicyEval = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(geminiAnalysis, policyPrefs);
							if (geminiPolicyEval.Decision == PolicyDecision.RecommendSkip)
							{
								trackObj.IsFlagged = true;
								var geminiSummary = string.Join("; ", geminiPolicyEval.ReasonCodes);
								trackObj.FlagReason = $"{userPrefs.Profile} Ethos Violation ({geminiSummary})";
								_logger.LogInformation("Track '{Track}' by '{Artist}' flagged by Gemini 2.5 Flash for profile {Profile}: {Summary}", trackName, artist, userPrefs.Profile, geminiSummary);

								_cache.Set(stanceCacheKey, new StanceEvaluationResult
								{
									IsFlagged = true,
									SkipReason = trackObj.FlagReason,
									Lyrics = lyrics
								}, TimeSpan.FromHours(1));
								return;
							}
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Gemini 2.5 Flash analysis error during queue enrichment for track {TrackName}", trackName);
					}

					_cache.Set(stanceCacheKey, new StanceEvaluationResult
					{
						IsFlagged = false,
						SkipReason = string.Empty,
						Lyrics = lyrics
					}, TimeSpan.FromHours(1));
				}
				else
				{
					_cache.Set(stanceCacheKey, new StanceEvaluationResult
					{
						IsFlagged = false,
						SkipReason = string.Empty,
						Lyrics = lyrics
					}, TimeSpan.FromHours(1));
				}
			}
		}

		private void EnrichFromCache(Track trackObj, string userId, UserFilterPreferences userPrefs)
		{
			if (trackObj.FullTrack == null) return;
			var artist = trackObj.FullTrack.Artists?.FirstOrDefault()?.Name;
			var trackName = trackObj.FullTrack.Name;
			var trackId = trackObj.FullTrack.Id ?? $"{artist}_{trackName}";
			if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(trackName)) return;

			var lyricCacheKey = $"lyrics_{artist}_{trackName}".ToLowerInvariant();
			if (_cache.TryGetValue(lyricCacheKey, out string? cachedLyrics) && !string.IsNullOrWhiteSpace(cachedLyrics))
			{
				trackObj.Lyrics = cachedLyrics;
			}

			int policyVersion = _cache.TryGetValue($"policy_version_{userId}", out int pv) ? pv : 0;
			var stanceCacheKey = $"stance_{userId}_v{policyVersion}_{userPrefs.Profile}_{userPrefs.StrictnessLevel}_{trackId}".ToLowerInvariant();
			if (_cache.TryGetValue(stanceCacheKey, out StanceEvaluationResult? cachedResult) && cachedResult != null)
			{
				trackObj.Lyrics ??= cachedResult.Lyrics;
				if (cachedResult.IsFlagged)
				{
					trackObj.IsFlagged = true;
					trackObj.FlagReason = cachedResult.SkipReason;
				}
			}
		}

		public UserFilterPreferences GetUserFilterPreferences(string userId)
		{
			var prefsKey = $"policy_prefs_{userId}";
			if (_cache.TryGetValue(prefsKey, out UserFilterPreferences? prefs) && prefs != null)
			{
				return prefs;
			}
			return new UserFilterPreferences();
		}

		public void SaveUserFilterPreferences(string userId, UserFilterPreferences preferences)
		{
			ArgumentNullException.ThrowIfNull(preferences);
			var prefsKey = $"policy_prefs_{userId}";
			_cache.Set(prefsKey, preferences, TimeSpan.FromDays(30));

			var versionKey = $"policy_version_{userId}";
			int currentVersion = _cache.TryGetValue(versionKey, out int v) ? v + 1 : 1;
			_cache.Set(versionKey, currentVersion, TimeSpan.FromDays(30));

			InvalidateQueue(userId);
			_logger.LogInformation("Saved filter preferences for user {UserId} (Policy v{Version}): Profile={Profile}, Strictness={Strictness}, Explicit={Explicit}",
				userId, currentVersion, preferences.Profile, preferences.StrictnessLevel, preferences.FilterSpotifyExplicit);
		}

		/// <summary>
		/// Validates the cached queue against a known current track ID purely in memory with zero HTTP calls.
		/// </summary>
		public bool ValidateQueue(string userId, string currentTrackId)
		{
			var cacheKey = $"queue_{userId}";
			if (_cache.TryGetValue(cacheKey, out MusicQueue? cachedQueue) && cachedQueue != null && !cachedQueue.IsExpired)
			{
				var isValid = cachedQueue.MatchesCurrentPlayback(currentTrackId);
				if (!isValid)
				{
					_logger.LogInformation("Queue out of sync with track {TrackId} - invalidating cache", currentTrackId);
					InvalidateQueue(userId);
				}
				return isValid;
			}
			return false;
		}

		public async Task<bool> ValidateQueueAsync(string userId, SpotifyClient spotify, string? knownCurrentTrackId = null)
		{
			if (!string.IsNullOrEmpty(knownCurrentTrackId))
			{
				return ValidateQueue(userId, knownCurrentTrackId);
			}

			var cacheKey = $"queue_{userId}";
			if (!_cache.TryGetValue(cacheKey, out MusicQueue? queue) || queue == null || queue.IsEmpty || queue.IsExpired)
			{
				queue = await GetQueueAsync(userId, spotify);
				if (queue == null || queue.IsEmpty)
				{
					_logger.LogWarning("Queue for user {UserId} is empty or null.", userId);
					return false;
				}
			}

			var currentPlaybackResponse = await _spotifyRequest.GetCurrentlyPlayingAsync(spotify, userId);
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

		public async Task<Track?> GetTrackFromQueueAsync(string userId, string trackId, SpotifyClient spotify)
		{
			var queue = await GetQueueAsync(userId, spotify);
			return queue?.Tracks.FirstOrDefault(t => t.FullTrack?.Id == trackId);
		}

        public async Task<SkipActionResult> SkipFlaggedSongsAsync(string userId, SpotifyClient spotify)
        {
            await _queueSemaphore.WaitAsync();
            try
            {
                var queue = await GetQueueAsync(userId, spotify);
                if (queue?.CurrentTrack == null)
                {
                    _logger.LogDebug("Queue is empty or no current track is playing for user {UserId}.", userId);
                    return SkipActionResult.NoAction(null, "Queue is empty or no current track");
                }

                var userPrefs = GetUserFilterPreferences(userId);
                var policyPrefs = userPrefs.ToPolicyPreferences();

                var currentTarget = queue.CurrentTrack;
                string skipReason = string.Empty;
                bool isFlagged = currentTarget.IsFlagged;

                // 1. Check official Spotify explicit metadata flag using deterministic policy evaluator
                if (!isFlagged && currentTarget.FullTrack != null)
                {
                    var explicitEval = DeterministicPolicyEvaluator.EvaluateSpotifyExplicit(currentTarget.FullTrack.Explicit, policyPrefs);
                    if (explicitEval.Decision == PolicyDecision.RecommendSkip)
                    {
                        isFlagged = true;
                        currentTarget.IsFlagged = true;
                        skipReason = "Official Spotify explicit metadata";
                    }
                }

                // 2. Automated open lyrics retrieval and heuristic stance evaluation with user policy evaluation
                if (!isFlagged && _freeLyricsProvider != null && currentTarget.FullTrack != null)
                {
                    var artist = currentTarget.FullTrack.Artists?.FirstOrDefault()?.Name;
                    var trackName = currentTarget.FullTrack.Name;
                    var trackId = currentTarget.FullTrack.Id ?? $"{artist}_{trackName}";

                    if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(trackName))
                    {
                        var stanceCacheKey = $"stance_eval_{userId}_{userPrefs.Profile}_{userPrefs.StrictnessLevel}_{trackId}".ToLowerInvariant();
                        if (_cache.TryGetValue(stanceCacheKey, out StanceEvaluationResult? cachedResult) && cachedResult != null)
                        {
                            currentTarget.Lyrics = cachedResult.Lyrics;
                            if (cachedResult.IsFlagged)
                            {
                                isFlagged = true;
                                currentTarget.IsFlagged = true;
                                skipReason = cachedResult.SkipReason;
                            }
                        }
                        else
                        {
                            var lyricCacheKey = $"lyrics_{artist}_{trackName}".ToLowerInvariant();
                            if (!_cache.TryGetValue(lyricCacheKey, out string? lyrics))
                            {
                                lyrics = await _freeLyricsProvider.GetLyricsAsync(artist, trackName);
                                _cache.Set(lyricCacheKey, lyrics ?? string.Empty, TimeSpan.FromHours(12));
                            }

                            currentTarget.Lyrics = lyrics;
                            bool evaluatedIsFlagged = false;
                            string evaluatedSkipReason = string.Empty;

                            if (!string.IsNullOrWhiteSpace(lyrics))
                            {
                                var sourceVersion = _normalizer.Normalize(lyrics, sourceId: currentTarget.FullTrack.Id);
                                var analysis = await _heuristicEngine.AnalyzeAsync(sourceVersion);

                                var policyEval = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(analysis, policyPrefs);
                                if (policyEval.Decision == PolicyDecision.RecommendSkip)
                                {
                                    evaluatedIsFlagged = true;
                                    isFlagged = true;
                                    currentTarget.IsFlagged = true;
                                    var summary = string.Join("; ", policyEval.ReasonCodes);
                                    evaluatedSkipReason = $"Lyrics content policy violation: {summary}";
                                    skipReason = evaluatedSkipReason;
                                    _logger.LogInformation("Track '{Track}' by '{Artist}' flagged by heuristic policy evaluator: {Summary}", trackName, artist, summary);
                                }
                                else if (userPrefs.EnableGeminiLLM && _geminiEngine != null)
                                {
                                    try
                                    {
                                        var geminiAnalysis = await _geminiEngine.AnalyzeAsync(
                                            sourceVersion,
                                            targetLanguage: "en",
                                            profile: userPrefs.Profile,
                                            strictnessLevel: userPrefs.StrictnessLevel);

                                        if (geminiAnalysis.Status == AnalysisStatus.Completed)
                                        {
                                            var geminiPolicyEval = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(geminiAnalysis, policyPrefs);
                                            if (geminiPolicyEval.Decision == PolicyDecision.RecommendSkip)
                                            {
                                                evaluatedIsFlagged = true;
                                                isFlagged = true;
                                                currentTarget.IsFlagged = true;
                                                var summary = string.Join("; ", geminiPolicyEval.ReasonCodes);
                                                evaluatedSkipReason = $"Gemini 2.5 LLM [{userPrefs.Profile} Strictness {userPrefs.StrictnessLevel}/10] violation: {summary}";
                                                skipReason = evaluatedSkipReason;
                                                _logger.LogInformation("Track '{Track}' by '{Artist}' flagged by Gemini 2.5 LLM ({Profile}, Strictness {Strictness}/10): {Summary}",
                                                    trackName, artist, userPrefs.Profile, userPrefs.StrictnessLevel, summary);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Gemini 2.5 LLM analysis encountered an error for track {TrackName}. Relying on heuristic outcome.", trackName);
                                    }
                                }
                            }

                            _cache.Set(stanceCacheKey, new StanceEvaluationResult
                            {
                                IsFlagged = evaluatedIsFlagged,
                                SkipReason = evaluatedSkipReason,
                                Lyrics = lyrics
                            }, TimeSpan.FromHours(12));
                        }
                    }
                }

                if (isFlagged)
                {
                    _logger.LogInformation("Skipping flagged track: {TrackName} ({Reason})", currentTarget.FullTrack?.Name ?? "Unknown", skipReason);
                    try
                    {
                        await spotify.Player.SkipNext();
                    }
                    catch (APIException apiEx)
                    {
                        _logger.LogError(apiEx, "Spotify API error when skipping track {TrackName}: {StatusCode}", currentTarget.FullTrack?.Name, apiEx.Response?.StatusCode);
                        return SkipActionResult.Failed(currentTarget, $"Spotify API error {(int?)apiEx.Response?.StatusCode}: {apiEx.Message}");
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TimeoutException or OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Network timeout or lost response during skip for {TrackName}. Marking OutcomeUnknown.", currentTarget.FullTrack?.Name);
                        InvalidateQueue(userId);
                        return SkipActionResult.OutcomeUnknown(currentTarget, $"Network timeout or response lost: {ex.Message}");
                    }

                    queue.DequeueTrack();
                    var cacheKey = $"queue_{userId}";
                    _cache.Set(cacheKey, queue, TimeSpan.FromMinutes(5));

                    return SkipActionResult.Success(currentTarget, queue.CurrentTrack, string.IsNullOrEmpty(skipReason) ? "Flagged content" : skipReason);
                }

                return SkipActionResult.NoAction(currentTarget);
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
                // Create a new queue if none exists
                existingQueue = new MusicQueue { Tracks = [currentTrack], CurrentIndex = 0, CachedAt = DateTime.UtcNow };
                existingQueue.Tracks.AddRange(queueTracks);
                return existingQueue;
            }

            if (existingQueue.CurrentTrack?.FullTrack?.Id == currentTrack.FullTrack?.Id)
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
                if (existingQueue.Tracks[i].FullTrack?.Id == currentTrack.FullTrack?.Id)
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

    public sealed record StanceEvaluationResult
    {
        public bool IsFlagged { get; init; }
        public string SkipReason { get; init; } = string.Empty;
        public string? Lyrics { get; init; }
    }
}
