using Alify.Core.Features.Analysis.Models;

namespace Alify.Core.Features.Analysis.Policy;

/// <summary>
/// Deterministic, side-effect-free policy evaluator (ALI-018 / DATA-006 / PRD-009).
/// Produces Allow, RecommendSkip, or Abstain recommendations without executing playback commands.
/// Enforces strict architectural lane separation between official Spotify metadata and standalone lyrics.
/// </summary>
public static class DeterministicPolicyEvaluator
{
    public const string ContextStandaloneLyrics = "StandaloneLyrics";
    public const string ContextSpotifyExplicitMetadata = "SpotifyExplicitMetadata";

    /// <summary>
    /// Evaluates the official Spotify explicit metadata lane.
    /// Strictly side-effect free, requires explicit opt-in, and forbids semantic foreign keys or lyrics.
    /// </summary>
    public static PolicyEvaluation EvaluateSpotifyExplicit(bool? isExplicit, PolicyPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (!preferences.ExplicitFilterOptIn)
        {
            return new PolicyEvaluation
            {
                DecisionId = Guid.NewGuid().ToString("N"),
                Context = ContextSpotifyExplicitMetadata,
                Decision = PolicyDecision.Abstain,
                ReasonCodes = ["EXPLICIT_FILTER_OPT_OUT"],
                PreferenceRevision = preferences.PreferenceRevision,
                RulesVersion = preferences.RulesVersion
            };
        }

        if (isExplicit == null)
        {
            return new PolicyEvaluation
            {
                DecisionId = Guid.NewGuid().ToString("N"),
                Context = ContextSpotifyExplicitMetadata,
                Decision = PolicyDecision.Abstain,
                ReasonCodes = ["UNKNOWN_EXPLICIT_METADATA"],
                PreferenceRevision = preferences.PreferenceRevision,
                RulesVersion = preferences.RulesVersion
            };
        }

        if (isExplicit.Value)
        {
            return new PolicyEvaluation
            {
                DecisionId = Guid.NewGuid().ToString("N"),
                Context = ContextSpotifyExplicitMetadata,
                Decision = PolicyDecision.RecommendSkip,
                ReasonCodes = ["OFFICIAL_EXPLICIT_METADATA_FLAGGED"],
                PreferenceRevision = preferences.PreferenceRevision,
                RulesVersion = preferences.RulesVersion
            };
        }

        return new PolicyEvaluation
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            Context = ContextSpotifyExplicitMetadata,
            Decision = PolicyDecision.Allow,
            ReasonCodes = ["OFFICIAL_METADATA_CLEAN"],
            PreferenceRevision = preferences.PreferenceRevision,
            RulesVersion = preferences.RulesVersion
        };
    }

    /// <summary>
    /// Evaluates standalone semantic lyrics analysis against versioned user preferences and reviewer audits.
    /// Returns Allow, RecommendSkip, or Abstain. Standalone reports cannot drive Spotify.
    /// </summary>
    public static PolicyEvaluation EvaluateStandaloneLyrics(
        AnalysisResult analysis,
        PolicyPreferences preferences,
        IReadOnlyList<ReviewRecord>? reviews = null)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(preferences);

        // Rule (DATA-006): Incomplete, invalid, or unavailable analysis defaults to Abstain
        if (analysis.Status != AnalysisStatus.Completed)
        {
            return new PolicyEvaluation
            {
                DecisionId = Guid.NewGuid().ToString("N"),
                Context = ContextStandaloneLyrics,
                Decision = PolicyDecision.Abstain,
                ReasonCodes = [$"ANALYSIS_STATUS_{analysis.Status.ToString().ToUpperInvariant()}"],
                AnalysisId = analysis.AnalysisId,
                PreferenceRevision = preferences.PreferenceRevision,
                RulesVersion = preferences.RulesVersion
            };
        }

        // Rule (DATA-005): Build effective review map by finding ID (latest revision wins)
        var latestReviewsByFindingId = new Dictionary<string, ReviewRecord>(StringComparer.OrdinalIgnoreCase);
        if (reviews != null)
        {
            foreach (var review in reviews.OrderBy(r => r.Revision))
            {
                if (!string.IsNullOrEmpty(review.FindingId))
                {
                    latestReviewsByFindingId[review.FindingId] = review;
                }
            }
        }

        // Rule (DATA-005): Check for unresolved disagreements across findings
        var unresolvedReviews = latestReviewsByFindingId.Values
            .Where(r => r.Disposition == ReviewDisposition.Unresolved || r.Disposition == ReviewDisposition.NeedsContext)
            .ToList();

        if (unresolvedReviews.Count > 0)
        {
            return new PolicyEvaluation
            {
                DecisionId = Guid.NewGuid().ToString("N"),
                Context = ContextStandaloneLyrics,
                Decision = PolicyDecision.Abstain,
                ReasonCodes = ["UNRESOLVED_REVIEW_AUDIT"],
                AnalysisId = analysis.AnalysisId,
                PreferenceRevision = preferences.PreferenceRevision,
                RulesVersion = preferences.RulesVersion
            };
        }

        // Evaluate findings against trigger stances
        var triggeringReasons = new List<string>();

        foreach (var finding in analysis.Findings)
        {
            // If human reviewer marked Disagree on this finding, we suppress this specific finding
            if (latestReviewsByFindingId.TryGetValue(finding.FindingId, out var review) &&
                review.Disposition == ReviewDisposition.Disagree)
            {
                continue;
            }

            if (preferences.TriggerStancesByTopic.TryGetValue(finding.Topic, out var triggerStances))
            {
                if (triggerStances.Contains(finding.Stance))
                {
                    triggeringReasons.Add($"TRIGGER_{finding.Topic.ToUpperInvariant()}_{finding.Stance.ToString().ToUpperInvariant()}");
                }
            }
        }

        if (triggeringReasons.Count > 0)
        {
            return new PolicyEvaluation
            {
                DecisionId = Guid.NewGuid().ToString("N"),
                Context = ContextStandaloneLyrics,
                Decision = PolicyDecision.RecommendSkip,
                ReasonCodes = triggeringReasons,
                AnalysisId = analysis.AnalysisId,
                PreferenceRevision = preferences.PreferenceRevision,
                RulesVersion = preferences.RulesVersion
            };
        }

        return new PolicyEvaluation
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            Context = ContextStandaloneLyrics,
            Decision = PolicyDecision.Allow,
            ReasonCodes = ["NO_TRIGGERING_STANCE_FOUND"],
            AnalysisId = analysis.AnalysisId,
            PreferenceRevision = preferences.PreferenceRevision,
            RulesVersion = preferences.RulesVersion
        };
    }
}
