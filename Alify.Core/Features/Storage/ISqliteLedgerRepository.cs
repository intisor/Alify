using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Lyrics.Import;

namespace Alify.Core.Features.Storage;

/// <summary>
/// Repository interface for local SQLite ledger storage (ALI-016 / DATA-012).
/// Supports atomic transactional saves, source versioning, and lane separation.
/// </summary>
public interface ISqliteLedgerRepository : IDisposable
{
    Task InitializeSchemaAsync(CancellationToken cancellationToken = default);

    Task SaveSourceRecordAsync(PermissionedSourceRecord record, CancellationToken cancellationToken = default);
    Task<PermissionedSourceRecord?> GetSourceRecordAsync(string sourceId, CancellationToken cancellationToken = default);

    Task SaveAnalysisResultAsync(AnalysisResult analysis, CancellationToken cancellationToken = default);
    Task<AnalysisResult?> GetAnalysisResultAsync(string analysisId, CancellationToken cancellationToken = default);

    Task SaveReviewRecordAsync(ReviewRecord review, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReviewRecord>> GetReviewsForAnalysisAsync(string analysisId, CancellationToken cancellationToken = default);

    Task SavePolicyEvaluationAsync(PolicyEvaluation evaluation, CancellationToken cancellationToken = default);
    Task<PolicyEvaluation?> GetPolicyEvaluationAsync(string decisionId, CancellationToken cancellationToken = default);

    Task<bool> DeleteSourceRecordAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<int> PurgeAllSourcesAsync(CancellationToken cancellationToken = default);
}
