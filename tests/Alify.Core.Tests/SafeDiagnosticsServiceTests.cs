using Alify.Core.Features.Storage;
using Alify.Core.Infrastructure.Logging;
using Xunit;

namespace Alify.Core.Tests;

public class SafeDiagnosticsServiceTests
{
    private class DummyRepository : ISqliteLedgerRepository
    {
        public Task InitializeSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveSourceRecordAsync(Features.Lyrics.Import.PermissionedSourceRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Features.Lyrics.Import.PermissionedSourceRecord?> GetSourceRecordAsync(string sourceId, CancellationToken cancellationToken = default) => Task.FromResult<Features.Lyrics.Import.PermissionedSourceRecord?>(null);
        public Task SaveAnalysisResultAsync(Features.Analysis.Models.AnalysisResult analysis, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Features.Analysis.Models.AnalysisResult?> GetAnalysisResultAsync(string analysisId, CancellationToken cancellationToken = default) => Task.FromResult<Features.Analysis.Models.AnalysisResult?>(null);
        public Task SaveReviewRecordAsync(Features.Analysis.Models.ReviewRecord review, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Features.Analysis.Models.ReviewRecord>> GetReviewsForAnalysisAsync(string analysisId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Features.Analysis.Models.ReviewRecord>>([]);
        public Task SavePolicyEvaluationAsync(Features.Analysis.Models.PolicyEvaluation evaluation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Features.Analysis.Models.PolicyEvaluation?> GetPolicyEvaluationAsync(string decisionId, CancellationToken cancellationToken = default) => Task.FromResult<Features.Analysis.Models.PolicyEvaluation?>(null);
        public Task<bool> DeleteSourceRecordAsync(string sourceId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<int> PurgeAllSourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public void Dispose() { }
    }

    [Fact]
    public void Redact_BearerTokens_ReplacesWithRedactedPlaceholder()
    {
        string input = "Sending request with Authorization: Bearer BQD1234567890abcdef1234567890 to Spotify";
        string redacted = SafeDiagnosticsService.Redact(input);

        Assert.DoesNotContain("BQD1234567890abcdef1234567890", redacted);
        Assert.Contains("Bearer [REDACTED]", redacted);
    }

    [Fact]
    public void Redact_GeminiApiKeys_ReplacesWithRedactedApiKey()
    {
        string input = "Gemini initialized with key AIzaSyD1234567890abcdef1234567890abc";
        string redacted = SafeDiagnosticsService.Redact(input);

        Assert.DoesNotContain("AIzaSyD1234567890abcdef1234567890abc", redacted);
        Assert.Contains("[REDACTED_API_KEY]", redacted);
    }

    [Fact]
    public void Redact_ClientSecret_ReplacesWithValueRedacted()
    {
        string input = "Config loaded: client_secret=\"super_secret_value_12345\"";
        string redacted = SafeDiagnosticsService.Redact(input);

        Assert.DoesNotContain("super_secret_value_12345", redacted);
        Assert.Contains("client_secret=[REDACTED]", redacted);
    }

    [Fact]
    public async Task GenerateSanitizedDiagnosticsAsync_ReturnsValidBundle()
    {
        var service = new SafeDiagnosticsService(new DummyRepository());
        var bundle = await service.GenerateSanitizedDiagnosticsAsync();

        Assert.NotNull(bundle);
        Assert.NotEmpty(bundle.DotNetRuntimeVersion);
        Assert.NotEmpty(bundle.OsDescription);
        Assert.NotEmpty(bundle.ProcessArchitecture);
        Assert.Equal("Healthy", bundle.Status);
    }
}
