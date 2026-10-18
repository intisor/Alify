using Alify.Core.Features.Analysis.Models;

namespace Alify.Core.Features.Analysis.Policy;

/// <summary>
/// User preference set controlling deterministic policy recommendations (DATA-006 / PRD-009).
/// Versioned separately from analysis findings and rules.
/// </summary>
public record PolicyPreferences
{
    public string PreferenceRevision { get; init; } = "pref-default-v1";
    public string RulesVersion { get; init; } = "2026.1";

    /// <summary>
    /// Whether explicit filtering in Spotify observation lane is enabled by the user.
    /// Default is false (explicit opt-in required).
    /// </summary>
    public bool ExplicitFilterOptIn { get; init; } = false;

    /// <summary>
    /// Topics and their stances that trigger a RecommendSkip recommendation in the standalone lyrics lane.
    /// Example: { "violence": [Stance.Endorsed], "substance_use": [Stance.Endorsed] }
    /// </summary>
    public Dictionary<string, HashSet<Stance>> TriggerStancesByTopic { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["violence"] = new() { Stance.Endorsed },
        ["substance_use"] = new() { Stance.Endorsed }
    };

    public static PolicyPreferences Default => new();
}
