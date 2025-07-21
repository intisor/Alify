using Alify.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Alify.Services
{
    public class LyricService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public LyricService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<string> GetLyricsAsync(string artist, string title)
        {
            var cacheKey = $"lyrics_{artist}_{title}";
            if (_cache.TryGetValue(cacheKey, out string cachedLyrics))
            {
                return cachedLyrics;
            }

            Console.WriteLine($"Fetching lyrics for: {title} by {artist}");

            // Try Genius
            var geniusLyrics = await GetLyricsFromGeniusAsync(title, artist);

            if (!string.IsNullOrWhiteSpace(geniusLyrics))
            {
                _cache.Set(cacheKey, geniusLyrics, TimeSpan.FromHours(24));
                Console.WriteLine($"Successfully fetched lyrics for: {title} by {artist}");
            }
            else
            {
                Console.WriteLine($"No lyrics found for: {title} by {artist}");
            }

            return !string.IsNullOrWhiteSpace(geniusLyrics)
                ? geniusLyrics
                : null;
        }

        private async Task<string> TryGetLyricsFromOvhAsync(string artist, string title)
        {
            var url = $"https://api.lyrics.ovh/v1/{HttpUtility.UrlEncode(artist)}/{HttpUtility.UrlEncode(title)}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("lyrics").GetString();
            }
            return null;
        }

        private async Task<string> GetLyricsFromGeniusAsync(string title, string artist)
        {
            try
            {
                var query = HttpUtility.UrlEncode($"{title} {artist}");
                var geniusKey = _configuration["Genius:token"];
                
                if (string.IsNullOrEmpty(geniusKey))
                {
                    Console.WriteLine("Genius API token is not configured");
                    return null;
                }

                var searchUrl = $"https://api.genius.com/search?q={query}";

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", geniusKey);

                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode) 
                {
                    Console.WriteLine($"Genius search failed: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var hits = doc.RootElement.GetProperty("response").GetProperty("hits").EnumerateArray();
                var hit = hits.FirstOrDefault();
                
                if (hit.ValueKind == JsonValueKind.Undefined) 
                {
                    Console.WriteLine("No search results found on Genius");
                    return null;
                }

                var songUrl = hit.GetProperty("result").GetProperty("url").GetString();
                if (string.IsNullOrEmpty(songUrl)) 
                {
                    Console.WriteLine("No song URL found in Genius results");
                    return null;
                }

                Console.WriteLine($"Scraping lyrics from: {songUrl}");

                // Step 2: Scrape lyrics
                var html = await _httpClient.GetStringAsync(songUrl);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var lyricsNode = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'Lyrics__Container')]");
                if (lyricsNode == null) 
                {
                    Console.WriteLine("Lyrics container not found on Genius page");
                    return null;
                }

                // Step 3: Clean lyrics
                var lyrics = lyricsNode.InnerText;
                lyrics = HttpUtility.HtmlDecode(lyrics);
                lyrics = lyrics.Trim();

                if (string.IsNullOrWhiteSpace(lyrics))
                {
                    Console.WriteLine("Extracted lyrics are empty");
                    return null;
                }

                Console.WriteLine($"Successfully extracted lyrics ({lyrics.Length} characters)");
                return lyrics;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching lyrics from Genius: {ex.Message}");
                return null;
            }
        }

        public async Task<LyricsModerationResult> ModerateLyricsAsync(string lyrics)
        {
            var cacheKey = $"moderation_{lyrics.GetHashCode()}";
            if (_cache.TryGetValue(cacheKey, out LyricsModerationResult cachedResult))
            {
                return cachedResult;
            }

            var apiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Gemini API key is not configured");
                return null;
            }

            var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";
            var url = $"{endpoint}?key={apiKey}";

            var prompt = new
            {
                contents = new[]
                {
                    new 
                    {
                        parts = new[] 
                        {
                            new 
                            {
                                text = $"Return only valid JSON like:\n" +
                                    "{{ \"violence\": true/false, \"hate\": true/false, \"profanity\": true/false, \"sexual\": true/false, \"suitable_for_kids\": true/false }}\n\n" +
                                    $"Lyrics:\n{lyrics}"
                            }
                        },
                        role = "user"
                    }
                }
            };

            try
            {
                var requestJson = JsonSerializer.Serialize(prompt);
                var requestBody = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, requestBody);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Gemini API error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(responseString))
                {
                    Console.WriteLine("Empty response from Gemini API");
                    return null;
                }

                using var doc = JsonDocument.Parse(responseString);
                var textResponse = doc
                    .RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                var jsonStart = textResponse?.IndexOf('{') ?? -1;
                var jsonEnd = textResponse?.LastIndexOf('}') ?? -1;

                if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
                {
                    Console.WriteLine($"Could not extract clean JSON block from: {textResponse}");
                    return null;
                }

                var cleanJson = textResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                Console.WriteLine($"Extracted JSON: {cleanJson}");

                var result = JsonSerializer.Deserialize<LyricsModerationResult>(cleanJson);
                if (result != null)
                {
                    _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                    Console.WriteLine($"Moderation result - Violence: {result.violence}, Hate: {result.hate}, Profanity: {result.profanity}, Sexual: {result.sexual}, Suitable for kids: {result.suitable_for_kids}");
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during lyrics moderation: {ex.Message}");
                return null;
            }
        }
    }
}
