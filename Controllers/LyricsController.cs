using Alify.Extensions;
using Alify.Models;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;

namespace Alify.Controllers
{
    /// <summary>
    /// Enhanced Lyrics API Controller demonstrating Method Injection patterns.
    /// 
    /// Architecture Benefits:
    /// 1. Constructor injection for core, always-needed dependencies
    /// 2. Method injection for operation-specific services
    /// 3. Reduced memory footprint - services resolved on-demand
    /// 4. Better testability - each method can be tested with specific service mocks
    /// 5. Improved performance - services only created when actually used
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class LyricsController : ControllerBase
    {
        // CORE DEPENDENCIES: Always needed, injected via constructor
        private readonly SpotifyService _spotifyService;
        private readonly ILogger<LyricsController> _logger;

        /// <summary>
        /// Constructor only includes core dependencies used across multiple actions.
        /// Benefits: Reduced constructor complexity, cleaner dependency management.
        /// </summary>
        public LyricsController(SpotifyService spotifyService, ILogger<LyricsController> logger)
        {
            _spotifyService = spotifyService;
            _logger = logger;
        }

        /// <summary>
        /// Get lyrics for currently playing track using method injection.
        /// Benefits: LyricService only resolved when actually fetching lyrics.
        /// </summary>
        [HttpGet("current")]
        [OutputCache(Duration = 30)] // Cache for 30 seconds for performance
        public async Task<ActionResult<object>> GetCurrentTrackLyrics()
        {
            // Check authentication first (using core service)
            if (!_spotifyService.IsAuthenticated())
            {
                _logger.LogWarning("Lyrics API accessed without authentication");
                return Unauthorized(new { error = "Authentication required" });
            }

            try
            {
                // METHOD INJECTION: Get Spotify client and lyrics service only when needed
                return await this.WithServiceAsync<LyricService, ActionResult<object>>(async lyricService =>
                {
                    var spotifyClient = await _spotifyService.GetSpotifyClientAsync();
                    if (spotifyClient == null)
                    {
                        return BadRequest(new { error = "Spotify client unavailable" });
                    }

                    var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotifyClient);
                    if (playbackInfo?.CurrentlyPlaying?.FullTrack == null)
                    {
                        return NotFound(new { message = "No track currently playing" });
                    }

                    var track = playbackInfo.CurrentlyPlaying.FullTrack;
                    var artistName = track.Artists.FirstOrDefault()?.Name ?? "Unknown";

                    // Fetch lyrics using method-injected service
                    var lyrics = await lyricService.GetLyricsAsync(artistName, track.Name);
                    
                    _logger.LogInformation("Lyrics requested for: {Artist} - {Track}", artistName, track.Name);

                    return Ok(new
                    {
                        track = new
                        {
                            name = track.Name,
                            artist = artistName,
                            album = track.Album.Name
                        },
                        lyrics = lyrics ?? "No lyrics found",
                        hasLyrics = !string.IsNullOrEmpty(lyrics)
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching current track lyrics");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Analyze lyrics structure using multiple method-injected services.
        /// Benefits: Services only resolved for this specific analysis operation.
        /// </summary>
        [HttpGet("analyze")]
        [OutputCache(Duration = 60)] // Longer cache for analysis results
        public async Task<ActionResult<object>> AnalyzeLyrics()
        {
            if (!_spotifyService.IsAuthenticated())
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            try
            {
                // METHOD INJECTION: Chain multiple services for complex analysis
                return await this.WithServiceAsync<LyricService, ActionResult<object>>(async lyricService =>
                {
                    var spotifyClient = await _spotifyService.GetSpotifyClientAsync();
                    if (spotifyClient == null)
                    {
                        return BadRequest(new { error = "Spotify client unavailable" });
                    }

                    var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotifyClient);
                    if (playbackInfo?.CurrentlyPlaying?.FullTrack == null)
                    {
                        return NotFound(new { message = "No track currently playing" });
                    }

                    var track = playbackInfo.CurrentlyPlaying.FullTrack;
                    var artistName = track.Artists.FirstOrDefault()?.Name ?? "Unknown";
                    var lyrics = await lyricService.GetLyricsAsync(artistName, track.Name);

                    if (string.IsNullOrEmpty(lyrics))
                    {
                        return NotFound(new { message = "No lyrics found for analysis" });
                    }

                    // METHOD INJECTION: Resolve ArtistLyricService for parsing and analysis
                    return await this.WithServiceAsync<ArtistLyricService, ActionResult<object>>(async artistLyricService =>
                    {
                        var mapping = artistLyricService.ParseLyricsWithArtistMapping(lyrics, artistName);
                        var artists = mapping.GetArtists();
                        var sections = mapping.GetSections();

                        // Build detailed analysis
                        var artistLineMapping = artistLyricService.GetArtistLineMapping(lyrics);
                        var sectionLineMapping = artistLyricService.GetSectionLineMapping(lyrics);

                        _logger.LogInformation("Lyrics analysis completed: {ArtistCount} artists, {SectionCount} sections",
                                             artists.Count, sections.Count);

                        await Task.CompletedTask; // Placeholder for any async analysis

                        return Ok(new
                        {
                            track = new
                            {
                                name = track.Name,
                                artist = artistName
                            },
                            analysis = new
                            {
                                totalLines = mapping.Lines.Count,
                                artists = artists,
                                sections = sections,
                                artistDistribution = artistLineMapping.ToDictionary(
                                    kvp => kvp.Key, 
                                    kvp => new { lineCount = kvp.Value.Count, lines = kvp.Value }
                                ),
                                sectionDistribution = sectionLineMapping.ToDictionary(
                                    kvp => kvp.Key, 
                                    kvp => new { lineCount = kvp.Value.Count, lines = kvp.Value }
                                )
                            }
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing lyrics");
                return StatusCode(500, new { error = "Lyrics analysis failed" });
            }
        }

        /// <summary>
        /// Get lyrics with moderation using conditional method injection.
        /// Benefits: Moderation service only resolved when actually needed.
        /// </summary>
        [HttpGet("moderated")]
        [OutputCache(Duration = 30)]
        public async Task<ActionResult<object>> GetModeratedLyrics([FromQuery] bool includeModerationDetails = false)
        {
            if (!_spotifyService.IsAuthenticated())
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            try
            {
                return await this.WithServiceAsync<LyricService, ActionResult<object>>(async lyricService =>
                {
                    var spotifyClient = await _spotifyService.GetSpotifyClientAsync();
                    if (spotifyClient == null)
                    {
                        return BadRequest(new { error = "Spotify client unavailable" });
                    }

                    var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotifyClient);
                    if (playbackInfo?.CurrentlyPlaying?.FullTrack == null)
                    {
                        return NotFound(new { message = "No track currently playing" });
                    }

                    var track = playbackInfo.CurrentlyPlaying.FullTrack;
                    var artistName = track.Artists.FirstOrDefault()?.Name ?? "Unknown";
                    var lyrics = await lyricService.GetLyricsAsync(artistName, track.Name);

                    if (string.IsNullOrEmpty(lyrics))
                    {
                        return NotFound(new { message = "No lyrics found" });
                    }

                    // CONDITIONAL METHOD INJECTION: Only moderate lyrics if requested
                    LyricsModerationResult? moderationResult = null;
                    if (includeModerationDetails)
                    {
                        moderationResult = await lyricService.ModerateLyricsAsync(lyrics);
                        _logger.LogInformation("Lyrics moderation performed for: {Artist} - {Track}", artistName, track.Name);
                    }

                    // Create response with consistent object structure
                    object response;
                    if (includeModerationDetails)
                    {
                        response = new
                        {
                            track = new
                            {
                                name = track.Name,
                                artist = artistName,
                                isExplicit = track.Explicit
                            },
                            lyrics = lyrics,
                            moderation = new
                            {
                                wasChecked = true,
                                suitableForKids = moderationResult?.suitable_for_kids ?? true,
                                flagged = new
                                {
                                    violence = moderationResult?.violence ?? false,
                                    hate = moderationResult?.hate ?? false,
                                    sexual = moderationResult?.sexual ?? false,
                                    profanity = moderationResult?.profanity ?? false
                                }
                            }
                        };
                    }
                    else
                    {
                        response = new
                        {
                            track = new
                            {
                                name = track.Name,
                                artist = artistName,
                                isExplicit = track.Explicit
                            },
                            lyrics = lyrics,
                            moderation = new
                            {
                                wasChecked = false
                            }
                        };
                    }

                    return Ok(response);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting moderated lyrics");
                return StatusCode(500, new { error = "Failed to get moderated lyrics" });
            }
        }

        /// <summary>
        /// Clear lyrics cache using method injection for cache services.
        /// Benefits: Cache service only resolved when actually clearing cache.
        /// </summary>
        [HttpPost("cache/clear")]
        public ActionResult ClearLyricsCache()
        {
            if (!_spotifyService.IsAuthenticated())
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            try
            {
                // METHOD INJECTION: Resolve cache service only for cache operations
                this.WithService<IMemoryCache>(cache =>
                {
                    // Clear lyrics-related cache entries
                    var cacheKeys = new[]
                    {
                        "CurrentlyPlaying",
                        "Queue"
                    };

                    foreach (var key in cacheKeys)
                    {
                        cache.Remove(key);
                    }

                    _logger.LogInformation("Lyrics cache cleared");
                });

                // Also clear Spotify request cache
                this.WithService<SpotifyRequestCache>(requestCache =>
                {
                    requestCache.ClearCache();
                });

                return Ok(new { message = "Lyrics cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing lyrics cache");
                return StatusCode(500, new { error = "Failed to clear cache" });
            }
        }

        /// <summary>
        /// Get queue with lyrics preview using performance-optimized method injection.
        /// Benefits: Services resolved on-demand, better memory management for bulk operations.
        /// </summary>
        [HttpGet("queue")]
        [OutputCache(Duration = 15)] // Shorter cache for dynamic queue data
        public async Task<ActionResult<object>> GetQueueWithLyrics([FromQuery] int limit = 5)
        {
            if (!_spotifyService.IsAuthenticated())
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            if (limit <= 0 || limit > 20)
            {
                return BadRequest(new { error = "Limit must be between 1 and 20" });
            }

            try
            {
                return await this.WithServiceAsync<LyricService, ActionResult<object>>(async lyricService =>
                {
                    var spotifyClient = await _spotifyService.GetSpotifyClientAsync();
                    if (spotifyClient == null)
                    {
                        return BadRequest(new { error = "Spotify client unavailable" });
                    }

                    var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotifyClient);
                    if (playbackInfo?.Queue == null || !playbackInfo.Queue.Any())
                    {
                        return Ok(new { queue = new object[0], message = "Queue is empty" });
                    }

                    // Process queue items with lyrics preview (performance optimized)
                    var queueWithLyrics = new List<object>();
                    var queueItems = playbackInfo.Queue.Take(limit);

                    foreach (var queueTrack in queueItems)
                    {
                        var track = queueTrack.FullTrack;
                        var artistName = track.Artists.FirstOrDefault()?.Name ?? "Unknown";
                        
                        // Get lyrics preview (first 200 characters for performance)
                        var fullLyrics = await lyricService.GetLyricsAsync(artistName, track.Name);
                        var lyricsPreview = string.IsNullOrEmpty(fullLyrics) 
                            ? "No lyrics available"
                            : (fullLyrics.Length > 200 ? fullLyrics.Substring(0, 200) + "..." : fullLyrics);

                        queueWithLyrics.Add(new
                        {
                            track = new
                            {
                                name = track.Name,
                                artist = artistName,
                                isExplicit = track.Explicit
                            },
                            lyricsPreview = lyricsPreview,
                            hasFullLyrics = !string.IsNullOrEmpty(fullLyrics)
                        });
                    }

                    _logger.LogInformation("Queue with lyrics retrieved: {Count} tracks", queueWithLyrics.Count);

                    return Ok(new
                    {
                        queue = queueWithLyrics,
                        totalInQueue = playbackInfo.Queue.Count,
                        showing = queueWithLyrics.Count
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queue with lyrics");
                return StatusCode(500, new { error = "Failed to get queue with lyrics" });
            }
        }
    }
}