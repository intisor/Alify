namespace Alify.Core.Features.Analysis.Models;

/// <summary>
/// Terminal resolution status for permissioned lyric import or lookup (DATA-002).
/// </summary>
public enum LyricResolution
{
    Found,
    NotFound,
    Instrumental,
    Mismatch,
    ProviderUnavailable
}

/// <summary>
/// Terminal status of semantic interpretation (DATA-004).
/// </summary>
public enum AnalysisStatus
{
    Completed,
    InsufficientContext,
    UnsupportedLanguage,
    Unavailable,
    InvalidOutput
}

/// <summary>
/// Evaluated stance toward an interpreted topic (DATA-004).
/// </summary>
public enum Stance
{
    Endorsed,
    Mentioned,
    Rejected,
    Ambiguous
}

/// <summary>
/// Evaluated mode of expression for a finding (DATA-004).
/// </summary>
public enum ExpressionType
{
    Literal,
    Figurative,
    Unknown
}

/// <summary>
/// Recommendation produced by deterministic policy evaluation (DATA-006).
/// Note: Separate from analysis and separate from command dispatch.
/// </summary>
public enum PolicyDecision
{
    Allow,
    RecommendSkip,
    Abstain
}

/// <summary>
/// Human reviewer audit disposition (DATA-005).
/// </summary>
public enum ReviewDisposition
{
    Agree,
    Disagree,
    NeedsContext,
    Unresolved
}
