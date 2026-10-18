namespace Alify.Core.Features.Analysis.Models;

/// <summary>
/// Immutable deterministic policy decision output (DATA-006).
/// Recommends an action based on preferences and findings without executing commands.
/// </summary>
public record PolicyEvaluation
{
    public required string DecisionId { get; init; }
    public required string Context { get; init; } // e.g., "StandaloneLyrics" or "SpotifyExplicitMetadata"
    public required PolicyDecision Decision { get; init; }
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
    public string? AnalysisId { get; init; }
    public string? PreferenceRevision { get; init; }
    public string? RulesVersion { get; init; }
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;

    public static PolicyEvaluation Allow(string context, string? analysisId = null, string? reason = null) =>
        new()
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            Context = context,
            Decision = PolicyDecision.Allow,
            AnalysisId = analysisId,
            ReasonCodes = reason != null ? [reason] : []
        };

    public static PolicyEvaluation RecommendSkip(string context, string reason, string? analysisId = null) =>
        new()
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            Context = context,
            Decision = PolicyDecision.RecommendSkip,
            AnalysisId = analysisId,
            ReasonCodes = [reason]
        };

    public static PolicyEvaluation Abstain(string context, string reason, string? analysisId = null) =>
        new()
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            Context = context,
            Decision = PolicyDecision.Abstain,
            AnalysisId = analysisId,
            ReasonCodes = [reason]
        };
}

/// <summary>
/// Append-only record of a human reviewer auditing or correcting an analysis finding (DATA-005).
/// </summary>
public record ReviewRecord
{
    public required string ReviewId { get; init; }
    public required string AnalysisId { get; init; }
    public string? FindingId { get; init; }
    public required string ReviewerId { get; init; }
    public int Revision { get; init; } = 1;
    public required ReviewDisposition Disposition { get; init; }
    public required string Rationale { get; init; }
    public IReadOnlyList<EvidenceReference> AlternateEvidence { get; init; } = [];
    public string? SupersedesReviewId { get; init; }
    public DateTime ReviewedAtUtc { get; init; } = DateTime.UtcNow;
}
