using System.Text;
using System.Text.Json;

namespace Alify.Core.Features.Analysis.Engine.Gemini;

/// <summary>
/// Direct HTTP REST client for Google Gemini 2.5 models (ALI-020).
/// Communicates directly with generativelanguage.googleapis.com without third-party SDK dependencies.
/// </summary>
public class HttpGeminiModelClient : IGeminiModelClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    public string ModelName { get; }

    public HttpGeminiModelClient(HttpClient httpClient, string apiKey, string modelName = "gemini-2.5-flash")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : throw new ArgumentException("API key cannot be empty.", nameof(apiKey));
        ModelName = string.IsNullOrWhiteSpace(modelName) ? "gemini-2.5-flash" : modelName;
    }

    public async Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent?key={_apiKey}";
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
            candidates.GetArrayLength() > 0 &&
            candidates[0].TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) &&
            parts.GetArrayLength() > 0 &&
            parts[0].TryGetProperty("text", out var textElem))
        {
            return textElem.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
