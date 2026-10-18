namespace Alify.Core.Features.Analysis.Models;

/// <summary>
/// Verifiable evidence citation pointing to exact lines and hashes in an immutable source ledger (DATA-003 / DATA-004).
/// </summary>
public record EvidenceReference
{
    public required int StartLine { get; init; }
    public required int EndLine { get; init; }
    public required string QuotedText { get; init; }
    public string? LineHash { get; init; }
}

/// <summary>
/// Specific stance finding for an interpreted topic backed by verifiable evidence (DATA-004).
/// </summary>
public record Finding
{
    public required string FindingId { get; init; }
    public required string Topic { get; init; }
    public required Stance Stance { get; init; }
    public required ExpressionType Expression { get; init; }
    public required string Rationale { get; init; }
    public IReadOnlyList<EvidenceReference> Evidence { get; init; } = [];
    public double? Confidence { get; init; }
    public string? Uncertainty { get; init; }
}

/// <summary>
/// Neutral semantic analysis output describing meaning and uncertainty (DATA-004).
/// Crucial Rule: Analysis results MUST NOT contain any playback action, ShouldSkip flag, or Spotify SDK dependency.
/// </summary>
public record AnalysisResult
{
    public required string AnalysisId { get; init; }
    public required string SourceId { get; init; }
    public string? SourceVersionId { get; init; }
    public required AnalysisStatus Status { get; init; }
    public required string EngineKind { get; init; }
    public string? Language { get; init; }
    public string? ReasonCode { get; init; }
    public IReadOnlyList<Finding> Findings { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = [];
    public DateTime ExecutedAtUtc { get; init; } = DateTime.UtcNow;

    public static AnalysisResult CreateUnavailable(string sourceId, string engineKind, string reasonCode) =>
        new()
        {
            AnalysisId = Guid.NewGuid().ToString("N"),
            SourceId = sourceId,
            Status = AnalysisStatus.Unavailable,
            EngineKind = engineKind,
            ReasonCode = reasonCode
        };

    public static AnalysisResult CreateUnsupportedLanguage(string sourceId, string engineKind, string language) =>
        new()
        {
            AnalysisId = Guid.NewGuid().ToString("N"),
            SourceId = sourceId,
            Status = AnalysisStatus.UnsupportedLanguage,
            EngineKind = engineKind,
            Language = language,
            ReasonCode = "LANGUAGE_NOT_SUPPORTED"
        };
}
