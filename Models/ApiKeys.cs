using System.Text.Json.Serialization;

namespace Alify.Models
{
    public class ApiKeys
    {
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public GeniusOptions? Genius { get; set; }
        public GeminiOptions? Gemini { get; set; }
        public OpenRouterOptions? OpenRouter { get; set; }
        public MistralOptions? Mistral { get; set; }
        public SpotifyOptions? Spotify { get; set; }
    }

    public class SpotifyOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? RedirectUri { get; set; }
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
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
}
