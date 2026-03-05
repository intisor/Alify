using Alify.Core.Infrastructure.FeatureFlags;
using Alify.Core.Models;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace Alify.Features.Lyrics.Controllers;

/// <summary>
/// API controller for Genius song search (Issue #2).
/// Thin wrapper around existing LyricService.SearchAsync() - no duplication!
/// </summary>
[FeatureGate(FeatureFlags.EnableGeniusSearch)]
[Route("api/lyrics/search")]
[ApiController]
public class LyricsSearchController : ControllerBase
{
    private readonly LyricService _lyricService;
    private readonly ILogger<LyricsSearchController> _logger;

    public LyricsSearchController(LyricService lyricService, ILogger<LyricsSearchController> logger)
    {
        _lyricService = lyricService;
        _logger = logger;
    }

    /// <summary>
    /// Search for songs by title and/or artist.
    /// Returns 404 if EnableGeniusSearch feature flag is disabled.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponse>> SearchSongs(
        [FromQuery] string? query,
        [FromQuery] string? artist = null,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = "Query parameter is required" });

        _logger.LogInformation("Searching: {Query} by {Artist}", query, artist ?? "any");

        var results = await _lyricService.SearchAsync(query, artist, perPage);
        
        return Ok(new SearchResponse
        {
            Results = results,
            TotalCount = results.Length,
            HasMore = results.Length == perPage,
            Page = page,
            PerPage = perPage
        });
    }
}
