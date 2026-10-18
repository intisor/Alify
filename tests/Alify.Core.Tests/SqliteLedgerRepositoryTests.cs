using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Lyrics.Import;
using Alify.Core.Features.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Alify.Core.Tests;

public class SqliteLedgerRepositoryTests : IDisposable
{
    private readonly string _dbFilePath;
    private readonly SqliteLedgerRepository _repository;

    public SqliteLedgerRepositoryTests()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"alify_test_{Guid.NewGuid():N}.db");
        _repository = new SqliteLedgerRepository($"Data Source={_dbFilePath};Foreign Keys=True");
    }

    public void Dispose()
    {
        _repository.Dispose();
        try
        {
            if (File.Exists(_dbFilePath))
            {
                File.Delete(_dbFilePath);
            }
        }
        catch
        {
            // Best effort cleanup in temp directory
        }
    }

    [Fact]
    public async Task InitializeSchemaAsync_IsIdempotent()
    {
        await _repository.InitializeSchemaAsync();
        await _repository.InitializeSchemaAsync(); // Second call should not throw
    }

    [Fact]
    public async Task SourceRecord_RoundTrip_PersistsAndRestoresAllFields()
    {
        await _repository.InitializeSchemaAsync();

        var record = new PermissionedSourceRecord
        {
            SourceId = "src-001",
            Kind = SourceKind.UserPaste,
            Recording = new RecordingIdentity
            {
                RecordingId = "rec-001",
                Title = "Test Song",
                Artists = new List<string> { "Artist One", "Artist Two" },
                Album = "Test Album",
                VersionLabel = "Original",
                IsConfirmed = true
            },
            Attestation = new PermissionAttestation
            {
                AttestationId = "att-001",
                Statement = "User pasted for personal analysis",
                AllowedPurposes = AllowedPurposes.LocalAnalysis | AllowedPurposes.Retention,
                AttestedAtUtc = DateTime.UtcNow
            },
            CurrentVersion = new SourceVersion
            {
                SourceId = "src-001",
                SourceVersionId = "ver-001",
                ParentVersionId = null,
                RawContentHash = "rawhash123",
                CanonicalContentHash = "canonhash123",
                NormalizerVersion = "1.0.0",
                CreatedAtUtc = DateTime.UtcNow,
                Lines = new List<SourceLine>
                {
                    new()
                    {
                        LineNumber = 1,
                        RawText = "First line of lyric",
                        CanonicalText = "First line of lyric",
                        Kind = SourceLineKind.Lyric,
                        SectionName = "Verse"
                    },
                    new()
                    {
                        LineNumber = 2,
                        RawText = "Second line of lyric",
                        CanonicalText = "Second line of lyric",
                        Kind = SourceLineKind.Lyric,
                        SectionName = "Verse"
                    }
                }
            },
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repository.SaveSourceRecordAsync(record);
        var retrieved = await _repository.GetSourceRecordAsync("src-001");

        Assert.NotNull(retrieved);
        Assert.Equal("src-001", retrieved.SourceId);
        Assert.Equal(SourceKind.UserPaste, retrieved.Kind);
        Assert.Equal("rec-001", retrieved.Recording.RecordingId);
        Assert.Equal("Test Song", retrieved.Recording.Title);
        Assert.Equal(2, retrieved.Recording.Artists.Count);
        Assert.Equal("Artist One", retrieved.Recording.Artists[0]);
        Assert.Equal("Test Album", retrieved.Recording.Album);
        Assert.True(retrieved.Recording.IsConfirmed);

        Assert.Equal("att-001", retrieved.Attestation.AttestationId);
        Assert.Equal(AllowedPurposes.LocalAnalysis | AllowedPurposes.Retention, retrieved.Attestation.AllowedPurposes);

        Assert.Equal("ver-001", retrieved.CurrentVersion.SourceVersionId);
        Assert.Equal("rawhash123", retrieved.CurrentVersion.RawContentHash);
        Assert.Equal(2, retrieved.CurrentVersion.Lines.Count);
        Assert.Equal("First line of lyric", retrieved.CurrentVersion.Lines[0].CanonicalText);
    }

    [Fact]
    public async Task GetSourceRecordAsync_NonExistent_ReturnsNull()
    {
        await _repository.InitializeSchemaAsync();
        var retrieved = await _repository.GetSourceRecordAsync("non-existent");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task AnalysisResult_RoundTrip_PersistsAndRestores()
    {
        await _repository.InitializeSchemaAsync();

        // Must save source record first due to foreign key constraint
        var source = CreateSampleSource("src-002", "rec-002", "att-002", "ver-002");
        await _repository.SaveSourceRecordAsync(source);

        var analysis = new AnalysisResult
        {
            AnalysisId = "an-001",
            SourceId = "src-002",
            SourceVersionId = "ver-002",
            Status = AnalysisStatus.Completed,
            EngineKind = "HeuristicStanceEngine",
            Language = "en",
            ReasonCode = "AnalysisComplete",
            Findings = new List<Finding>
            {
                new()
                {
                    FindingId = "f-001",
                    Topic = "violence",
                    Stance = Stance.Rejected,
                    Expression = ExpressionType.Literal,
                    Rationale = "Explicit rejection of violence",
                    Confidence = 0.95,
                    Evidence = new List<EvidenceReference>
                    {
                        new() { StartLine = 1, EndLine = 1, QuotedText = "put down your weapons" }
                    }
                }
            },
            Limitations = new List<string> { "Heuristic pattern match only" },
            ExecutedAtUtc = DateTime.UtcNow
        };

        await _repository.SaveAnalysisResultAsync(analysis);
        var retrieved = await _repository.GetAnalysisResultAsync("an-001");

        Assert.NotNull(retrieved);
        Assert.Equal("an-001", retrieved.AnalysisId);
        Assert.Equal("src-002", retrieved.SourceId);
        Assert.Equal("ver-002", retrieved.SourceVersionId);
        Assert.Equal(AnalysisStatus.Completed, retrieved.Status);
        Assert.Equal("HeuristicStanceEngine", retrieved.EngineKind);
        Assert.Equal("en", retrieved.Language);
        Assert.Single(retrieved.Findings);
        Assert.Equal("violence", retrieved.Findings[0].Topic);
        Assert.Equal(Stance.Rejected, retrieved.Findings[0].Stance);
        Assert.Single(retrieved.Limitations);
    }

    [Fact]
    public async Task ReviewRecord_Revisions_MaintainsRevisionOrdering()
    {
        await _repository.InitializeSchemaAsync();

        var source = CreateSampleSource("src-003", "rec-003", "att-003", "ver-003");
        await _repository.SaveSourceRecordAsync(source);

        var analysis = new AnalysisResult
        {
            AnalysisId = "an-003",
            SourceId = "src-003",
            Status = AnalysisStatus.Completed,
            EngineKind = "HeuristicStanceEngine"
        };
        await _repository.SaveAnalysisResultAsync(analysis);

        var review1 = new ReviewRecord
        {
            ReviewId = "rev-001",
            AnalysisId = "an-003",
            FindingId = "f-001",
            ReviewerId = "user-1",
            Revision = 1,
            Disposition = ReviewDisposition.Disagree,
            Rationale = "Metaphorical usage in verse",
            AlternateEvidence = new List<EvidenceReference>(),
            ReviewedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };

        var review2 = new ReviewRecord
        {
            ReviewId = "rev-002",
            AnalysisId = "an-003",
            FindingId = "f-001",
            ReviewerId = "user-1",
            Revision = 2,
            Disposition = ReviewDisposition.Agree,
            Rationale = "Re-read lyrics in context",
            AlternateEvidence = new List<EvidenceReference>(),
            SupersedesReviewId = "rev-001",
            ReviewedAtUtc = DateTime.UtcNow
        };

        // Insert in reverse order to verify order by revision asc
        await _repository.SaveReviewRecordAsync(review2);
        await _repository.SaveReviewRecordAsync(review1);

        var reviews = await _repository.GetReviewsForAnalysisAsync("an-003");
        Assert.Equal(2, reviews.Count);
        Assert.Equal(1, reviews[0].Revision);
        Assert.Equal(ReviewDisposition.Disagree, reviews[0].Disposition);
        Assert.Equal(2, reviews[1].Revision);
        Assert.Equal(ReviewDisposition.Agree, reviews[1].Disposition);
        Assert.Equal("rev-001", reviews[1].SupersedesReviewId);
    }

    [Fact]
    public async Task PolicyEvaluation_RoundTrip_PersistsAndRestores()
    {
        await _repository.InitializeSchemaAsync();

        var evaluation = new PolicyEvaluation
        {
            DecisionId = "dec-001",
            Context = "LocalPlaybackCheck",
            Decision = PolicyDecision.Allow,
            ReasonCodes = new List<string> { "NoEndorsedHarm", "ExplicitFilterPassed" },
            AnalysisId = "an-001",
            PreferenceRevision = "pref-v2",
            RulesVersion = "2026.1",
            EvaluatedAtUtc = DateTime.UtcNow
        };

        await _repository.SavePolicyEvaluationAsync(evaluation);
        var retrieved = await _repository.GetPolicyEvaluationAsync("dec-001");

        Assert.NotNull(retrieved);
        Assert.Equal("dec-001", retrieved.DecisionId);
        Assert.Equal(PolicyDecision.Allow, retrieved.Decision);
        Assert.Equal(2, retrieved.ReasonCodes.Count);
        Assert.Contains("NoEndorsedHarm", retrieved.ReasonCodes);
        Assert.Equal("pref-v2", retrieved.PreferenceRevision);
    }

    [Fact]
    public async Task ForeignKeys_Enforced_ThrowsOnOrphanAnalysis()
    {
        await _repository.InitializeSchemaAsync();

        var orphanAnalysis = new AnalysisResult
        {
            AnalysisId = "an-orphan",
            SourceId = "non-existent-source",
            Status = AnalysisStatus.Completed,
            EngineKind = "HeuristicStanceEngine"
        };

        await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await _repository.SaveAnalysisResultAsync(orphanAnalysis);
        });
    }

    [Fact]
    public async Task DeleteSourceRecordAsync_CascadesDeletion_RemovesAnalysesReviewsAndVersions()
    {
        await _repository.InitializeSchemaAsync();

        var source = CreateSampleSource("src-del-1", "rec-del-1", "att-del-1", "ver-del-1");
        await _repository.SaveSourceRecordAsync(source);

        var analysis = new AnalysisResult
        {
            AnalysisId = "an-del-1",
            SourceId = "src-del-1",
            Status = AnalysisStatus.Completed,
            EngineKind = "HeuristicStanceEngine"
        };
        await _repository.SaveAnalysisResultAsync(analysis);

        var review = new ReviewRecord
        {
            ReviewId = "rev-del-1",
            AnalysisId = "an-del-1",
            ReviewerId = "user-1",
            Revision = 1,
            Disposition = ReviewDisposition.Agree,
            Rationale = "Confirmed"
        };
        await _repository.SaveReviewRecordAsync(review);

        var deleted = await _repository.DeleteSourceRecordAsync("src-del-1");
        Assert.True(deleted);

        // Verify source, analysis, and reviews are completely absent
        Assert.Null(await _repository.GetSourceRecordAsync("src-del-1"));
        Assert.Null(await _repository.GetAnalysisResultAsync("an-del-1"));
        var reviews = await _repository.GetReviewsForAnalysisAsync("an-del-1");
        Assert.Empty(reviews);

        // Deleting non-existent returns false
        var deletedAgain = await _repository.DeleteSourceRecordAsync("src-del-1");
        Assert.False(deletedAgain);
    }

    [Fact]
    public async Task PurgeAllSourcesAsync_ClearsEntireLedger()
    {
        await _repository.InitializeSchemaAsync();

        var source = CreateSampleSource("src-purge-1", "rec-purge-1", "att-purge-1", "ver-purge-1");
        await _repository.SaveSourceRecordAsync(source);

        await _repository.PurgeAllSourcesAsync();

        Assert.Null(await _repository.GetSourceRecordAsync("src-purge-1"));
    }

    private static PermissionedSourceRecord CreateSampleSource(string sourceId, string recordingId, string attestationId, string versionId)
    {
        return new PermissionedSourceRecord
        {
            SourceId = sourceId,
            Kind = SourceKind.UserPaste,
            Recording = new RecordingIdentity
            {
                RecordingId = recordingId,
                Title = "Song Title",
                Artists = new List<string> { "Artist" },
                VersionLabel = "Original",
                IsConfirmed = true
            },
            Attestation = new PermissionAttestation
            {
                AttestationId = attestationId,
                Statement = "Personal import",
                AllowedPurposes = AllowedPurposes.LocalAnalysis,
                AttestedAtUtc = DateTime.UtcNow
            },
            CurrentVersion = new SourceVersion
            {
                SourceId = sourceId,
                SourceVersionId = versionId,
                RawContentHash = "raw",
                CanonicalContentHash = "canon",
                NormalizerVersion = "1.0.0",
                CreatedAtUtc = DateTime.UtcNow,
                Lines = new List<SourceLine>
                {
                    new()
                    {
                        LineNumber = 1,
                        RawText = "put down your weapons",
                        CanonicalText = "put down your weapons",
                        Kind = SourceLineKind.Lyric,
                        SectionName = "Verse"
                    }
                }
            },
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
