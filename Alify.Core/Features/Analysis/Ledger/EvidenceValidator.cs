using Alify.Core.Features.Analysis.Models;

namespace Alify.Core.Features.Analysis.Ledger;

public record EvidenceValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static EvidenceValidationResult Success() => new() { IsValid = true };
    public static EvidenceValidationResult Fail(string error) => new() { IsValid = false, ErrorMessage = error };
}

/// <summary>
/// Strict evidence validator that verifies citations against the raw immutable ledger (ALI-018 / DATA-003).
/// Rejects fabricated excerpts, invalid line ranges, and non-lyric-only citations.
/// </summary>
public static class EvidenceValidator
{
    /// <summary>
    /// Validates a single evidence reference against a specific immutable source version.
    /// </summary>
    public static EvidenceValidationResult ValidateCitation(EvidenceReference evidence, SourceVersion source)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(source);

        if (evidence.StartLine < 1)
        {
            return EvidenceValidationResult.Fail($"Invalid StartLine: {evidence.StartLine}. Line numbering is 1-based.");
        }

        if (evidence.EndLine > source.Lines.Count)
        {
            return EvidenceValidationResult.Fail($"EndLine {evidence.EndLine} exceeds document length ({source.Lines.Count} lines).");
        }

        if (evidence.StartLine > evidence.EndLine)
        {
            return EvidenceValidationResult.Fail($"StartLine {evidence.StartLine} cannot be greater than EndLine {evidence.EndLine}.");
        }

        var referencedLines = source.Lines
            .Skip(evidence.StartLine - 1)
            .Take(evidence.EndLine - evidence.StartLine + 1)
            .ToList();

        // Rule (DATA-003): Citation to a non-lyric/header line alone cannot substantiate a lyric stance claim
        bool hasActualLyricLine = referencedLines.Any(l => l.Kind == SourceLineKind.Lyric);
        if (!hasActualLyricLine)
        {
            return EvidenceValidationResult.Fail($"Referenced line range [{evidence.StartLine}..{evidence.EndLine}] contains only section headers or blank lines, not sung lyrics.");
        }

        // Rule (DATA-003): QuotedText must match the LF-joined canonical text of the referenced slice exactly
        string expectedText = string.Join('\n', referencedLines.Select(l => l.CanonicalText));
        if (!string.Equals(expectedText, evidence.QuotedText, StringComparison.Ordinal))
        {
            return EvidenceValidationResult.Fail($"Quoted excerpt does not match the canonical ledger text at lines [{evidence.StartLine}..{evidence.EndLine}]. Fabricated citation rejected.");
        }

        // Optional hash verification
        if (!string.IsNullOrEmpty(evidence.LineHash) && evidence.StartLine == evidence.EndLine)
        {
            var targetLine = referencedLines[0];
            if (!string.Equals(targetLine.LineHash, evidence.LineHash, StringComparison.OrdinalIgnoreCase))
            {
                return EvidenceValidationResult.Fail($"Line hash mismatch for line {evidence.StartLine}. Expected {targetLine.LineHash}, received {evidence.LineHash}.");
            }
        }

        return EvidenceValidationResult.Success();
    }

    /// <summary>
    /// Validates all findings in an AnalysisResult against the source version.
    /// Rejects any finding that makes a stance claim without valid, verified citations.
    /// </summary>
    public static EvidenceValidationResult ValidateAnalysis(AnalysisResult analysis, SourceVersion source)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(source);

        if (analysis.Status != AnalysisStatus.Completed)
        {
            // Non-completed analysis does not require evidence validation
            return EvidenceValidationResult.Success();
        }

        foreach (var finding in analysis.Findings)
        {
            // Rule: Stance claims (Endorsed, Mentioned, Rejected) require at least one verified citation
            if (finding.Stance != Stance.Ambiguous && (finding.Evidence == null || finding.Evidence.Count == 0))
            {
                return EvidenceValidationResult.Fail($"Finding '{finding.FindingId}' claims stance '{finding.Stance}' on topic '{finding.Topic}' without any supporting evidence citations.");
            }

            if (finding.Evidence != null)
            {
                foreach (var citation in finding.Evidence)
                {
                    var citationResult = ValidateCitation(citation, source);
                    if (!citationResult.IsValid)
                    {
                        return EvidenceValidationResult.Fail($"Finding '{finding.FindingId}' has invalid citation: {citationResult.ErrorMessage}");
                    }
                }
            }
        }

        return EvidenceValidationResult.Success();
    }
}
