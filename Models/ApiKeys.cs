using System.Text.Json.Serialization;

namespace Alify.Models
{
    public class ApiKeys
    {
        public GeniusOptions? Genius { get; set; }
        public GeminiOptions? Gemini { get; set; }
        public OpenRouterOptions? OpenRouter { get; set; }
        public MistralOptions? Mistral { get; set; }
    }

    public class GeniusOptions
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    public class GeminiOptions
    {
        public string? ApiKey { get; set; }
    }

    public class OpenRouterOptions
    {
        public string? ApiKey { get; set; }
    }

    public class MistralOptions
    {
        public string? ApiKey { get; set; }
    }
}
