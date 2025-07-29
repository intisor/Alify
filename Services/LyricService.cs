#nullable enable
using Alify.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Alify.Services
{
    /// <summary>
    /// Service for fetching and moderating song lyrics.
    /// </summary>
    public class LyricService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        // Gemini API rate limiting (30 RPM = 1 request every 2 seconds)
        private static readonly SemaphoreSlim _geminiRateLimiter = new(1, 1);
        private static DateTime _lastGeminiRequestTime = DateTime.MinValue;
        private static readonly TimeSpan _geminiRequestInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Initializes a new instance of the <see cref="LyricService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for making requests.</param>
        /// <param name="configuration">The application configuration for accessing API keys.</param>
        /// <param name="cache">The memory cache for storing lyrics and moderation results.</param>
        public LyricService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _cache = cache;
        }

        /// <summary>
        /// Gets the lyrics for a song, using a cache to avoid repeated requests.
        /// </summary>
        /// <param name="artist">The artist of the song.</param>
        /// <param name="title">The title of the song.</param>
        /// <returns>The lyrics of the song, or null if not found.</returns>
        public async Task<string?> GetLyricsAsync(string artist, string title)
        {
            var cacheKey = $"lyrics_{artist}_{title}";
            if (_cache.TryGetValue(cacheKey, out string? cachedLyrics))
            {
                return cachedLyrics;
            }

            var geniusLyrics = await GetLyricsFromGeniusAsync(title, artist);

            if (!string.IsNullOrWhiteSpace(geniusLyrics))
            {
                _cache.Set(cacheKey, geniusLyrics, TimeSpan.FromHours(24));
                Console.WriteLine($"Lyrics found for: {title} by {artist}");
            }
            else
            {
                Console.WriteLine($"No lyrics found for: {title} by {artist}");
            }

            return geniusLyrics;
        }

        /// <summary>
        /// Fetches lyrics from the Genius API.
        /// </summary>
        /// <param name="title">The title of the song.</param>
        /// <param name="artist">The artist of the song.</param>
        /// <returns>The lyrics of the song, or null if an error occurs.</returns>
        private async Task<string?> GetLyricsFromGeniusAsync(string title, string artist)
        {
            try
            {
                var geniusKey = _configuration["Genius:token"];
                if (string.IsNullOrEmpty(geniusKey))
                {
                    Console.WriteLine("Genius API token is not configured.");
                    return null;
                }

                var searchUrl = $"https://api.genius.com/search?q={HttpUtility.UrlEncode($"{title} {artist}")}";
                
                using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", geniusKey);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Genius search failed: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var hit = doc.RootElement.GetProperty("response").GetProperty("hits").EnumerateArray().FirstOrDefault();
                if (hit.ValueKind == JsonValueKind.Undefined)
                {
                    return null;
                }

                var songUrl = hit.GetProperty("result").GetProperty("url").GetString();
                if (string.IsNullOrEmpty(songUrl)) return null;

                var html = await _httpClient.GetStringAsync(songUrl);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var lyricsNode = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'Lyrics__Container')]");
                if (lyricsNode == null) return null;

                // Replace <br> tags with newlines to preserve formatting
                var lyricsHtml = lyricsNode.InnerHtml;
                var lyricsWithNewlines = lyricsHtml.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);

                // Create a new HtmlDocument to parse the modified HTML and get the plain text
                var tempDoc = new HtmlDocument();
                tempDoc.LoadHtml(lyricsWithNewlines);
                var lyrics = HttpUtility.HtmlDecode(tempDoc.DocumentNode.InnerText).Trim();

                return string.IsNullOrWhiteSpace(lyrics) ? null : lyrics;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching lyrics from Genius: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Moderates lyrics for explicit content using the Gemini API.
        /// </summary>
        /// <param name="lyrics">The lyrics to moderate.</param>
        /// <returns>A <see cref="LyricsModerationResult"/> indicating the content categories, or null if an error occurs.</returns>
        public async Task<LyricsModerationResult?> ModerateLyricsAsync(string lyrics)
        {
            var cacheKey = $"moderation_{lyrics.GetHashCode()}";
            if (_cache.TryGetValue(cacheKey, out LyricsModerationResult? cachedResult))
            {
                return cachedResult;
            }

            var apiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey.StartsWith("AIza") == false)
            {
                Console.WriteLine("Gemini API key is not configured. Please set it in appsettings.json.");
                return null;
            }

            return await ModerateLyricsWithRetryAsync(lyrics, apiKey, cacheKey);
        }

        /// <summary>
        /// Moderates lyrics with a retry mechanism and rate limiting.
        /// </summary>
        /// <param name="lyrics">The lyrics to moderate.</param>
        /// <param name="apiKey">The Gemini API key.</param>
        /// <param name="cacheKey">The cache key for storing the result.</param>
        /// <returns>A <see cref="LyricsModerationResult"/>, or null if the request fails.</returns>
        private async Task<LyricsModerationResult?> ModerateLyricsWithRetryAsync(string lyrics, string apiKey, string cacheKey)
        {
            const int maxRetries = 3;
            var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";
            var promptBuilder = new StringBuilder();
            promptBuilder.Append("Return only valid JSON like:\n{ \"violence\": true/false, \"hate\": true/false, \"profanity\": true/false, \"sexual\": true/false, \"suitable_for_kids\": true/false }\n\nLyrics:\n");
            promptBuilder.Append(lyrics);
            var prompt = new { contents = new[] { new { parts = new[] { new { text = promptBuilder.ToString() } }, role = "user" } } };
            var requestJson = JsonSerializer.Serialize(prompt);

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                await _geminiRateLimiter.WaitAsync();
                try
                {
                    var timeSinceLastRequest = DateTime.UtcNow - _lastGeminiRequestTime;
                    if (timeSinceLastRequest < _geminiRequestInterval)
                    {
                        await Task.Delay(_geminiRequestInterval - timeSinceLastRequest);
                    }

                    // Create a CancellationTokenSource for the request timeout
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    var requestBody = new StringContent(requestJson, Encoding.UTF8, "application/json");
                    
                    // Use the injected _httpClient instead of creating a new one
                    var response = await _httpClient.PostAsync($"{endpoint}?key={apiKey}", requestBody, cts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        var result = ParseGeminiResponse(responseString);
                        if (result != null)
                        {
                            _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                            Console.WriteLine($"Gemini moderation successful (attempt {attempt})");
                        }
                        return result;
                    }

                    if (!IsRetryable(response.StatusCode) || attempt == maxRetries)
                    {
                        Console.WriteLine($"Gemini API request failed with status {response.StatusCode}. Not retrying.");
                        return null;
                    }
                }
                // Catch OperationCanceledException specifically for timeouts
                catch (OperationCanceledException ex)
                {
                    Console.WriteLine($"Error during lyrics moderation (attempt {attempt}): Request timed out. {ex.Message}");
                    if (attempt == maxRetries) return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during lyrics moderation (attempt {attempt}): {ex.Message}");
                    if (attempt == maxRetries) return null;
                }
                finally
                {
                    _lastGeminiRequestTime = DateTime.UtcNow;
                    _geminiRateLimiter.Release();
                }

                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Waiting {delay.TotalSeconds}s before retry...");
                    await Task.Delay(delay);
                }
            }
            return null;
        }

        /// <summary>
        /// Determines if an HTTP status code is retryable.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <returns>True if the status code indicates a transient error, false otherwise.</returns>
        private static bool IsRetryable(HttpStatusCode statusCode) =>
            statusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;

        /// <summary>
        /// Parses the JSON response from the Gemini API.
        /// </summary>
        /// <param name="responseString">The JSON response string.</param>
        /// <returns>A <see cref="LyricsModerationResult"/>, or null if parsing fails.</returns>
        private static LyricsModerationResult? ParseGeminiResponse(string responseString)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var textResponse = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

                if (textResponse is null) return null;

                var jsonStart = textResponse.IndexOf('{');
                var jsonEnd = textResponse.LastIndexOf('}');

                if (jsonStart == -1 || jsonEnd == -1) return null;

                var cleanJson = textResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<LyricsModerationResult>(cleanJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse Gemini response: {ex.Message}");
                return null;
            }
        }
    }
}
