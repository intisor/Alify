using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Lyrics.Import;
using Alify.Core.Features.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Alify.Lyrics.Pages.Lyrics;

public class ImportModel : PageModel
{
    private readonly PermissionedImportService _importService;
    private readonly ISqliteLedgerRepository _repository;

    public ImportModel(PermissionedImportService importService, ISqliteLedgerRepository repository)
    {
        _importService = importService;
        _repository = repository;
    }

    [BindProperty]
    [Required(ErrorMessage = "Track title is required")]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Artist name is required")]
    public string Artist { get; set; } = string.Empty;

    [BindProperty]
    public string? Album { get; set; }

    [BindProperty]
    public string VersionLabel { get; set; } = "Original";

    [BindProperty]
    [Required(ErrorMessage = "Lyrics text is required")]
    public string LyricsText { get; set; } = string.Empty;

    [BindProperty]
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must attest lawful permission for local analysis")]
    public bool AttestPermission { get; set; }

    [BindProperty]
    public bool IsInstrumental { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!AttestPermission)
        {
            ErrorMessage = "You must attest lawful permission for local personal analysis.";
            return Page();
        }

        var recording = RecordingIdentity.CreateDeclared(Title, Artist, VersionLabel);
        if (!string.IsNullOrWhiteSpace(Album))
        {
            recording = recording with { Album = Album.Trim() };
        }

        var attestation = new PermissionAttestation
        {
            AttestationId = Guid.NewGuid().ToString("N"),
            Statement = "User declared lawful possession and permission for private local lyric analysis.",
            AllowedPurposes = AllowedPurposes.LocalAnalysis | AllowedPurposes.Retention,
            AttestedAtUtc = DateTime.UtcNow
        };

        var result = await _importService.ImportPasteAsync(LyricsText, recording, attestation, IsInstrumental);

        if (result.SourceRecord == null)
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to normalize and import lyrics.";
            return Page();
        }

        await _repository.SaveSourceRecordAsync(result.SourceRecord);

        return RedirectToPage("/Lyrics/Analysis", new { sourceId = result.SourceRecord.SourceId });
    }
}
