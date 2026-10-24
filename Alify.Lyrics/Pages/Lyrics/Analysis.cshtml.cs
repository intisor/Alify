using Alify.Core.Features.Analysis.Engine;
using Alify.Core.Features.Analysis.Engine.Gemini;
using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Analysis.Policy;
using Alify.Core.Features.Lyrics.Import;
using Alify.Core.Features.Storage;
using Alify.Lyrics.Features.Lyrics.Analysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alify.Lyrics.Pages.Lyrics;

public class AnalysisModel : PageModel
{
    private readonly ISqliteLedgerRepository _repository;
    private readonly HeuristicStanceEngine _heuristicEngine;
    private readonly BudgetLedger _budgetLedger;

    public AnalysisModel(
        ISqliteLedgerRepository repository,
        HeuristicStanceEngine heuristicEngine,
        BudgetLedger budgetLedger)
    {
        _repository = repository;
        _heuristicEngine = heuristicEngine;
        _budgetLedger = budgetLedger;
    }

    public PermissionedSourceRecord? SourceRecord { get; set; }
    public AnalysisResult? LatestAnalysis { get; set; }
    public IReadOnlyList<ReviewRecord> Reviews { get; set; } = [];
    public PolicyEvaluation? CurrentPolicy { get; set; }

    public int RemainingBudget => _budgetLedger.RemainingCapacity;
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string sourceId, string? analysisId = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return RedirectToPage("/Lyrics/Import");
        }

        SourceRecord = await _repository.GetSourceRecordAsync(sourceId);
        if (SourceRecord == null)
        {
            ErrorMessage = $"Source record '{sourceId}' not found in the local ledger.";
            return Page();
        }

        if (!string.IsNullOrEmpty(analysisId))
        {
            LatestAnalysis = await _repository.GetAnalysisResultAsync(analysisId);
        }

        if (LatestAnalysis != null)
        {
            Reviews = await _repository.GetReviewsForAnalysisAsync(LatestAnalysis.AnalysisId);
            var prefs = PolicyPreferences.Default;
            CurrentPolicy = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(LatestAnalysis, prefs, Reviews);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRunHeuristicAsync(string sourceId)
    {
        SourceRecord = await _repository.GetSourceRecordAsync(sourceId);
        if (SourceRecord == null)
        {
            ErrorMessage = $"Source record '{sourceId}' not found.";
            return Page();
        }

        LatestAnalysis = await _heuristicEngine.AnalyzeAsync(SourceRecord.CurrentVersion);
        await _repository.SaveAnalysisResultAsync(LatestAnalysis);

        var prefs = PolicyPreferences.Default;
        Reviews = await _repository.GetReviewsForAnalysisAsync(LatestAnalysis.AnalysisId);
        CurrentPolicy = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(LatestAnalysis, prefs, Reviews);
        await _repository.SavePolicyEvaluationAsync(CurrentPolicy);

        StatusMessage = "Heuristic baseline analysis completed and saved to ledger.";
        return Page();
    }

    public async Task<IActionResult> OnPostRunGeminiAsync(string sourceId, string apiKey, bool userConsent)
    {
        SourceRecord = await _repository.GetSourceRecordAsync(sourceId);
        if (SourceRecord == null)
        {
            ErrorMessage = $"Source record '{sourceId}' not found.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ErrorMessage = "An AI API key is required for BYOK analysis (Gemini, OpenRouter, or Groq).";
            return Page();
        }

        if (!userConsent)
        {
            ErrorMessage = "You must explicitly consent to transmitting lyrics to the external AI provider.";
            return Page();
        }

        var trimmedKey = apiKey.Trim();
        IGeminiModelClient client;
        if (trimmedKey.StartsWith("gsk_", StringComparison.OrdinalIgnoreCase))
        {
            client = new OpenAiCompatibleModelClient(new HttpClient(), trimmedKey, "llama-3.3-70b-versatile", OpenAiCompatibleModelClient.DefaultGroqEndpoint);
        }
        else if (trimmedKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
        {
            client = new OpenAiCompatibleModelClient(new HttpClient(), trimmedKey, "meta-llama/llama-3.3-70b-instruct:free", OpenAiCompatibleModelClient.DefaultOpenRouterEndpoint);
        }
        else
        {
            client = new MsccGeminiModelClient(trimmedKey);
        }

        var consent = ProviderConsent.Granted(client.ModelName);
        var engine = new GeminiStanceAnalysisEngine(client, consent, _budgetLedger);

        LatestAnalysis = await engine.AnalyzeAsync(SourceRecord.CurrentVersion);
        await _repository.SaveAnalysisResultAsync(LatestAnalysis);

        Reviews = await _repository.GetReviewsForAnalysisAsync(LatestAnalysis.AnalysisId);
        var prefs = PolicyPreferences.Default;
        CurrentPolicy = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(LatestAnalysis, prefs, Reviews);
        await _repository.SavePolicyEvaluationAsync(CurrentPolicy);

        if (LatestAnalysis.Status == AnalysisStatus.Completed)
        {
            StatusMessage = $"{client.ModelName} structured stance analysis completed and saved to ledger.";
        }
        else
        {
            ErrorMessage = $"{client.ModelName} analysis returned {LatestAnalysis.Status}: {LatestAnalysis.ReasonCode}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSubmitReviewAsync(
        string sourceId,
        string analysisId,
        string? findingId,
        string reviewerId,
        string disposition,
        string rationale)
    {
        SourceRecord = await _repository.GetSourceRecordAsync(sourceId);
        LatestAnalysis = await _repository.GetAnalysisResultAsync(analysisId);

        if (SourceRecord == null || LatestAnalysis == null)
        {
            ErrorMessage = "Source or Analysis record not found.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(rationale))
        {
            ErrorMessage = "A rationale is required for reviewer audit records.";
            Reviews = await _repository.GetReviewsForAnalysisAsync(analysisId);
            return Page();
        }

        if (!Enum.TryParse<ReviewDisposition>(disposition, true, out var parsedDisposition))
        {
            ErrorMessage = "Invalid disposition selected.";
            Reviews = await _repository.GetReviewsForAnalysisAsync(analysisId);
            return Page();
        }

        var existingReviews = await _repository.GetReviewsForAnalysisAsync(analysisId);
        int nextRevision = existingReviews.Count > 0 ? existingReviews.Max(r => r.Revision) + 1 : 1;
        string? supersedesId = existingReviews.LastOrDefault(r => r.FindingId == findingId)?.ReviewId;

        var review = new ReviewRecord
        {
            ReviewId = Guid.NewGuid().ToString("N"),
            AnalysisId = analysisId,
            FindingId = string.IsNullOrWhiteSpace(findingId) ? null : findingId,
            ReviewerId = string.IsNullOrWhiteSpace(reviewerId) ? "local-user" : reviewerId.Trim(),
            Revision = nextRevision,
            Disposition = parsedDisposition,
            Rationale = rationale.Trim(),
            AlternateEvidence = [],
            SupersedesReviewId = supersedesId,
            ReviewedAtUtc = DateTime.UtcNow
        };

        await _repository.SaveReviewRecordAsync(review);

        Reviews = await _repository.GetReviewsForAnalysisAsync(analysisId);
        var prefs = PolicyPreferences.Default;
        CurrentPolicy = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(LatestAnalysis, prefs, Reviews);
        await _repository.SavePolicyEvaluationAsync(CurrentPolicy);

        StatusMessage = $"Review revision #{nextRevision} saved. Policy recomputed.";
        return Page();
    }
}
