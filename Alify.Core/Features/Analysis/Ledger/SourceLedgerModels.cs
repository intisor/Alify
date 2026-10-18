namespace Alify.Core.Features.Analysis.Ledger;

/// <summary>
/// Canonical kind of logical source line in the immutable ledger (DATA-003).
/// </summary>
public enum SourceLineKind
{
    Lyric,
    Blank,
    Section,
    Metadata
}

/// <summary>
/// Immutable representation of a single logical line in a lyric source version (DATA-003).
/// </summary>
public record SourceLine
{
    public required int LineNumber { get; init; } // 1-based stable logical line index
    public required string RawText { get; init; }
    public required string CanonicalText { get; init; }
    public required SourceLineKind Kind { get; init; }
    public string? SectionName { get; init; }
    public string? ArtistAnnotation { get; init; }
    public string? LineHash { get; init; } // SHA-256 of CanonicalText
}

/// <summary>
/// Immutable version of a lyric source in the ledger (DATA-003).
/// Edits create a new SourceVersion referencing parentVersionId without mutating past versions.
/// </summary>
public record SourceVersion
{
    public required string SourceId { get; init; }
    public required string SourceVersionId { get; init; }
    public string? ParentVersionId { get; init; }
    public required string RawContentHash { get; init; } // SHA-256 of original raw bytes/text
    public required string CanonicalContentHash { get; init; } // SHA-256 of normalized LF text
    public string NormalizerVersion { get; init; } = "1.0.0";
    public required IReadOnlyList<SourceLine> Lines { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public string GetCanonicalText() =>
        string.Join('\n', Lines.Select(l => l.CanonicalText));

    public string GetCanonicalEvidenceExcerpt(int startLine, int endLine)
    {
        if (startLine < 1 || endLine > Lines.Count || startLine > endLine)
        {
            throw new ArgumentOutOfRangeException($"Invalid line range [{startLine}..{endLine}] for source with {Lines.Count} lines.");
        }

        var slice = Lines.Skip(startLine - 1).Take(endLine - startLine + 1);
        return string.Join('\n', slice.Select(l => l.CanonicalText));
    }
}
