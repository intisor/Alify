using Alify.Core.Features.Analysis.Ledger;
using System.Text;

namespace Alify.Core.Features.Lyrics.Import;

/// <summary>
/// Service for importing lyrics from user paste or user files with permission attestations,
/// bounded size limits, and canonical ledger integration (ALI-017 / PRD-006 / DATA-002).
/// Arbitrary web scraping is disabled by policy.
/// </summary>
public class PermissionedImportService
{
    public const int MaxPasteCharacterLimit = 64 * 1024; // 64 KB text limit
    public const int MaxFileByteLimit = 128 * 1024;      // 128 KB file limit

    private readonly CanonicalLyricNormalizer _normalizer;

    public PermissionedImportService(CanonicalLyricNormalizer? normalizer = null)
    {
        _normalizer = normalizer ?? new CanonicalLyricNormalizer();
    }

    /// <summary>
    /// Imports lyrics from user-pasted text with permission attestation and size limit validation.
    /// </summary>
    public Task<ImportResolutionResult> ImportPasteAsync(
        string rawText,
        RecordingIdentity recording,
        PermissionAttestation attestation,
        bool isExplicitlyInstrumental = false)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(attestation);

        if (attestation.AllowedPurposes == AllowedPurposes.None)
        {
            return Task.FromResult(ImportResolutionResult.Mismatch("Import rejected: Attestation must declare at least one allowed purpose (e.g. LocalAnalysis)."));
        }

        if (isExplicitlyInstrumental)
        {
            var instVersion = _normalizer.Normalize("[Instrumental]\n(No sung lyrics for this recording)");
            var instRecord = new PermissionedSourceRecord
            {
                SourceId = Guid.NewGuid().ToString("N"),
                Kind = SourceKind.UserPaste,
                Recording = recording,
                Attestation = attestation,
                CurrentVersion = instVersion
            };
            return Task.FromResult(ImportResolutionResult.Instrumental(instRecord, "Affirmative user declaration: Recording is an instrumental track without sung lyrics."));
        }

        if (string.IsNullOrWhiteSpace(rawText))
        {
            return Task.FromResult(ImportResolutionResult.NotFound("Imported text is empty. No lyrics provided."));
        }

        if (rawText.Length > MaxPasteCharacterLimit)
        {
            return Task.FromResult(ImportResolutionResult.Mismatch($"Imported text size ({rawText.Length} chars) exceeds the allowed limit ({MaxPasteCharacterLimit} chars)."));
        }

        var sourceId = Guid.NewGuid().ToString("N");
        var version = _normalizer.Normalize(rawText, sourceId);

        bool hasLyrics = version.Lines.Any(l => l.Kind == SourceLineKind.Lyric);
        if (!hasLyrics)
        {
            return Task.FromResult(ImportResolutionResult.NotFound("Imported text contains only metadata or blank lines, but no sung lyrics."));
        }

        var record = new PermissionedSourceRecord
        {
            SourceId = sourceId,
            Kind = SourceKind.UserPaste,
            Recording = recording,
            Attestation = attestation,
            CurrentVersion = version
        };

        return Task.FromResult(ImportResolutionResult.Success(record));
    }

    /// <summary>
    /// Imports lyrics from a user file (.txt or .lrc) with byte size checks and encoding handling.
    /// </summary>
    public Task<ImportResolutionResult> ImportFileAsync(
        string fileName,
        byte[] fileBytes,
        RecordingIdentity recording,
        PermissionAttestation attestation,
        bool isExplicitlyInstrumental = false)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(fileBytes);
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(attestation);

        if (fileBytes.Length > MaxFileByteLimit)
        {
            return Task.FromResult(ImportResolutionResult.Mismatch($"File size ({fileBytes.Length} bytes) exceeds maximum allowed size ({MaxFileByteLimit} bytes)."));
        }

        // Decode UTF-8 handling BOM if present
        string rawText = Encoding.UTF8.GetString(fileBytes);
        if (rawText.StartsWith('\uFEFF'))
        {
            rawText = rawText[1..]; // Strip BOM
        }

        return ImportPasteAsync(rawText, recording, attestation, isExplicitlyInstrumental);
    }

    /// <summary>
    /// Explicit provider lookup endpoint. Unconsented scraping or arbitrary URL fetching is disabled by policy (PRD-006).
    /// </summary>
    public Task<ImportResolutionResult> ResolveFromProviderAsync(string artist, string title)
    {
        return Task.FromResult(ImportResolutionResult.ProviderUnavailable(
            "PROVIDER_IMPORT_BLOCKED",
            "Automatic external provider lookup is currently disabled pending rights and platform review (PRD-006). Use permissioned user paste or file import."));
    }
}
