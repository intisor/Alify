using Alify.Core.Features.Analysis.Models;

namespace Alify.Core.Features.Analysis.Policy;

/// <summary>
/// User-configurable filter preferences for active Spotify playback and queue evaluation.
/// Enables specifying what content the user wants to hear vs what should be skipped.
/// </summary>
public record UserFilterPreferences
{
    /// <summary>
    /// Whether to skip tracks marked as Explicit in Spotify's official metadata.
    /// </summary>
    public bool FilterSpotifyExplicit { get; init; } = true;

    /// <summary>
    /// Whether to skip tracks promoting or glorifying violence, weapons, or killing.
    /// </summary>
    public bool FilterViolenceEndorsed { get; init; } = true;

    /// <summary>
    /// Whether to skip tracks that mention or reference violence in storytelling/news.
    /// </summary>
    public bool FilterViolenceMentioned { get; init; } = false;

    /// <summary>
    /// Whether to skip tracks promoting or glorifying drug use, pills, or narcotics.
    /// </summary>
    public bool FilterSubstanceEndorsed { get; init; } = true;

    /// <summary>
    /// Whether to skip tracks mentioning substances or alcohol references.
    /// </summary>
    public bool FilterSubstanceMentioned { get; init; } = false;

    /// <summary>
    /// Converts this user filter preference model into the deterministic PolicyPreferences representation.
    /// </summary>
    public PolicyPreferences ToPolicyPreferences()
    {
        var triggerStances = new Dictionary<string, HashSet<Stance>>(StringComparer.OrdinalIgnoreCase);

        var violenceSet = new HashSet<Stance>();
        if (FilterViolenceEndorsed) violenceSet.Add(Stance.Endorsed);
        if (FilterViolenceMentioned) violenceSet.Add(Stance.Mentioned);
        if (violenceSet.Count > 0) triggerStances["Violence"] = violenceSet;

        var substanceSet = new HashSet<Stance>();
        if (FilterSubstanceEndorsed) substanceSet.Add(Stance.Endorsed);
        if (FilterSubstanceMentioned) substanceSet.Add(Stance.Mentioned);
        if (substanceSet.Count > 0) triggerStances["SubstanceUse"] = substanceSet;

        return new PolicyPreferences
        {
            ExplicitFilterOptIn = FilterSpotifyExplicit,
            TriggerStancesByTopic = triggerStances
        };
    }
}
