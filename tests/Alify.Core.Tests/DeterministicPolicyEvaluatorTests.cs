using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Analysis.Policy;
using Xunit;

namespace Alify.Core.Tests;

public class DeterministicPolicyEvaluatorTests
{
    [Fact]
    public void SpotifyExplicit_WhenOptedOut_ReturnsAbstain()
    {
        var prefs = new PolicyPreferences { ExplicitFilterOptIn = false };

        var evaluation = DeterministicPolicyEvaluator.EvaluateSpotifyExplicit(true, prefs);

        Assert.Equal(PolicyDecision.Abstain, evaluation.Decision);
        Assert.Equal(DeterministicPolicyEvaluator.ContextSpotifyExplicitMetadata, evaluation.Context);
        Assert.Contains("EXPLICIT_FILTER_OPT_OUT", evaluation.ReasonCodes);
    }

    [Fact]
    public void SpotifyExplicit_WhenUnknownExplicitFlag_ReturnsAbstain()
    {
        var prefs = new PolicyPreferences { ExplicitFilterOptIn = true };

        var evaluation = DeterministicPolicyEvaluator.EvaluateSpotifyExplicit(null, prefs);

        Assert.Equal(PolicyDecision.Abstain, evaluation.Decision);
        Assert.Contains("UNKNOWN_EXPLICIT_METADATA", evaluation.ReasonCodes);
    }

    [Fact]
    public void SpotifyExplicit_WhenFlaggedExplicit_ReturnsRecommendSkip()
    {
        var prefs = new PolicyPreferences { ExplicitFilterOptIn = true };

        var evaluation = DeterministicPolicyEvaluator.EvaluateSpotifyExplicit(true, prefs);

        Assert.Equal(PolicyDecision.RecommendSkip, evaluation.Decision);
        Assert.Contains("OFFICIAL_EXPLICIT_METADATA_FLAGGED", evaluation.ReasonCodes);
    }

    [Fact]
    public void SpotifyExplicit_WhenCleanMetadata_ReturnsAllow()
    {
        var prefs = new PolicyPreferences { ExplicitFilterOptIn = true };

        var evaluation = DeterministicPolicyEvaluator.EvaluateSpotifyExplicit(false, prefs);

        Assert.Equal(PolicyDecision.Allow, evaluation.Decision);
        Assert.Contains("OFFICIAL_METADATA_CLEAN", evaluation.ReasonCodes);
    }

    [Fact]
    public void StandaloneLyrics_WhenAnalysisNotCompleted_ReturnsAbstain()
    {
        var prefs = PolicyPreferences.Default;
        var analysis = new AnalysisResult
        {
            AnalysisId = "an-unsupported",
            SourceId = "src-01",
            Status = AnalysisStatus.UnsupportedLanguage,
            EngineKind = "HeuristicStanceEngine",
            Language = "es"
        };

        var evaluation = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(analysis, prefs);

        Assert.Equal(PolicyDecision.Abstain, evaluation.Decision);
        Assert.Contains("ANALYSIS_STATUS_UNSUPPORTEDLANGUAGE", evaluation.ReasonCodes);
        Assert.Equal("an-unsupported", evaluation.AnalysisId);
    }

    [Fact]
    public void StandaloneLyrics_WhenEndorsedViolence_ReturnsRecommendSkip()
    {
        var prefs = PolicyPreferences.Default;
        var analysis = new AnalysisResult
        {
            AnalysisId = "an-violence",
            SourceId = "src-01",
            Status = AnalysisStatus.Completed,
            EngineKind = "HeuristicStanceEngine",
            Findings = new List<Finding>
            {
                new()
                {
                    FindingId = "f-1",
                    Topic = "violence",
                    Stance = Stance.Endorsed,
                    Expression = ExpressionType.Literal,
                    Rationale = "Endorses violence"
                }
            }
        };

        var evaluation = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(analysis, prefs);

        Assert.Equal(PolicyDecision.RecommendSkip, evaluation.Decision);
        Assert.Contains("TRIGGER_VIOLENCE_ENDORSED", evaluation.ReasonCodes);
    }

    [Fact]
    public void StandaloneLyrics_WhenRejectedViolence_ReturnsAllow()
    {
        var prefs = PolicyPreferences.Default;
        var analysis = new AnalysisResult
        {
            AnalysisId = "an-peace",
            SourceId = "src-01",
            Status = AnalysisStatus.Completed,
            EngineKind = "HeuristicStanceEngine",
            Findings = new List<Finding>
            {
                new()
                {
                    FindingId = "f-1",
                    Topic = "violence",
                    Stance = Stance.Rejected,
                    Expression = ExpressionType.Literal,
                    Rationale = "Explicitly rejects violence"
                }
            }
        };

        var evaluation = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(analysis, prefs);

        Assert.Equal(PolicyDecision.Allow, evaluation.Decision);
        Assert.Contains("NO_TRIGGERING_STANCE_FOUND", evaluation.ReasonCodes);
    }

    [Fact]
    public void StandaloneLyrics_WhenHumanReviewerDisagreesWithFlag_ReturnsAllow()
    {
        var prefs = PolicyPreferences.Default;
        var analysis = new AnalysisResult
        {
            AnalysisId = "an-disputed",
            SourceId = "src-01",
            Status = AnalysisStatus.Completed,
            EngineKind = "HeuristicStanceEngine",
            Findings = new List<Finding>
            {
                new()
                {
                    FindingId = "f-1",
                    Topic = "violence",
                    Stance = Stance.Endorsed,
                    Expression = ExpressionType.Literal,
                    Rationale = "Literal match"
                }
            }
        };

        var reviews = new List<ReviewRecord>
        {
            new()
            {
                ReviewId = "rev-1",
                AnalysisId = "an-disputed",
                FindingId = "f-1",
                ReviewerId = "local-auditor",
                Revision = 1,
                Disposition = ReviewDisposition.Disagree,
                Rationale = "Character monologue in theater piece, not actual endorsement"
            }
        };

        var evaluation = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(analysis, prefs, reviews);

        Assert.Equal(PolicyDecision.Allow, evaluation.Decision);
        Assert.Contains("NO_TRIGGERING_STANCE_FOUND", evaluation.ReasonCodes);
    }

    [Fact]
    public void StandaloneLyrics_WhenUnresolvedAudit_ReturnsAbstain()
    {
        var prefs = PolicyPreferences.Default;
        var analysis = new AnalysisResult
        {
            AnalysisId = "an-unresolved",
            SourceId = "src-01",
            Status = AnalysisStatus.Completed,
            EngineKind = "HeuristicStanceEngine",
            Findings = new List<Finding>
            {
                new()
                {
                    FindingId = "f-1",
                    Topic = "violence",
                    Stance = Stance.Endorsed,
                    Expression = ExpressionType.Literal,
                    Rationale = "Possible endorsement"
                }
            }
        };

        var reviews = new List<ReviewRecord>
        {
            new()
            {
                ReviewId = "rev-1",
                AnalysisId = "an-unresolved",
                FindingId = "f-1",
                ReviewerId = "local-auditor",
                Revision = 1,
                Disposition = ReviewDisposition.Unresolved,
                Rationale = "Ambiguous cultural idiom requiring expert consultation"
            }
        };

        var evaluation = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(analysis, prefs, reviews);

        Assert.Equal(PolicyDecision.Abstain, evaluation.Decision);
        Assert.Contains("UNRESOLVED_REVIEW_AUDIT", evaluation.ReasonCodes);
    }
}
