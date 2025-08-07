using System.Diagnostics;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Alify.Core.Models;
using Alify.Core.Infrastructure.Logging;

namespace Alify.Services
{
    [DebuggerDisplay("HasGeniusKey: {_apiKeys.Genius.Token != null}, HasGeminiKey: {_apiKeys.Gemini.ApiKey != null}, HasOpenRouterKey: {_apiKeys.OpenRouter.ApiKey != null}, HasMistralKey: {_apiKeys.Mistral.ApiKey != null}")]
    public class LyricService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ApiKeys _apiKeys;
        private readonly ILogger<LyricService> _logger;
        private readonly PlaywrightLyricsScraper _playwrightScraper;

        // Gemini API rate limiting (30 RPM = 1 request every 2 seconds)
        private static readonly SemaphoreSlim _geminiRateLimiter = new(1, 1);
        private static DateTime _lastGeminiRequestTime = DateTime.MinValue;
        private static readonly TimeSpan _geminiRequestInterval = TimeSpan.FromSeconds(2);

        public LyricService(HttpClient httpClient, IOptions<ApiKeys> apiKeys, IMemoryCache cache, ILogger<LyricService> logger)
        {
            _httpClient = httpClient;
            _apiKeys = apiKeys.Value;
            _cache = cache;
            _logger = logger;
            _playwrightScraper = new PlaywrightLyricsScraper();

            // Log API key status for debugging
            _logger.LogInformation("Genius API Key: {Status}", string.IsNullOrEmpty(_apiKeys.Genius?.Token) ? "Not Set" : "Set");
            _logger.LogInformation("Gemini API Key: {Status}", string.IsNullOrEmpty(_apiKeys.Gemini?.ApiKey) ? "Not Set" : "Set");
            _logger.LogInformation("OpenRouter API Key: {Status}", string.IsNullOrEmpty(_apiKeys.OpenRouter?.ApiKey) ? "Not Set" : "Set");
            _logger.LogInformation("Mistral API Key: {Status}", string.IsNullOrEmpty(_apiKeys.Mistral?.ApiKey) ? "Not Set" : "Set");
        }

        public async Task<string?> GetLyricsAsync(string artist, string title)
        {
            string cacheKey = $"lyrics_{artist}_{title}";
            if (_cache.TryGetValue(cacheKey, out string? cachedLyrics))
            {
                return cachedLyrics;
            }

            var geniusKey = _apiKeys.Genius?.Token;
            if (string.IsNullOrEmpty(geniusKey))
            {
                _logger.LogGeniusTokenMissing();
                return null;
            }

            string searchUrl = $"https://api.genius.com/search?q={System.Web.HttpUtility.UrlEncode($"{title} {artist}")}";
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", geniusKey);

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogGeniusSearchFailed(response.StatusCode, title, artist);
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
            JsonElement hit = doc.RootElement.GetProperty("response").GetProperty("hits").EnumerateArray().FirstOrDefault();
            if (hit.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                return null;
            }

            var songUrl = hit.GetProperty("result").GetProperty("url").GetString();
            if (string.IsNullOrEmpty(songUrl)) return null;

            // Use Playwright as the main scraper
            var lyrics = await _playwrightScraper.ScrapeLyricsAsync(songUrl);
            if (!string.IsNullOrWhiteSpace(lyrics))
            {
                _cache.Set(cacheKey, lyrics, TimeSpan.FromHours(24));
                _logger.LogLyricsFound(title, artist);
            }
            else
            {
                _logger.LogLyricsNotFound(title, artist);
            }
            return lyrics;
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

            // The check for the primary API key is now handled within the retry logic.
            return await ModerateLyricsWithRetryAsync(lyrics, cacheKey);
        }

        /// <summary>
        /// Moderates lyrics with a retry mechanism and rate limiting, falling back to OpenRouter and Mistral if Gemini fails.
        /// </summary>
        /// <param name="lyrics">The lyrics to moderate.</param>
        /// <param name="cacheKey">The cache key for storing the result.</param>
        /// <returns>A <see cref="LyricsModerationResult"/>, or null if the request fails.</returns>
        private async Task<LyricsModerationResult?> ModerateLyricsWithRetryAsync(string lyrics, string cacheKey)
        {
            const int maxRetries = 3;
            var promptBuilder = new StringBuilder();
            promptBuilder.Append("Return only valid JSON like:\n{ \"violence\": true/false, \"hate\": true/false, \"profanity\": true/false, \"sexual\": true/false, \"suitable_for_kids\": true/false }\n\nLyrics:\n");
            promptBuilder.Append(lyrics);
            var prompt = promptBuilder.ToString();

            var providers = new List<(string Provider, string Endpoint, string? ApiKey, string Model)>
            {
                ("Gemini", "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent", _apiKeys.Gemini?.ApiKey, "gemini-1.5-flash"),
                ("OpenRouter", "https://openrouter.ai/api/v1/chat/completions", _apiKeys.OpenRouter?.ApiKey, "meta-llama/llama-3.1-8b-instruct"),
                ("Mistral", "https://api.mistral.ai/v1/chat/moderations", _apiKeys.Mistral?.ApiKey, "mistral-moderation-latest")
            };

            foreach (var (provider, endpoint, providerApiKey, model) in providers)
            {
                _logger.LogInformation("Moderation attempt using {Provider} with API Key: {Status}", provider, string.IsNullOrEmpty(providerApiKey) ? "Not Set" : "Set");
                if (string.IsNullOrEmpty(providerApiKey))
                {
                    _logger.LogModerationApiKeyMissing(provider);
                    continue;
                }

                int consecutive503s = 0;
                for (var attempt = 1; attempt <= maxRetries; attempt++)
                {
                    if (provider == "Gemini")
                        await _geminiRateLimiter.WaitAsync();
                    try
                    {
                        if (provider == "Gemini")
                        {
                            var timeSinceLastRequest = DateTime.UtcNow - _lastGeminiRequestTime;
                            if (timeSinceLastRequest < _geminiRequestInterval)
                            {
                                await Task.Delay(_geminiRequestInterval - timeSinceLastRequest);
                            }
                        }
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        var result = await CallApiAsync(provider, endpoint, providerApiKey, model, prompt, cts.Token);
                        if (result != null)
                        {
                            _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                            _logger.LogModerationSuccess(provider, attempt);
                            return result;
                        }
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode.HasValue && IsRetryable(ex.StatusCode.Value))
                    {
                        _logger.LogModerationRateLimit(provider, attempt, ex.StatusCode, ex.Message);
                        if (provider == "Gemini" && ex.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            _logger.LogModerationProviderFailed(provider, attempt);
                            break;
                        }
                        if (provider == "Gemini" && ex.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            consecutive503s++;
                            if (consecutive503s >= 2)
                            {
                                _logger.LogWarning("Gemini returned 503 ServiceUnavailable twice in a row. Falling back to next provider.");
                                break;
                            }
                        }
                        if (attempt == maxRetries) break;
                    }
                    catch (OperationCanceledException ex)
                    {
                        _logger.LogModerationTimeout(provider, attempt, ex.Message);
                        if (attempt == maxRetries) break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogModerationUnexpectedError(ex, provider, attempt);
                        if (attempt == maxRetries) break;
                    }
                    finally
                    {
                        if (provider == "Gemini")
                        {
                            _lastGeminiRequestTime = DateTime.UtcNow;
                            _geminiRateLimiter.Release();
                        }
                    }

                    if (attempt < maxRetries)
                    {
                        TimeSpan delay;
                        if (provider == "Gemini" && consecutive503s > 0)
                        {
                            delay = TimeSpan.FromSeconds(10 * consecutive503s); // Exponential backoff for 503s
                        }
                        else
                        {
                            delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                        }
                        _logger.LogModerationRetryDelay(delay.TotalSeconds);
                        await Task.Delay(delay);
                    }
                }
                // If Gemini fails after 2x 503 or 3 attempts, fallback to next provider
            }

            _logger.LogModerationAllProvidersFailed();
            return null;
        }

        /// <summary>
        /// Makes an API call to the specified provider.
        /// </summary>
        /// <param name="provider">The API provider name.</param>
        /// <param name="endpoint">The API endpoint URL.</param>
        /// <param name="apiKey">The API key for the provider.</param>
        /// <param name="model">The model to use for the provider.</param>
        /// <param name="prompt">The prompt to send.</param>
        /// <param name="cancellationToken">The cancellation token for the request.</param>
        /// <returns>A <see cref="LyricsModerationResult"/>, or null if the request fails.</returns>
        private async Task<LyricsModerationResult?> CallApiAsync(string provider, string endpoint, string apiKey, string model, string prompt, CancellationToken cancellationToken)
        {
            object requestBody;
            if (provider == "Gemini")
            {
                requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } }, role = "user" } } };
            }
            else if (provider == "Mistral")
            {
                requestBody = new {
                    model,
                    input = new[] { new { role = "user", content = prompt } }
                };
            }
            else
            {
                requestBody = new { model, messages = new[] { new { role = "user", content = prompt } } };
            }

            var requestJson = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, provider == "Gemini" ? $"{endpoint}?key={apiKey}" : endpoint)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            if (provider != "Gemini")
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Provider {Provider} failed with {StatusCode}: {ErrorContent}", provider, response.StatusCode, errorContent);
                if (IsRetryable(response.StatusCode))
                {
                    response.EnsureSuccessStatusCode();
                }
                _logger.LogModerationNonRetryableError(provider, response.StatusCode);
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();
            if (provider == "Gemini")
                return ParseGeminiResponse(responseString);
            if (provider == "Mistral")
                return ParseMistralResponse(responseString);
            return ParseOpenAIResponse(responseString);
        }

        /// <summary>
        /// Parses the JSON response from the Mistral API.
        /// </summary>
        /// <param name="responseString">The JSON response string.</param>
        /// <returns>A <see cref="LyricsModerationResult"/>, or null if parsing fails.</returns>
        private LyricsModerationResult? ParseMistralResponse(string responseString)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var result = new LyricsModerationResult();
                var root = doc.RootElement;
                // Defensive: check if 'results' exists and is an array with at least one element
                if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                {
                    _logger.LogModerationParseError(new KeyNotFoundException("'results' array missing or empty in Mistral response"), "Mistral");
                    return null;
                }
                var firstResult = results[0];
                // Defensive: check for 'flagged' and 'category_scores'
                if (!firstResult.TryGetProperty("flagged", out var flaggedProp) || !firstResult.TryGetProperty("category_scores", out var scores))
                {
                    _logger.LogModerationParseError(new KeyNotFoundException("'flagged' or 'category_scores' missing in Mistral response"), "Mistral");
                    return null;
                }
                var flagged = flaggedProp.GetBoolean();
                result.suitable_for_kids = !flagged;
                result.violence = scores.TryGetProperty("violence", out var violenceScore) && violenceScore.GetDouble() > 0.5;
                result.hate = scores.TryGetProperty("hate", out var hateScore) && hateScore.GetDouble() > 0.5;
                result.sexual = scores.TryGetProperty("sexual", out var sexualScore) && sexualScore.GetDouble() > 0.5;
                result.profanity = scores.TryGetProperty("profanity", out var profanityScore) && profanityScore.GetDouble() > 0.5;
                result.confidence = flagged ? 1.0 : 1.0 - scores.EnumerateObject().Max(p => p.Value.GetDouble());
                result.moderatedAt = DateTime.UtcNow;
                result.context = flagged ? "Flagged by Mistral" : "Not flagged by Mistral";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogModerationParseError(ex, "Mistral");
                return null;
            }
        }

        /// <summary>
        /// Parses the JSON response from OpenAI-compatible APIs (OpenRouter, Mistral).
        /// </summary>
        /// <param name="responseString">The JSON response string.</param>
        /// <returns>A <see cref="LyricsModerationResult"/>, or null if parsing fails.</returns>
        private LyricsModerationResult? ParseOpenAIResponse(string responseString)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (content is null) return null;

                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');

                if (jsonStart == -1 || jsonEnd == -1) return null;

                var cleanJson = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<LyricsModerationResult>(cleanJson);
            }
            catch (Exception ex)
            {
                // HIGH-PERFORMANCE LOGGING: Exception + provider parameter, optimized
                _logger.LogModerationParseError(ex, "OpenAI-compatible");
                return null;
            }
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
        private LyricsModerationResult? ParseGeminiResponse(string responseString)
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
                // HIGH-PERFORMANCE LOGGING: Exception + provider parameter, zero allocation
                _logger.LogModerationParseError(ex, "Gemini");
                return null;
            }
        }
    }
}
