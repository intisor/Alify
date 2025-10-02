using System.Net.Http;
using System.Text.Json;
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
            try
            {
                var encodedArtist = Uri.EscapeDataString(artist);
                var encodedTrack = Uri.EscapeDataString(track);
                var url = $"https://api.lyrics.ovh/v1/{encodedArtist}/{encodedTrack}";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var lyrics = doc.RootElement.GetProperty("lyrics").GetString();
                    _logger.LogInformation("Fetched lyrics for {Track} by {Artist} from free provider", track, artist);
                    return lyrics ?? string.Empty;
                }
                else
                {
                    _logger.LogWarning("Failed to fetch lyrics for {Track} by {Artist}: {StatusCode}", track, artist, response.StatusCode);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching lyrics for {Track} by {Artist}", track, artist);
                return string.Empty;
            }
        }
    }
}