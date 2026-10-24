using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Alify.Core.Features.Analysis.Engine.Gemini;

/// <summary>
/// Universal HTTP REST client for OpenAI-compatible LLM endpoints (OpenRouter, Groq, OpenAI, Mistral, DeepSeek, Local Ollama, LM Studio).
/// Enables BYOK with any model or provider using standard chat completion semantics.
/// </summary>
public class OpenAiCompatibleModelClient : IGeminiModelClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _endpointUrl;
    public string ModelName { get; }

    public const string DefaultOpenRouterEndpoint = "https://openrouter.ai/api/v1/chat/completions";
    public const string DefaultGroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    public const string DefaultOpenAiEndpoint = "https://api.openai.com/v1/chat/completions";
    public const string DefaultOllamaEndpoint = "http://localhost:11434/v1/chat/completions";

    public OpenAiCompatibleModelClient(
        HttpClient httpClient,
        string? apiKey = null,
        string modelName = "meta-llama/llama-3.3-70b-instruct:free",
        string endpointUrl = DefaultOpenRouterEndpoint)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey;
        ModelName = string.IsNullOrWhiteSpace(modelName) ? "meta-llama/llama-3.3-70b-instruct:free" : modelName;
        _endpointUrl = string.IsNullOrWhiteSpace(endpointUrl) ? DefaultOpenRouterEndpoint : endpointUrl;
    }

    public async Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var payload = new
        {
            model = ModelName,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.1
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey.Trim());
        }

        // Add optional OpenRouter attribution headers
        if (_endpointUrl.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/intisor/Alify");
            request.Headers.TryAddWithoutValidation("X-Title", "Alify Music Filter");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (doc.RootElement.TryGetProperty("choices", out var choices) &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var contentElem))
        {
            return contentElem.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
