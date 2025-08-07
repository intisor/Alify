using Alify.Extensions;
using Alify.Models;
using Alify.Services;

namespace Alify.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LyricsController : ControllerBase
    {
        private readonly SpotifyService _spotifyService;
        private readonly ILogger<LyricsController> _logger;

        public LyricsController(SpotifyService spotifyService, ILogger<LyricsController> logger)
        {
            _spotifyService = spotifyService;
            _logger = logger;
        }

        [HttpGet("current")]
        [OutputCache(Duration = 30)]
        public async Task<ActionResult<object>> GetCurrentTrackLyrics()
        {
            if (!_spotifyService.IsAuthenticated())
            {
                _logger.LogUnauthenticatedLyricsAccess();
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
                    
                    return Ok(new
                    {
                        track = new
                        {
                            name = track.Name,
                            artist = artistName,
                            album = track.Album.Name
                        },
                        lyrics = lyrics ?? "Lyrics not found",
                        hasLyrics = !string.IsNullOrEmpty(lyrics)
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogCurrentTrackLyricsError(ex);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("analyze")]
        [OutputCache(Duration = 60)]
        public async Task<ActionResult<object>> AnalyzeLyrics()
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
                        return NotFound(new { message = "No lyrics found for analysis" });
                    }

                    return await this.WithServiceAsync<ArtistLyricService, ActionResult<object>>(async artistLyricService =>
                    {
                        var mapping = artistLyricService.ParseLyricsWithArtistMapping(lyrics, artistName);
                        var artists = mapping.GetArtists();
                        var sections = mapping.GetSections();
                        var artistLineMapping = artistLyricService.GetArtistLineMapping(lyrics);
                        var sectionLineMapping = artistLyricService.GetSectionLineMapping(lyrics);

                        _logger.LogLyricsAnalysisCompleted(artists.Count, sections.Count);

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

                    LyricsModerationResult? moderationResult = null;
                    if (includeModerationDetails)
                    {
                        moderationResult = await lyricService.ModerateLyricsAsync(lyrics);
                        _logger.LogLyricsModerationPerformed(artistName, track.Name);
                    }

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

        [HttpPost("cache/clear")]
        public ActionResult ClearLyricsCache()
        {
            if (!_spotifyService.IsAuthenticated())
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            try
            {
                this.WithService<IMemoryCache>(cache =>
                {
                    var cacheKeys = new[] { "CurrentlyPlaying", "Queue" };
                    foreach (var key in cacheKeys)
                    {
                        cache.Remove(key);
                    }
                    _logger.LogInformation("Lyrics cache cleared");
                });

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

        [HttpGet("queue")]
        [OutputCache(Duration = 15)]
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

                    var queueWithLyrics = new List<object>();
                    var queueItems = playbackInfo.Queue.Take(limit);

                    foreach (var queueTrack in queueItems)
                    {
                        var track = queueTrack.FullTrack;
                        var artistName = track.Artists.FirstOrDefault()?.Name ?? "Unknown";
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

                    _logger.LogQueueWithLyricsRetrieved(queueWithLyrics.Count);

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