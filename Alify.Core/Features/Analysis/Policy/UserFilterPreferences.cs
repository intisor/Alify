using Alify.Core.Features.Analysis.Models;

namespace Alify.Core.Features.Analysis.Policy;

/// <summary>
/// Worldview / Ethos profile for personalized lyrical filtering.
/// </summary>
public enum FilterProfile
{
    General = 0,
    Islamic = 1,
    Christian = 2,
    FamilyFriendly = 3,
    Custom = 4
}

/// <summary>
/// User-configurable filter preferences for active Spotify playback and queue evaluation.
/// Enables specifying worldview ethos (Islamic, Christian, Family-Friendly, General, Custom)
/// and a strictness level from 1 to 10 with Gemini 2.5 LLM semantic verification.
/// </summary>
public record UserFilterPreferences
{
    /// <summary>
    /// Selected content worldview ethos profile.
    /// </summary>
    public FilterProfile Profile { get; init; } = FilterProfile.General;

    /// <summary>
    /// Strictness level from 1 (Most Lenient) to 10 (Strict Zero-Tolerance).
    /// Default is 5.
    /// At level >= 7, even narrative storytelling, casual mentions, and incidental references trigger skips.
    /// </summary>
    public int StrictnessLevel { get; init; } = 5;

    /// <summary>
    /// Whether to utilize Google Gemini 2.5 Flash for deep semantic verification.
    /// </summary>
    public bool EnableGeminiLLM { get; init; } = true;

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
    /// Applies profile ethos rules and strictness scaling (1-10).
    /// </summary>
    public PolicyPreferences ToPolicyPreferences()
    {
        var triggerStances = new Dictionary<string, HashSet<Stance>>(StringComparer.OrdinalIgnoreCase);

        // Strictness scaling: level >= 7 triggers on mentions/incidental references
        bool strictMentions = StrictnessLevel >= 7;

        void AddTrigger(string topic, bool includeMentioned = false)
        {
            var set = new HashSet<Stance> { Stance.Endorsed };
            if (includeMentioned || strictMentions)
            {
                set.Add(Stance.Mentioned);
            }
            triggerStances[topic] = set;
        }

        switch (Profile)
        {
            case FilterProfile.Islamic:
                // Halal ethos: strict against shirk/blasphemy, intoxicants/khamr, explicit sexuality/fahisha, vulgarity, violence
                AddTrigger("Blasphemy", strictMentions);
                AddTrigger("blasphemy", strictMentions);
                AddTrigger("SubstanceUse", strictMentions);
                AddTrigger("substance_use", strictMentions);
                AddTrigger("SexualContent", strictMentions);
                AddTrigger("sexual_content", strictMentions);
                AddTrigger("Vulgarity", strictMentions);
                AddTrigger("profanity", strictMentions);
                AddTrigger("Violence", strictMentions);
                AddTrigger("violence", strictMentions);
                AddTrigger("Occult", strictMentions);
                AddTrigger("occult", strictMentions);
                break;

            case FilterProfile.Christian:
                // Biblical ethos: strict against taking God's name in vain/blasphemy, demonic/occult, sexual immorality, vulgarity, violence
                AddTrigger("Blasphemy", strictMentions);
                AddTrigger("blasphemy", strictMentions);
                AddTrigger("Occult", strictMentions);
                AddTrigger("occult", strictMentions);
                AddTrigger("SexualContent", strictMentions);
                AddTrigger("sexual_content", strictMentions);
                AddTrigger("Vulgarity", strictMentions);
                AddTrigger("profanity", strictMentions);
                AddTrigger("Violence", strictMentions);
                AddTrigger("violence", strictMentions);
                AddTrigger("SubstanceUse", strictMentions);
                AddTrigger("substance_use", strictMentions);
                break;

            case FilterProfile.FamilyFriendly:
                // Child & family safety: no vulgarity, no explicit, no drugs, no violence
                AddTrigger("Violence", strictMentions);
                AddTrigger("violence", strictMentions);
                AddTrigger("SubstanceUse", strictMentions);
                AddTrigger("substance_use", strictMentions);
                AddTrigger("SexualContent", strictMentions);
                AddTrigger("sexual_content", strictMentions);
                AddTrigger("Vulgarity", strictMentions);
                AddTrigger("profanity", strictMentions);
                break;

            case FilterProfile.General:
            case FilterProfile.Custom:
            default:
                var violenceSet = new HashSet<Stance>();
                if (FilterViolenceEndorsed) violenceSet.Add(Stance.Endorsed);
                if (FilterViolenceMentioned || strictMentions) violenceSet.Add(Stance.Mentioned);
                if (violenceSet.Count > 0)
                {
                    triggerStances["Violence"] = violenceSet;
                    triggerStances["violence"] = violenceSet;
                }

                var substanceSet = new HashSet<Stance>();
                if (FilterSubstanceEndorsed) substanceSet.Add(Stance.Endorsed);
                if (FilterSubstanceMentioned || strictMentions) substanceSet.Add(Stance.Mentioned);
                if (substanceSet.Count > 0)
                {
                    triggerStances["SubstanceUse"] = substanceSet;
                    triggerStances["substance_use"] = substanceSet;
                }
                break;
        }

        return new PolicyPreferences
        {
            ExplicitFilterOptIn = FilterSpotifyExplicit || Profile != FilterProfile.General,
            TriggerStancesByTopic = triggerStances
        };
    }
}
