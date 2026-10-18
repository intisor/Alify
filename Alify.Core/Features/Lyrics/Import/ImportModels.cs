using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;

namespace Alify.Core.Features.Lyrics.Import;

public enum SourceKind
{
    UserPaste,
    UserFile,
    PermissionedProvider
}

[Flags]
public enum AllowedPurposes
{
    None = 0,
    LocalAnalysis = 1,
    Retention = 2,
    Export = 4,
    ExternalProcessing = 8
}

/// <summary>
/// Immutable local recording identity metadata (DATA-001 / PRD-013).
/// Distinguishes live, remix, clean, and explicit editions so lyrics aren't mistakenly conflated.
/// </summary>
public record RecordingIdentity
{
    public required string RecordingId { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<string> Artists { get; init; }
    public string? Album { get; init; }
    public string VersionLabel { get; init; } = "Original"; // "Original", "Clean", "Explicit", "Live", "Remix"
    public int? DurationMs { get; init; }
    public bool IsConfirmed { get; init; } = false;

    public static RecordingIdentity CreateDeclared(string title, string artist, string version = "Original") =>
        new()
        {
            RecordingId = Guid.NewGuid().ToString("N"),
            Title = title,
            Artists = [artist],
            VersionLabel = version,
            IsConfirmed = false
        };
}

/// <summary>
/// Recorded user attestation of permission/purpose for imported lyrics (DATA-002 / PRD-006).
/// Attestation is a recorded statement, not a legal finding.
/// </summary>
public record PermissionAttestation
{
    public required string AttestationId { get; init; }
    public required string Statement { get; init; }
    public required AllowedPurposes AllowedPurposes { get; init; }
    public DateTime AttestedAtUtc { get; init; } = DateTime.UtcNow;

    public static PermissionAttestation CreatePersonalStudy(AllowedPurposes purposes = AllowedPurposes.LocalAnalysis | AllowedPurposes.Retention) =>
        new()
        {
            AttestationId = Guid.NewGuid().ToString("N"),
            Statement = "User attests permission to import lyrics for local analysis and personal reference.",
            AllowedPurposes = purposes
        };
}

/// <summary>
/// Complete permissioned source record with provenance and active SourceVersion (DATA-002 / PRD-006).
/// </summary>
public record PermissionedSourceRecord
{
    public required string SourceId { get; init; }
    public required SourceKind Kind { get; init; }
    public required RecordingIdentity Recording { get; init; }
    public required PermissionAttestation Attestation { get; init; }
    public required SourceVersion CurrentVersion { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Typed result of a lyric import or resolution operation (DATA-002).
/// </summary>
public record ImportResolutionResult
{
    public required LyricResolution Resolution { get; init; }
    public PermissionedSourceRecord? SourceRecord { get; init; }
    public string? ErrorMessage { get; init; }
    public string? DiagnosticCode { get; init; }

    public static ImportResolutionResult Success(PermissionedSourceRecord record) =>
        new() { Resolution = LyricResolution.Found, SourceRecord = record };

    public static ImportResolutionResult NotFound(string message) =>
        new() { Resolution = LyricResolution.NotFound, ErrorMessage = message };

    public static ImportResolutionResult Instrumental(PermissionedSourceRecord record, string reason) =>
        new() { Resolution = LyricResolution.Instrumental, SourceRecord = record, ErrorMessage = reason };

    public static ImportResolutionResult Mismatch(string reason) =>
        new() { Resolution = LyricResolution.Mismatch, ErrorMessage = reason };

    public static ImportResolutionResult ProviderUnavailable(string code, string message) =>
        new() { Resolution = LyricResolution.ProviderUnavailable, DiagnosticCode = code, ErrorMessage = message };
}
