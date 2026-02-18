using Alify.Features.Spotify.Models;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using SpotifyAPI.Web;

namespace Alify.Features.Spotify.Controllers;

/// <summary>
/// API controller for episode/podcast-specific operations.
/// Provides endpoints to list, manage, and queue episodes — features that the Spotify 
/// Community has repeatedly requested and that Spotify's native apps handle poorly.
/// </summary>
[ApiController]
[Route("api/episodes")]
public class EpisodeController : ControllerBase
{
    private readonly SpotifyService _spotifyService;
    private readonly ILogger<EpisodeController> _logger;

    public EpisodeController(SpotifyService spotifyService, ILogger<EpisodeController> logger)
    {
        _spotifyService = spotifyService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the user's saved/followed shows (podcasts).
    /// </summary>
    [HttpGet("shows")]
    public async Task<IActionResult> GetSavedShows()
    {
        var accessToken = HttpContext.Session.GetString("SpotifyAccessToken");
        var spotify = await _spotifyService.GetSpotifyClientAsync(accessToken);
        if (spotify == null) return Unauthorized("Not authenticated with Spotify.");

        try
        {
            var shows = await spotify.Library.GetShows(new LibraryShowsRequest { Limit = 50 });
            var result = shows.Items?.Select(s => new
            {
                id = s.Show.Id,
                name = s.Show.Name,
                publisher = s.Show.Publisher,
                description = s.Show.Description,
                imageUrl = s.Show.Images?.FirstOrDefault()?.Url,
                totalEpisodes = s.Show.TotalEpisodes
            });

            return Ok(result);
        }
        catch (APIException ex)
        {
            _logger.LogError(ex, "Error fetching saved shows.");
            return StatusCode(500, "Error fetching saved shows.");
        }
    }

    /// <summary>
    /// Gets episodes for a specific show, with resume progress and played status.
    /// This restores the "New Episodes" feed functionality that Spotify removed.
    /// </summary>
    [HttpGet("shows/{showId}/episodes")]
    public async Task<IActionResult> GetShowEpisodes(string showId, [FromQuery] int limit = 20, [FromQuery] int offset = 0)
    {
        var accessToken = HttpContext.Session.GetString("SpotifyAccessToken");
        var spotify = await _spotifyService.GetSpotifyClientAsync(accessToken);
        if (spotify == null) return Unauthorized("Not authenticated with Spotify.");

        try
        {
            var episodes = await spotify.Shows.GetEpisodes(showId, new ShowEpisodesRequest
            {
                Limit = limit,
                Offset = offset
            });

            var result = episodes.Items?.Select(ep => new
            {
                id = ep.Id,
                name = ep.Name,
                description = ep.Description,
                durationMs = ep.DurationMs,
                releaseDate = ep.ReleaseDate,
                uri = ep.Uri,
                imageUrl = ep.Images?.FirstOrDefault()?.Url,
                resumePositionMs = ep.ResumePoint?.ResumePositionMs,
                fullyPlayed = ep.ResumePoint?.FullyPlayed ?? false,
                isExplicit = ep.Explicit
            });

            return Ok(new
            {
                episodes = result,
                total = episodes.Total,
                offset = episodes.Offset
            });
        }
        catch (APIException ex)
        {
            _logger.LogError(ex, "Error fetching episodes for show {ShowId}.", showId);
            return StatusCode(500, "Error fetching episodes.");
        }
    }

    /// <summary>
    /// Gets a "new episodes" feed across all followed shows — unplayed or in-progress episodes
    /// sorted by release date. This restores the feature Spotify removed.
    /// </summary>
    [HttpGet("new")]
    public async Task<IActionResult> GetNewEpisodes([FromQuery] int limit = 20)
    {
        var accessToken = HttpContext.Session.GetString("SpotifyAccessToken");
        var spotify = await _spotifyService.GetSpotifyClientAsync(accessToken);
        if (spotify == null) return Unauthorized("Not authenticated with Spotify.");

        try
        {
            var shows = await spotify.Library.GetShows(new LibraryShowsRequest { Limit = 50 });
            if (shows?.Items == null || shows.Items.Count == 0)
            {
                return Ok(new { episodes = Array.Empty<object>(), total = 0 });
            }

            var allNewEpisodes = new List<object>();

            foreach (var savedShow in shows.Items)
            {
                try
                {
                    var episodes = await spotify.Shows.GetEpisodes(savedShow.Show.Id, new ShowEpisodesRequest
                    {
                        Limit = 5 // Latest 5 per show
                    });

                    if (episodes?.Items == null) continue;

                    var unplayed = episodes.Items
                        .Where(ep => ep.ResumePoint?.FullyPlayed != true)
                        .Select(ep => new
                        {
                            id = ep.Id,
                            name = ep.Name,
                            showName = savedShow.Show.Name,
                            showId = savedShow.Show.Id,
                            description = ep.Description,
                            durationMs = ep.DurationMs,
                            releaseDate = ep.ReleaseDate,
                            uri = ep.Uri,
                            imageUrl = ep.Images?.FirstOrDefault()?.Url ?? savedShow.Show.Images?.FirstOrDefault()?.Url,
                            resumePositionMs = ep.ResumePoint?.ResumePositionMs ?? 0,
                            fullyPlayed = false,
                            inProgress = (ep.ResumePoint?.ResumePositionMs ?? 0) > 0
                        });

                    allNewEpisodes.AddRange(unplayed);
                }
                catch (APIException ex)
                {
                    _logger.LogWarning(ex, "Skipping episodes fetch for show {ShowId}.", savedShow.Show.Id);
                }
            }

            // Sort by release date descending and take the requested limit
            var sorted = allNewEpisodes
                .OrderByDescending(e => ((dynamic)e).releaseDate)
                .Take(limit)
                .ToList();

            return Ok(new
            {
                episodes = sorted,
                total = sorted.Count
            });
        }
        catch (APIException ex)
        {
            _logger.LogError(ex, "Error fetching new episodes feed.");
            return StatusCode(500, "Error building new episodes feed.");
        }
    }

    /// <summary>
    /// Adds a specific episode to the playback queue.
    /// </summary>
    [HttpPost("queue")]
    public async Task<IActionResult> AddEpisodeToQueue([FromBody] AddToQueueRequest request)
    {
        var accessToken = HttpContext.Session.GetString("SpotifyAccessToken");
        var spotify = await _spotifyService.GetSpotifyClientAsync(accessToken);
        if (spotify == null) return Unauthorized("Not authenticated with Spotify.");

        if (string.IsNullOrEmpty(request?.Uri))
        {
            return BadRequest("Episode URI is required.");
        }

        var success = await _spotifyService.AddToQueueAsync(spotify, request.Uri);
        return success ? Ok(new { message = "Episode added to queue." }) : StatusCode(500, "Failed to add episode to queue.");
    }
}
