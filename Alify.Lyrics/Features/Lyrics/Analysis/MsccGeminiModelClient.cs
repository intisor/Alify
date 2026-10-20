using Alify.Core.Features.Analysis.Engine.Gemini;
using Mscc.GenerativeAI;

namespace Alify.Lyrics.Features.Lyrics.Analysis;

/// <summary>
/// Production Gemini model client implementing IGeminiModelClient via Mscc.GenerativeAI SDK (ALI-020).
/// Does not fall back to other providers or retry uncertain charges silently.
/// </summary>
public class MsccGeminiModelClient : IGeminiModelClient
{
    private readonly string _apiKey;
    public string ModelName { get; }

    public MsccGeminiModelClient(string apiKey, string modelName = "gemini-2.5-flash")
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key cannot be empty.", nameof(apiKey));
        _apiKey = apiKey;
        ModelName = string.IsNullOrWhiteSpace(modelName) ? "gemini-2.5-flash" : modelName;
    }

    public async Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var googleAi = new GoogleAI(_apiKey);
        var model = googleAi.GenerativeModel(model: ModelName);
        var response = await model.GenerateContent(prompt, cancellationToken: cancellationToken);
        return response?.Text ?? string.Empty;
    }
}
