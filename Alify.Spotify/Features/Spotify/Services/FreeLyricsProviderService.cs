using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alify.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Alify.Features.Spotify.Services
{
    /// <summary>
    /// Multi-tiered lyrics provider supporting global and regional repertoire (especially Afrobeats / Nigerian music).
    /// Tier 1: LRCLIB Exact Lookup
    /// Tier 2: LRCLIB Fuzzy Title-Normalized Search
    /// Tier 3: Direct Lightweight Genius Scraper (DOM ad-exclusion, multi-container stitching, zero browser overhead)
    /// Tier 4: lyrics.ovh Fallback
    /// </summary>
    public class FreeLyricsProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FreeLyricsProviderService> _logger;

        private const string BROWSER_USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

        public FreeLyricsProviderService(HttpClient httpClient, ILogger<FreeLyricsProviderService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GetLyricsAsync(string artist, string track)
        {
            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(track))
            {
                return string.Empty;
            }

            var cleanTrack = SanitizeTrackTitle(track);
            var cleanArtist = SanitizeArtistName(artist);

            // Tier 1: LRCLIB Exact Match
            var lyrics = await TryGetLrclibExactAsync(artist, track);
            if (!string.IsNullOrWhiteSpace(lyrics)) return lyrics;

            // Tier 2: LRCLIB Fuzzy Title-Normalized Search (great for Spotify tracks with features/remixes)
            lyrics = await TryGetLrclibSearchAsync(cleanArtist, cleanTrack);
            if (!string.IsNullOrWhiteSpace(lyrics)) return lyrics;

            // Tier 3: Direct Lightweight Genius (bypasses ads across multiple data-lyrics-containers)
            lyrics = await TryGetGeniusDirectAsync(cleanArtist, cleanTrack);
            if (!string.IsNullOrWhiteSpace(lyrics)) return lyrics;

            // Tier 4: lyrics.ovh Fallback
            lyrics = await TryGetLyricsOvhAsync(cleanArtist, cleanTrack);
            if (!string.IsNullOrWhiteSpace(lyrics)) return lyrics;

            _logger.LogInformation("No lyrics found across all 4 tiers for '{Track}' by '{Artist}'", track, artist);
            return string.Empty;
        }

        private async Task<string?> TryGetLrclibExactAsync(string artist, string track)
        {
            try
            {
                var encodedArtist = Uri.EscapeDataString(artist.Trim());
                var encodedTrack = Uri.EscapeDataString(track.Trim());
                var url = $"https://lrclib.net/api/get?artist_name={encodedArtist}&track_name={encodedTrack}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Alify/1.0 (https://github.com/alify-app)");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("plainLyrics", out var plainProp) &&
                        plainProp.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(plainProp.GetString()))
                    {
                        var lyrics = plainProp.GetString()!;
                        _logger.LogInformation("Tier 1 (LRCLIB Exact): Fetched lyrics for '{Track}' by '{Artist}'", track, artist);
                        return lyrics;
                    }

                    if (doc.RootElement.TryGetProperty("syncedLyrics", out var syncedProp) &&
                        syncedProp.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(syncedProp.GetString()))
                    {
                        var cleaned = StripLrcTimestamps(syncedProp.GetString()!);
                        _logger.LogInformation("Tier 1 (LRCLIB Exact Synced): Fetched lyrics for '{Track}' by '{Artist}'", track, artist);
                        return cleaned;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tier 1 LRCLIB exact match failed for '{Track}' by '{Artist}'", track, artist);
            }

            return null;
        }

        private async Task<string?> TryGetLrclibSearchAsync(string cleanArtist, string cleanTrack)
        {
            try
            {
                var query = Uri.EscapeDataString($"{cleanArtist} {cleanTrack}".Trim());
                var url = $"https://lrclib.net/api/search?q={query}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Alify/1.0 (https://github.com/alify-app)");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            if (item.TryGetProperty("plainLyrics", out var plainProp) &&
                                plainProp.ValueKind == JsonValueKind.String &&
                                !string.IsNullOrWhiteSpace(plainProp.GetString()))
                            {
                                var lyrics = plainProp.GetString()!;
                                _logger.LogInformation("Tier 2 (LRCLIB Search): Fetched lyrics for '{Track}' by '{Artist}'", cleanTrack, cleanArtist);
                                return lyrics;
                            }

                            if (item.TryGetProperty("syncedLyrics", out var syncedProp) &&
                                syncedProp.ValueKind == JsonValueKind.String &&
                                !string.IsNullOrWhiteSpace(syncedProp.GetString()))
                            {
                                var cleaned = StripLrcTimestamps(syncedProp.GetString()!);
                                _logger.LogInformation("Tier 2 (LRCLIB Search Synced): Fetched lyrics for '{Track}' by '{Artist}'", cleanTrack, cleanArtist);
                                return cleaned;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tier 2 LRCLIB search failed for '{Track}' by '{Artist}'", cleanTrack, cleanArtist);
            }

            return null;
        }

        public async Task<string?> TryGetGeniusDirectAsync(string cleanArtist, string cleanTrack)
        {
            try
            {
                var query = Uri.EscapeDataString($"{cleanArtist} {cleanTrack}".Trim());
                var searchUrl = $"https://genius.com/api/search/multi?q={query}";

                using var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                searchRequest.Headers.Add("User-Agent", BROWSER_USER_AGENT);
                searchRequest.Headers.Add("Accept", "application/json");

                var searchResponse = await _httpClient.SendAsync(searchRequest);
                if (!searchResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                var searchJson = await searchResponse.Content.ReadAsStringAsync();
                using var searchDoc = JsonDocument.Parse(searchJson);

                string? songPageUrl = null;
                if (searchDoc.RootElement.TryGetProperty("response", out var respProp) &&
                    respProp.TryGetProperty("sections", out var sectionsProp) &&
                    sectionsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var section in sectionsProp.EnumerateArray())
                    {
                        if (section.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "song")
                        {
                            if (section.TryGetProperty("hits", out var hitsProp) && hitsProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var hit in hitsProp.EnumerateArray())
                                {
                                    if (hit.TryGetProperty("result", out var resultProp) &&
                                        resultProp.TryGetProperty("url", out var urlProp) &&
                                        urlProp.ValueKind == JsonValueKind.String)
                                    {
                                        var url = urlProp.GetString();
                                        if (!string.IsNullOrWhiteSpace(url) && url.Contains("-lyrics"))
                                        {
                                            songPageUrl = url;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (songPageUrl != null) break;
                    }
                }

                if (string.IsNullOrWhiteSpace(songPageUrl))
                {
                    return null;
                }

                // Fetch Genius song page HTML
                using var pageRequest = new HttpRequestMessage(HttpMethod.Get, songPageUrl);
                pageRequest.Headers.Add("User-Agent", BROWSER_USER_AGENT);
                pageRequest.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                var pageResponse = await _httpClient.SendAsync(pageRequest);
                if (!pageResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                var pageHtml = await pageResponse.Content.ReadAsStringAsync();
                var lyrics = ParseGeniusLyricsFromHtml(pageHtml);
                if (!string.IsNullOrWhiteSpace(lyrics))
                {
                    _logger.LogInformation("Tier 3 (Direct Genius): Fetched full lyrics for '{Track}' by '{Artist}' from {Url}", cleanTrack, cleanArtist, songPageUrl);
                    return lyrics;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tier 3 Genius direct fetch failed for '{Track}' by '{Artist}'", cleanTrack, cleanArtist);
            }

            return null;
        }

        /// <summary>
        /// Robust HTML parser for Genius lyrics pages.
        /// Resolves the common bug where inline ads/headers truncate multi-container songs.
        /// Extracts and stitches all data-lyrics-container blocks while stripping data-exclude-from-selection nodes.
        /// </summary>
        public static string ParseGeniusLyricsFromHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Match all data-lyrics-container divs across the document
            var containers = doc.DocumentNode.SelectNodes("//div[@data-lyrics-container='true']");
            if (containers == null || containers.Count == 0)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();

            foreach (var container in containers)
            {
                // 1. Remove all inline ads, contributor widgets, and recommendations
                var excludedNodes = container.SelectNodes(".//*[@data-exclude-from-selection='true']");
                if (excludedNodes != null)
                {
                    foreach (var node in excludedNodes)
                    {
                        node.Remove();
                    }
                }

                // 2. Replace HTML line breaks with newlines
                var brNodes = container.SelectNodes(".//br");
                if (brNodes != null)
                {
                    foreach (var br in brNodes)
                    {
                        br.ParentNode.ReplaceChild(doc.CreateTextNode("\n"), br);
                    }
                }

                // 3. Extract text content and decode HTML entities
                var text = WebUtility.HtmlDecode(container.InnerText).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }

            return sb.ToString().Trim();
        }

        private async Task<string?> TryGetLyricsOvhAsync(string cleanArtist, string cleanTrack)
        {
            try
            {
                var encodedArtist = Uri.EscapeDataString(cleanArtist);
                var encodedTrack = Uri.EscapeDataString(cleanTrack);
                var url = $"https://api.lyrics.ovh/v1/{encodedArtist}/{encodedTrack}";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("lyrics", out var lyricsProp) &&
                        lyricsProp.ValueKind == JsonValueKind.String)
                    {
                        var lyrics = lyricsProp.GetString();
                        if (!string.IsNullOrWhiteSpace(lyrics))
                        {
                            _logger.LogInformation("Tier 4 (lyrics.ovh): Fetched lyrics for '{Track}' by '{Artist}'", cleanTrack, cleanArtist);
                            return lyrics;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tier 4 lyrics.ovh failed for '{Track}' by '{Artist}'", cleanTrack, cleanArtist);
            }

            return null;
        }

        /// <summary>
        /// Strips Spotify track noise: (feat. ...), [feat. ...], - Remastered, (Remix), etc.
        /// </summary>
        public static string SanitizeTrackTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;

            var cleaned = Regex.Replace(title, @"\s*[\(\[](?:feat|ft|with)[\.\s][^\)\]]+[\)\]]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*[\(\[](?:remix|explicit|clean|live|deluxe|version|edit|bonus\s+track)[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*-\s*(?:remastered|live|deluxe|bonus\s+track).*$", "", RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }

        /// <summary>
        /// Extracts the primary artist name when multiple are joined with &, commas, or "feat.".
        /// </summary>
        public static string SanitizeArtistName(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return string.Empty;

            var split = Regex.Split(artist, @"(?:\s*,\s*|\s+(?:&|feat\.?|ft\.?)\s+)", RegexOptions.IgnoreCase);
            return split.Length > 0 ? split[0].Trim() : artist.Trim();
        }

        private static string StripLrcTimestamps(string syncedLyrics)
        {
            return Regex.Replace(syncedLyrics, @"^\[\d{2}:\d{2}\.\d{2}\]\s*", "", RegexOptions.Multiline).Trim();
        }
    }
}