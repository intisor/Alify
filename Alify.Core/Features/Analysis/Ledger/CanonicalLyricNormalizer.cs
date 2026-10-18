using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Alify.Core.Features.Analysis.Ledger;

/// <summary>
/// Normalizes raw lyrics into immutable, CRLF-safe logical lines with cryptographic hashes (ALI-015 / DATA-003).
/// </summary>
public partial class CanonicalLyricNormalizer
{
    public const string CurrentVersion = "1.0.0";

    // Matches section headers: [Verse 1], [Chorus: Artist], [Bridge], [Instrumental], etc.
    [GeneratedRegex(@"^\[(?<section>Verse|Chorus|Bridge|Pre-Chorus|Post-Chorus|Outro|Intro|Hook|Refrain|Instrumental(?:\s+Break)?|Solo|Interlude|Break)(?:\s*(?<num>\d+))?(?:\s*:\s*(?<artist>[^\]]+))?\]$", RegexOptions.IgnoreCase)]
    private static partial Regex SectionRegex();

    // Matches standard metadata tags: [ar: Artist], [ti: Title], etc.
    [GeneratedRegex(@"^\[(?<key>ar|ti|al|by|length|re|ve):(?<value>[^\]]*)\]$", RegexOptions.IgnoreCase)]
    private static partial Regex MetadataRegex();

    /// <summary>
    /// Normalizes raw lyric text into an immutable SourceVersion.
    /// Preserves exact 1-based line numbering across blanks, sections, and metadata.
    /// Converts all newlines to LF canonically without altering line counts.
    /// </summary>
    public SourceVersion Normalize(string rawText, string? sourceId = null, string? parentVersionId = null)
    {
        ArgumentNullException.ThrowIfNull(rawText);

        sourceId ??= Guid.NewGuid().ToString("N");
        var versionId = Guid.NewGuid().ToString("N");

        byte[] rawBytes = Encoding.UTF8.GetBytes(rawText);
        string rawHash = Convert.ToHexString(SHA256.HashData(rawBytes)).ToLowerInvariant();

        // Split lines handling CRLF, CR, LF accurately
        var rawLines = SplitLogicalLines(rawText);
        var sourceLines = new List<SourceLine>(rawLines.Count);

        string? currentSection = null;
        string? currentArtist = null;

        for (int i = 0; i < rawLines.Count; i++)
        {
            int lineNumber = i + 1; // 1-based stable line ID
            string rawLine = rawLines[i];
            string trimmed = rawLine.Trim();

            SourceLineKind kind;
            string? sectionName = currentSection;
            string? artistAnnotation = currentArtist;

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                kind = SourceLineKind.Blank;
            }
            else
            {
                var sectionMatch = SectionRegex().Match(trimmed);
                if (sectionMatch.Success)
                {
                    kind = SourceLineKind.Section;
                    currentSection = sectionMatch.Groups["section"].Value;
                    sectionName = currentSection;

                    if (sectionMatch.Groups["artist"].Success)
                    {
                        currentArtist = sectionMatch.Groups["artist"].Value.Trim();
                        artistAnnotation = currentArtist;
                    }
                }
                else if (MetadataRegex().IsMatch(trimmed))
                {
                    kind = SourceLineKind.Metadata;
                }
                else
                {
                    kind = SourceLineKind.Lyric;
                }
            }

            string canonicalText = rawLine; // Canonical line text
            string lineHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText))).ToLowerInvariant();

            sourceLines.Add(new SourceLine
            {
                LineNumber = lineNumber,
                RawText = rawLine,
                CanonicalText = canonicalText,
                Kind = kind,
                SectionName = sectionName,
                ArtistAnnotation = artistAnnotation,
                LineHash = lineHash
            });
        }

        string canonicalDocument = string.Join('\n', sourceLines.Select(l => l.CanonicalText));
        string canonicalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalDocument))).ToLowerInvariant();

        return new SourceVersion
        {
            SourceId = sourceId,
            SourceVersionId = versionId,
            ParentVersionId = parentVersionId,
            RawContentHash = rawHash,
            CanonicalContentHash = canonicalHash,
            NormalizerVersion = CurrentVersion,
            Lines = sourceLines
        };
    }

    /// <summary>
    /// Splits text into logical lines across CRLF, CR, and LF without dropping empty lines or trailing newlines.
    /// </summary>
    public static List<string> SplitLogicalLines(string text)
    {
        var lines = new List<string>();
        int length = text.Length;
        int lineStart = 0;

        for (int i = 0; i < length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                lines.Add(text[lineStart..i]);
                if (i + 1 < length && text[i + 1] == '\n')
                {
                    i++; // Skip \n in \r\n
                }
                lineStart = i + 1;
            }
            else if (c == '\n')
            {
                lines.Add(text[lineStart..i]);
                lineStart = i + 1;
            }
        }

        if (lineStart <= length)
        {
            lines.Add(text[lineStart..length]);
        }

        // If the text ended with a trailing newline, remove the redundant trailing empty element if desired,
        // or keep consistent with standard text file semantics:
        // A single trailing empty line from a terminating \n is standard in text editors.
        if (lines.Count > 1 && lines[^1].Length == 0 && (text.EndsWith('\n') || text.EndsWith('\r')))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }
}
