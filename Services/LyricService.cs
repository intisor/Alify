using Alify.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using static System.Net.WebRequestMethods;

namespace Alify.Services
{
    public class LyricService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public LyricService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }
        public async Task<string> GetLyricsAsync(string artist,string title)
        {
            // 1. Try Lyrics.ovh
            var ovhLyrics = await TryGetLyricsFromOvhAsync(artist, title);
            if (!string.IsNullOrWhiteSpace(ovhLyrics))
                return ovhLyrics;

            // 2. Fallback to Genius
            var geniusLyrics = await GetLyricsFromGeniusAsync(title, artist);
            return !string.IsNullOrWhiteSpace(geniusLyrics)
                ? geniusLyrics
                : "❌ No lyrics found from any source.";
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
            var query = HttpUtility.UrlEncode($"{title} {artist}");
            var geniusKey = _configuration["Genius:token"];
            var searchUrl = $"https://api.genius.com/search?q={query}";

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", geniusKey);

                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var hit = doc.RootElement
                    .GetProperty("response")
                    .GetProperty("hits")
                    .EnumerateArray()
                    .FirstOrDefault();

                if (hit.ValueKind == JsonValueKind.Undefined) return null;

                var url = hit.GetProperty("result").GetProperty("url").GetString();
                if (string.IsNullOrEmpty(url)) return null;

                // Step 2: Scrape lyrics from the page
                var html = await _httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                htmlDoc.LoadHtml(html);

                // Find the lyrics container
                var lyricsNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@data-lyrics-container='true']");
                if (lyricsNode == null) return null;

                var lyrics = string.Join("\n", lyricsNode.Descendants()
                    .Where(n => n.Name == "p" || n.Name == "br" || n.Name == "#text")
                    .Select(n => n.InnerText.Trim()));

                return string.IsNullOrWhiteSpace(lyrics) ? null : lyrics;
        }

        public async Task<LyricsModerationResult> ModerateLyricsAsync(string lyrics)
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";
            var url = $"{endpoint}?key={apiKey}";

            var prompt = new
            {
                contents = new[]
                {
            new {
                parts = new[] {
                    new {
                        text = $"Return only valid JSON like:\n" +
                               "{{ \"violence\": true/false, \"hate\": true/false, \"profanity\": true/false, \"sexual\": true/false, \"suitable_for_kids\": true/false }}\n\n" +
                               $"Lyrics:\n{lyrics}"
                    }
                },
                role = "user"
            }
        }
            };

            var requestJson = JsonSerializer.Serialize(prompt);
            var requestBody = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Gemini returned error: {response.StatusCode}");
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseString);
            var textResponse = doc
                .RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            // Try to isolate clean JSON from any extra text
            var jsonStart = textResponse?.IndexOf('{') ?? -1;
            var jsonEnd = textResponse?.LastIndexOf('}') ?? -1;

            if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
            {
                Console.WriteLine(" Could not extract clean JSON block.");
                return null;
            }

            var cleanJson = textResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

            try
            {
                var result = JsonSerializer.Deserialize<LyricsModerationResult>(cleanJson);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Deserialization failed: {ex.Message}");
                return null;
            }
        }
    }
}
