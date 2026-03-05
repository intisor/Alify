namespace Alify.Core.Models;

/// <summary>
/// Represents a single song search result from the Genius API.
/// Used by the Genius search functionality (Issue #2) to display
/// search results before fetching full lyrics.
/// </summary>
public class SearchResult
{
    /// <summary>Genius track ID (used to fetch lyrics URL).</summary>
    public int GeniusId { get; set; }

    /// <summary>Song title (e.g., "Blinding Lights").</summary>
    public required string Title { get; set; }

    /// <summary>Primary artist name (e.g., "The Weeknd").</summary>
    public required string Artist { get; set; }

    /// <summary>Album name if available (e.g., "After Hours").</summary>
    public string? Album { get; set; }

    /// <summary>Release date display string (e.g., "March 20, 2020").</summary>
    public string? ReleaseDateDisplay { get; set; }

    /// <summary>Thumbnail image URL for album/song art (small, ~300px).</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Full Genius.com URL to the song page (for scraping lyrics).</summary>
    public required string GeniusUrl { get; set; }

    /// <summary>Full title with featured artists (e.g., "Song (feat. Artist)").</summary>
    public string? FullTitle { get; set; }
}

/// <summary>
/// Paginated search response wrapper for API controller responses.
/// Follows the spec from Issue #2 GitHub issue.
/// </summary>
public class SearchResponse
{
    /// <summary>Search results for the current page.</summary>
    public required SearchResult[] Results { get; set; }

    /// <summary>Total number of matches found by Genius (may exceed returned count).</summary>
    public int TotalCount { get; set; }

    /// <summary>Whether more results are available beyond this page.</summary>
    public bool HasMore { get; set; }

    /// <summary>Current page number (1-indexed).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Results per page.</summary>
    public int PerPage { get; set; } = 10;
}
