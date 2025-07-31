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

    /// <summary>
    /// Represents the result of lyrics content moderation.
    /// Used for checking lyrics appropriateness and content filtering.
    /// This supports Method Injection pattern for moderation services.
    /// </summary>
    [DebuggerDisplay("Suitable for Kids: {suitable_for_kids}, Violence: {violence}, Hate: {hate}, Sexual: {sexual}, Profanity: {profanity}")]
    public class LyricsModerationResult
    {
        /// <summary>
        /// Indicates if the lyrics are suitable for children/kids
        /// </summary>
        public bool suitable_for_kids { get; set; } = true;

        /// <summary>
        /// Indicates if the lyrics contain violent content
        /// </summary>
        public bool violence { get; set; } = false;

        /// <summary>
        /// Indicates if the lyrics contain hate speech or discriminatory content
        /// </summary>
        public bool hate { get; set; } = false;

        /// <summary>
        /// Indicates if the lyrics contain sexual content
        /// </summary>
        public bool sexual { get; set; } = false;

        /// <summary>
        /// Indicates if the lyrics contain profanity or explicit language
        /// </summary>
        public bool profanity { get; set; } = false;

        /// <summary>
        /// Overall confidence score of the moderation results (0.0 to 1.0)
        /// </summary>
        public double confidence { get; set; } = 1.0;

        /// <summary>
        /// Additional context or reason for the moderation results
        /// </summary>
        public string? context { get; set; }

        /// <summary>
        /// Timestamp when the moderation was performed
        /// </summary>
        public DateTime moderatedAt { get; set; } = DateTime.UtcNow;
    }
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
}
