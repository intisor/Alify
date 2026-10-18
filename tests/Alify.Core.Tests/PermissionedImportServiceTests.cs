using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Lyrics.Import;
using System.Text;
using Xunit;

namespace Alify.Core.Tests;

public class PermissionedImportServiceTests
{
    private readonly PermissionedImportService _importService = new();

    private RecordingIdentity SampleRecording =>
        RecordingIdentity.CreateDeclared("HUMBLE.", "Kendrick Lamar");

    private PermissionAttestation SampleAttestation =>
        PermissionAttestation.CreatePersonalStudy();

    [Fact]
    public async Task ImportPasteAsync_ValidLyrics_ReturnsFoundWithImmutableSource()
    {
        var lyrics = "[Verse 1]\nNobody pray for me\nEven a day for me";
        var result = await _importService.ImportPasteAsync(lyrics, SampleRecording, SampleAttestation);

        Assert.Equal(LyricResolution.Found, result.Resolution);
        Assert.NotNull(result.SourceRecord);
        Assert.Equal(SourceKind.UserPaste, result.SourceRecord.Kind);
        Assert.Equal("HUMBLE.", result.SourceRecord.Recording.Title);
        Assert.NotEmpty(result.SourceRecord.CurrentVersion.CanonicalContentHash);
    }

    [Fact]
    public async Task ImportPasteAsync_OversizedText_RejectsWithMismatch()
    {
        // Exceed 64 KB limit
        var hugeText = new string('A', PermissionedImportService.MaxPasteCharacterLimit + 50);
        var result = await _importService.ImportPasteAsync(hugeText, SampleRecording, SampleAttestation);

        Assert.Equal(LyricResolution.Mismatch, result.Resolution);
        Assert.Contains("exceeds the allowed limit", result.ErrorMessage);
    }

    [Fact]
    public async Task ImportPasteAsync_EmptyAttestationPurposes_RejectsWithMismatch()
    {
        var attestation = new PermissionAttestation
        {
            AttestationId = "a1",
            Statement = "Invalid",
            AllowedPurposes = AllowedPurposes.None
        };

        var result = await _importService.ImportPasteAsync("Some lyrics", SampleRecording, attestation);

        Assert.Equal(LyricResolution.Mismatch, result.Resolution);
        Assert.Contains("must declare at least one allowed purpose", result.ErrorMessage);
    }

    [Fact]
    public async Task ImportPasteAsync_ExplicitlyInstrumental_ReturnsInstrumental()
    {
        var result = await _importService.ImportPasteAsync("", SampleRecording, SampleAttestation, isExplicitlyInstrumental: true);

        Assert.Equal(LyricResolution.Instrumental, result.Resolution);
        Assert.NotNull(result.SourceRecord);
        Assert.Contains("instrumental track", result.ErrorMessage);
    }

    [Fact]
    public async Task ImportPasteAsync_WhitespaceOnly_ReturnsNotFound()
    {
        var result = await _importService.ImportPasteAsync("   \r\n  ", SampleRecording, SampleAttestation);

        Assert.Equal(LyricResolution.NotFound, result.Resolution);
        Assert.Contains("Imported text is empty", result.ErrorMessage);
    }

    [Fact]
    public async Task ImportFileAsync_ValidUtf8WithBom_StripsBomAndImportsSuccessfully()
    {
        var rawText = "[Verse 1]\nTest line";
        byte[] bytesWithBom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(rawText)).ToArray();

        var result = await _importService.ImportFileAsync("song.txt", bytesWithBom, SampleRecording, SampleAttestation);

        Assert.Equal(LyricResolution.Found, result.Resolution);
        Assert.NotNull(result.SourceRecord);
        Assert.False(result.SourceRecord.CurrentVersion.Lines[0].RawText.StartsWith('\uFEFF'));
    }

    [Fact]
    public async Task ResolveFromProviderAsync_ReturnsProviderUnavailablePerPolicy()
    {
        var result = await _importService.ResolveFromProviderAsync("Kendrick Lamar", "HUMBLE.");

        Assert.Equal(LyricResolution.ProviderUnavailable, result.Resolution);
        Assert.Equal("PROVIDER_IMPORT_BLOCKED", result.DiagnosticCode);
        Assert.Contains("disabled pending rights", result.ErrorMessage);
    }
}
