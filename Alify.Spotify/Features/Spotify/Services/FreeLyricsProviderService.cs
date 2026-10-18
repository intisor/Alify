using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alify.Core.Models;
using Microsoft.Extensions.Logging;

namespace Alify.Features.Spotify.Services
{
    public class FreeLyricsProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FreeLyricsProviderService> _logger;

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

            // 1. Primary: LRCLIB (Open-source community database, 10M+ tracks, instant REST response)
            try
            {
                var encodedArtist = Uri.EscapeDataString(artist.Trim());
                var encodedTrack = Uri.EscapeDataString(track.Trim());
                var lrclibUrl = $"https://lrclib.net/api/get?artist_name={encodedArtist}&track_name={encodedTrack}";

                using var request = new HttpRequestMessage(HttpMethod.Get, lrclibUrl);
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
                        _logger.LogInformation("Fetched plain lyrics for '{Track}' by '{Artist}' from LRCLIB", track, artist);
                        return lyrics;
                    }

                    if (doc.RootElement.TryGetProperty("syncedLyrics", out var syncedProp) &&
                        syncedProp.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(syncedProp.GetString()))
                    {
                        // Strip timestamps like [01:23.45] to get plain readable lyric lines
                        var raw = syncedProp.GetString()!;
                        var cleaned = Regex.Replace(raw, @"^\[\d{2}:\d{2}\.\d{2}\]\s*", "", RegexOptions.Multiline);
                        _logger.LogInformation("Fetched synced lyrics (stripped) for '{Track}' by '{Artist}' from LRCLIB", track, artist);
                        return cleaned;
                    }
                }
                else
                {
                    _logger.LogDebug("LRCLIB returned {StatusCode} for '{Track}' by '{Artist}'", response.StatusCode, track, artist);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching from LRCLIB for '{Track}' by '{Artist}', attempting fallback", track, artist);
            }

            // 2. Secondary Fallback: lyrics.ovh
            try
            {
                var encodedArtist = Uri.EscapeDataString(artist.Trim());
                var encodedTrack = Uri.EscapeDataString(track.Trim());
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
                            _logger.LogInformation("Fetched lyrics for '{Track}' by '{Artist}' from lyrics.ovh fallback", track, artist);
                            return lyrics;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Fallback lyrics.ovh also failed for '{Track}' by '{Artist}'", track, artist);
            }

            return string.Empty;
        }
    }
}