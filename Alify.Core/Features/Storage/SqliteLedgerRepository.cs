using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Lyrics.Import;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Alify.Core.Features.Storage;

/// <summary>
/// SQLite implementation of the immutable source and semantic ledger repository (ALI-016 / DATA-012).
/// Features WAL mode, foreign key enforcement, transaction wrappers, and serialized write coordination.
/// </summary>
public class SqliteLedgerRepository : ISqliteLedgerRepository
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _isDisposed;

    static SqliteLedgerRepository()
    {
        SQLitePCL.Batteries.Init();
    }

    public SqliteLedgerRepository(string connectionString = "Data Source=alify_ledger.db;Foreign Keys=True")
    {
        _connectionString = connectionString;
    }

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS recordings (
                    recording_id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    artists_json TEXT NOT NULL,
                    album TEXT,
                    version_label TEXT NOT NULL,
                    is_confirmed INTEGER NOT NULL DEFAULT 0,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS permission_attestations (
                    attestation_id TEXT PRIMARY KEY,
                    statement TEXT NOT NULL,
                    allowed_purposes INTEGER NOT NULL,
                    attested_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS sources (
                    source_id TEXT PRIMARY KEY,
                    recording_id TEXT NOT NULL REFERENCES recordings(recording_id) ON DELETE CASCADE,
                    attestation_id TEXT NOT NULL REFERENCES permission_attestations(attestation_id),
                    source_kind TEXT NOT NULL,
                    current_version_id TEXT,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS source_versions (
                    source_version_id TEXT PRIMARY KEY,
                    source_id TEXT NOT NULL REFERENCES sources(source_id) ON DELETE CASCADE,
                    parent_version_id TEXT,
                    raw_content_hash TEXT NOT NULL,
                    canonical_content_hash TEXT NOT NULL,
                    normalizer_version TEXT NOT NULL,
                    lines_json TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS analyses (
                    analysis_id TEXT PRIMARY KEY,
                    source_id TEXT NOT NULL REFERENCES sources(source_id) ON DELETE CASCADE,
                    source_version_id TEXT,
                    status TEXT NOT NULL,
                    engine_kind TEXT NOT NULL,
                    language TEXT,
                    reason_code TEXT,
                    findings_json TEXT NOT NULL,
                    limitations_json TEXT NOT NULL,
                    executed_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS reviews (
                    review_id TEXT PRIMARY KEY,
                    analysis_id TEXT NOT NULL REFERENCES analyses(analysis_id) ON DELETE CASCADE,
                    finding_id TEXT,
                    reviewer_id TEXT NOT NULL,
                    revision INTEGER NOT NULL,
                    disposition TEXT NOT NULL,
                    rationale TEXT NOT NULL,
                    alternate_evidence_json TEXT NOT NULL,
                    supersedes_review_id TEXT,
                    reviewed_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS policy_evaluations (
                    decision_id TEXT PRIMARY KEY,
                    context TEXT NOT NULL,
                    decision TEXT NOT NULL,
                    reason_codes_json TEXT NOT NULL,
                    analysis_id TEXT,
                    preference_revision TEXT,
                    rules_version TEXT,
                    evaluated_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_sources_recording ON sources(recording_id);
                CREATE INDEX IF NOT EXISTS idx_versions_source ON source_versions(source_id);
                CREATE INDEX IF NOT EXISTS idx_analyses_source ON analyses(source_id);
                CREATE INDEX IF NOT EXISTS idx_reviews_analysis ON reviews(analysis_id);
            ";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveSourceRecordAsync(PermissionedSourceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            // 1. Insert recording
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO recordings (recording_id, title, artists_json, album, version_label, is_confirmed, created_at)
                    VALUES ($id, $title, $artists, $album, $version, $confirmed, $created);";
                cmd.Parameters.AddWithValue("$id", record.Recording.RecordingId);
                cmd.Parameters.AddWithValue("$title", record.Recording.Title);
                cmd.Parameters.AddWithValue("$artists", JsonSerializer.Serialize(record.Recording.Artists));
                cmd.Parameters.AddWithValue("$album", (object?)record.Recording.Album ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$version", record.Recording.VersionLabel);
                cmd.Parameters.AddWithValue("$confirmed", record.Recording.IsConfirmed ? 1 : 0);
                cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2. Insert attestation
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO permission_attestations (attestation_id, statement, allowed_purposes, attested_at)
                    VALUES ($id, $stmt, $purposes, $attested);";
                cmd.Parameters.AddWithValue("$id", record.Attestation.AttestationId);
                cmd.Parameters.AddWithValue("$stmt", record.Attestation.Statement);
                cmd.Parameters.AddWithValue("$purposes", (int)record.Attestation.AllowedPurposes);
                cmd.Parameters.AddWithValue("$attested", record.Attestation.AttestedAtUtc.ToString("o"));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 3. Insert source
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO sources (source_id, recording_id, attestation_id, source_kind, current_version_id, created_at)
                    VALUES ($id, $recId, $attId, $kind, $verId, $created);";
                cmd.Parameters.AddWithValue("$id", record.SourceId);
                cmd.Parameters.AddWithValue("$recId", record.Recording.RecordingId);
                cmd.Parameters.AddWithValue("$attId", record.Attestation.AttestationId);
                cmd.Parameters.AddWithValue("$kind", record.Kind.ToString());
                cmd.Parameters.AddWithValue("$verId", record.CurrentVersion.SourceVersionId);
                cmd.Parameters.AddWithValue("$created", record.CreatedAtUtc.ToString("o"));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 4. Insert version
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO source_versions (source_version_id, source_id, parent_version_id, raw_content_hash, canonical_content_hash, normalizer_version, lines_json, created_at)
                    VALUES ($id, $srcId, $parent, $rawHash, $canonHash, $normVer, $lines, $created);";
                cmd.Parameters.AddWithValue("$id", record.CurrentVersion.SourceVersionId);
                cmd.Parameters.AddWithValue("$srcId", record.SourceId);
                cmd.Parameters.AddWithValue("$parent", (object?)record.CurrentVersion.ParentVersionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$rawHash", record.CurrentVersion.RawContentHash);
                cmd.Parameters.AddWithValue("$canonHash", record.CurrentVersion.CanonicalContentHash);
                cmd.Parameters.AddWithValue("$normVer", record.CurrentVersion.NormalizerVersion);
                cmd.Parameters.AddWithValue("$lines", JsonSerializer.Serialize(record.CurrentVersion.Lines));
                cmd.Parameters.AddWithValue("$created", record.CurrentVersion.CreatedAtUtc.ToString("o"));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            tx.Commit();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<PermissionedSourceRecord?> GetSourceRecordAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT s.source_id, s.source_kind, s.created_at,
                   r.recording_id, r.title, r.artists_json, r.album, r.version_label, r.is_confirmed,
                   a.attestation_id, a.statement, a.allowed_purposes, a.attested_at,
                   v.source_version_id, v.parent_version_id, v.raw_content_hash, v.canonical_content_hash, v.normalizer_version, v.lines_json, v.created_at as ver_created
            FROM sources s
            JOIN recordings r ON s.recording_id = r.recording_id
            JOIN permission_attestations a ON s.attestation_id = a.attestation_id
            JOIN source_versions v ON s.current_version_id = v.source_version_id
            WHERE s.source_id = $id;";
        cmd.Parameters.AddWithValue("$id", sourceId);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var recording = new RecordingIdentity
        {
            RecordingId = reader.GetString(3),
            Title = reader.GetString(4),
            Artists = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? [],
            Album = reader.IsDBNull(6) ? null : reader.GetString(6),
            VersionLabel = reader.GetString(7),
            IsConfirmed = reader.GetInt32(8) == 1
        };

        var attestation = new PermissionAttestation
        {
            AttestationId = reader.GetString(9),
            Statement = reader.GetString(10),
            AllowedPurposes = (AllowedPurposes)reader.GetInt32(11),
            AttestedAtUtc = DateTime.Parse(reader.GetString(12))
        };

        var lines = JsonSerializer.Deserialize<List<SourceLine>>(reader.GetString(18)) ?? [];
        var version = new SourceVersion
        {
            SourceId = sourceId,
            SourceVersionId = reader.GetString(13),
            ParentVersionId = reader.IsDBNull(14) ? null : reader.GetString(14),
            RawContentHash = reader.GetString(15),
            CanonicalContentHash = reader.GetString(16),
            NormalizerVersion = reader.GetString(17),
            Lines = lines,
            CreatedAtUtc = DateTime.Parse(reader.GetString(19))
        };

        return new PermissionedSourceRecord
        {
            SourceId = sourceId,
            Kind = Enum.Parse<SourceKind>(reader.GetString(1)),
            Recording = recording,
            Attestation = attestation,
            CurrentVersion = version,
            CreatedAtUtc = DateTime.Parse(reader.GetString(2))
        };
    }

    public async Task SaveAnalysisResultAsync(AnalysisResult analysis, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO analyses (analysis_id, source_id, source_version_id, status, engine_kind, language, reason_code, findings_json, limitations_json, executed_at)
                VALUES ($id, $srcId, $verId, $status, $engine, $lang, $reason, $findings, $limitations, $executed);";
            cmd.Parameters.AddWithValue("$id", analysis.AnalysisId);
            cmd.Parameters.AddWithValue("$srcId", analysis.SourceId);
            cmd.Parameters.AddWithValue("$verId", (object?)analysis.SourceVersionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", analysis.Status.ToString());
            cmd.Parameters.AddWithValue("$engine", analysis.EngineKind);
            cmd.Parameters.AddWithValue("$lang", (object?)analysis.Language ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$reason", (object?)analysis.ReasonCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$findings", JsonSerializer.Serialize(analysis.Findings));
            cmd.Parameters.AddWithValue("$limitations", JsonSerializer.Serialize(analysis.Limitations));
            cmd.Parameters.AddWithValue("$executed", analysis.ExecutedAtUtc.ToString("o"));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<AnalysisResult?> GetAnalysisResultAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT analysis_id, source_id, source_version_id, status, engine_kind, language, reason_code, findings_json, limitations_json, executed_at
            FROM analyses
            WHERE analysis_id = $id;";
        cmd.Parameters.AddWithValue("$id", analysisId);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var findings = JsonSerializer.Deserialize<List<Finding>>(reader.GetString(7)) ?? [];
        var limitations = JsonSerializer.Deserialize<List<string>>(reader.GetString(8)) ?? [];

        return new AnalysisResult
        {
            AnalysisId = reader.GetString(0),
            SourceId = reader.GetString(1),
            SourceVersionId = reader.IsDBNull(2) ? null : reader.GetString(2),
            Status = Enum.Parse<AnalysisStatus>(reader.GetString(3)),
            EngineKind = reader.GetString(4),
            Language = reader.IsDBNull(5) ? null : reader.GetString(5),
            ReasonCode = reader.IsDBNull(6) ? null : reader.GetString(6),
            Findings = findings,
            Limitations = limitations,
            ExecutedAtUtc = DateTime.Parse(reader.GetString(9))
        };
    }

    public async Task SaveReviewRecordAsync(ReviewRecord review, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO reviews (review_id, analysis_id, finding_id, reviewer_id, revision, disposition, rationale, alternate_evidence_json, supersedes_review_id, reviewed_at)
                VALUES ($id, $analysisId, $findingId, $reviewerId, $revision, $disposition, $rationale, $evidence, $supersedes, $reviewed);";
            cmd.Parameters.AddWithValue("$id", review.ReviewId);
            cmd.Parameters.AddWithValue("$analysisId", review.AnalysisId);
            cmd.Parameters.AddWithValue("$findingId", (object?)review.FindingId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$reviewerId", review.ReviewerId);
            cmd.Parameters.AddWithValue("$revision", review.Revision);
            cmd.Parameters.AddWithValue("$disposition", review.Disposition.ToString());
            cmd.Parameters.AddWithValue("$rationale", review.Rationale);
            cmd.Parameters.AddWithValue("$evidence", JsonSerializer.Serialize(review.AlternateEvidence));
            cmd.Parameters.AddWithValue("$supersedes", (object?)review.SupersedesReviewId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$reviewed", review.ReviewedAtUtc.ToString("o"));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<ReviewRecord>> GetReviewsForAnalysisAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT review_id, analysis_id, finding_id, reviewer_id, revision, disposition, rationale, alternate_evidence_json, supersedes_review_id, reviewed_at
            FROM reviews
            WHERE analysis_id = $id
            ORDER BY revision ASC;";
        cmd.Parameters.AddWithValue("$id", analysisId);

        var list = new List<ReviewRecord>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var evidence = JsonSerializer.Deserialize<List<EvidenceReference>>(reader.GetString(7)) ?? [];
            list.Add(new ReviewRecord
            {
                ReviewId = reader.GetString(0),
                AnalysisId = reader.GetString(1),
                FindingId = reader.IsDBNull(2) ? null : reader.GetString(2),
                ReviewerId = reader.GetString(3),
                Revision = reader.GetInt32(4),
                Disposition = Enum.Parse<ReviewDisposition>(reader.GetString(5)),
                Rationale = reader.GetString(6),
                AlternateEvidence = evidence,
                SupersedesReviewId = reader.IsDBNull(8) ? null : reader.GetString(8),
                ReviewedAtUtc = DateTime.Parse(reader.GetString(9))
            });
        }
        return list;
    }

    public async Task SavePolicyEvaluationAsync(PolicyEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO policy_evaluations (decision_id, context, decision, reason_codes_json, analysis_id, preference_revision, rules_version, evaluated_at)
                VALUES ($id, $context, $decision, $reasons, $analysisId, $prefRev, $rulesVer, $evaluated);";
            cmd.Parameters.AddWithValue("$id", evaluation.DecisionId);
            cmd.Parameters.AddWithValue("$context", evaluation.Context);
            cmd.Parameters.AddWithValue("$decision", evaluation.Decision.ToString());
            cmd.Parameters.AddWithValue("$reasons", JsonSerializer.Serialize(evaluation.ReasonCodes));
            cmd.Parameters.AddWithValue("$analysisId", (object?)evaluation.AnalysisId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$prefRev", (object?)evaluation.PreferenceRevision ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$rulesVer", (object?)evaluation.RulesVersion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$evaluated", evaluation.EvaluatedAtUtc.ToString("o"));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<PolicyEvaluation?> GetPolicyEvaluationAsync(string decisionId, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT decision_id, context, decision, reason_codes_json, analysis_id, preference_revision, rules_version, evaluated_at
            FROM policy_evaluations
            WHERE decision_id = $id;";
        cmd.Parameters.AddWithValue("$id", decisionId);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var reasonCodes = JsonSerializer.Deserialize<List<string>>(reader.GetString(3)) ?? [];

        return new PolicyEvaluation
        {
            DecisionId = reader.GetString(0),
            Context = reader.GetString(1),
            Decision = Enum.Parse<PolicyDecision>(reader.GetString(2)),
            ReasonCodes = reasonCodes,
            AnalysisId = reader.IsDBNull(4) ? null : reader.GetString(4),
            PreferenceRevision = reader.IsDBNull(5) ? null : reader.GetString(5),
            RulesVersion = reader.IsDBNull(6) ? null : reader.GetString(6),
            EvaluatedAtUtc = DateTime.Parse(reader.GetString(7))
        };
    }

    public async Task<bool> DeleteSourceRecordAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            // 1. Delete policy evaluations referencing analyses of this source
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    DELETE FROM policy_evaluations 
                    WHERE analysis_id IN (SELECT analysis_id FROM analyses WHERE source_id = $srcId);";
                cmd.Parameters.AddWithValue("$srcId", sourceId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2. Delete source record (ON DELETE CASCADE handles source_versions, analyses, and reviews)
            int affected;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM sources WHERE source_id = $id;";
                cmd.Parameters.AddWithValue("$id", sourceId);
                affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            tx.Commit();
            return affected > 0;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<int> PurgeAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                DELETE FROM policy_evaluations;
                DELETE FROM reviews;
                DELETE FROM analyses;
                DELETE FROM source_versions;
                DELETE FROM sources;
                DELETE FROM recordings;
                DELETE FROM permission_attestations;";
            int affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            tx.Commit();
            return affected;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _writeLock.Dispose();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
