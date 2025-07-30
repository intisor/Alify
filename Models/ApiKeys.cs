using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Alify.Models
{
    [DebuggerDisplay("Genius: {Genius?.Token != null ? \"Configured\" : \"Not configured\"}, Gemini: {Gemini?.ApiKey != null ? \"Configured\" : \"Not configured\"}, OpenRouter: {OpenRouter?.ApiKey != null ? \"Configured\" : \"Not configured\"}, Mistral: {Mistral?.ApiKey != null ? \"Configured\" : \"Not configured\"}")]
    public class ApiKeys
    {
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public GeniusOptions? Genius { get; set; }
        public GeminiOptions? Gemini { get; set; }
        public OpenRouterOptions? OpenRouter { get; set; }
        public MistralOptions? Mistral { get; set; }
    }

    [DebuggerDisplay("ClientId: {ClientId}, RedirectUri: {RedirectUri}")]
    public class SpotifyOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? RedirectUri { get; set; }
    }

    [DebuggerDisplay("Token: {Token != null ? \"Set\" : \"Not set\"}")]
    public class GeniusOptions
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    [DebuggerDisplay("ApiKey: {ApiKey != null ? \"Set\" : \"Not set\"}")]
    public class GeminiOptions
    {
        public string? ApiKey { get; set; }
    }

    [DebuggerDisplay("ApiKey: {ApiKey != null ? \"Set\" : \"Not set\"}")]
    public class OpenRouterOptions
    {
        public string? ApiKey { get; set; }
    }

    [DebuggerDisplay("ApiKey: {ApiKey != null ? \"Set\" : \"Not set\"}")]
    public class MistralOptions
    {
        public string? ApiKey { get; set; }
    }
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
}
