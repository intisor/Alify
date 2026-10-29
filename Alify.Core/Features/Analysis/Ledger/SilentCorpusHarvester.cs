using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Analysis.Policy;

namespace Alify.Core.Features.Analysis.Ledger;

/// <summary>
/// Background ledger that silently records structured song evaluations (SFT format)
/// and reviewer verdict overrides (DPO preference pairs) into persistent local storage.
/// </summary>
public static class SilentCorpusHarvester
{
    private static readonly ConcurrentDictionary<string, byte> SeenEvaluations = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object FileLock = new();

    public static void RecordEvaluation(
        string trackName,
        string artistName,
        FilterProfile profile,
        int strictnessLevel,
        string? lyrics,
        AnalysisResult result,
        bool isFlagged,
        string? flagReason)
    {
        if (string.IsNullOrWhiteSpace(lyrics) || result.Status != AnalysisStatus.Completed) return;

        var dedupKey = $"{artistName.Trim()}::{trackName.Trim()}::{profile}::{strictnessLevel}";
        if (!SeenEvaluations.TryAdd(dedupKey, 1)) return;

        _ = Task.Run(() =>
        {
            try
            {
                var record = new
                {
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    track = trackName,
                    artist = artistName,
                    ethos = profile.ToString(),
                    strictness = strictnessLevel,
                    instruction = $"Analyze the song lyrics for {profile} ethos (strictness {strictnessLevel}/10).",
                    input = $"Song: \"{trackName}\" by \"{artistName}\"\nLyrics:\n{(lyrics.Length > 4000 ? lyrics[..4000] : lyrics)}",
                    output = JsonSerializer.Serialize(new
                    {
                        flagged = isFlagged,
                        reason = flagReason ?? "Clean",
                        findings = result.Findings.Select(f => new
                        {
                            topic = f.Topic,
                            stance = f.Stance.ToString(),
                            expression = f.Expression.ToString(),
                            rationale = f.Rationale,
                            evidence = f.Evidence.Select(e => e.QuotedText).ToArray()
                        }).ToArray()
                    })
                };

                AppendJsonLine("sft_ledger.jsonl", JsonSerializer.Serialize(record));
            }
            catch
            {
                // Silent background operation
            }
        });
    }

    public static void RecordPreferenceOverride(
        string sourceId,
        string? lyrics,
        string chosenDisposition,
        string chosenRationale,
        AnalysisResult originalAnalysis)
    {
        _ = Task.Run(() =>
        {
            try
            {
                var record = new
                {
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    source_id = sourceId,
                    prompt = $"Evaluate song '{sourceId}' lyrics:\n{(lyrics != null && lyrics.Length > 4000 ? lyrics[..4000] : lyrics ?? string.Empty)}",
                    chosen = JsonSerializer.Serialize(new
                    {
                        disposition = chosenDisposition,
                        rationale = chosenRationale
                    }),
                    rejected = JsonSerializer.Serialize(new
                    {
                        engine = originalAnalysis.EngineKind,
                        findings = originalAnalysis.Findings.Select(f => new { f.Topic, Stance = f.Stance.ToString(), f.Rationale }).ToArray()
                    })
                };

                AppendJsonLine("dpo_ledger.jsonl", JsonSerializer.Serialize(record));
            }
            catch
            {
                // Silent background operation
            }
        });
    }

    private static void AppendJsonLine(string fileName, string jsonLine)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Alify", ".vault");
        lock (FileLock)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.AppendAllText(Path.Combine(dir, fileName), jsonLine + Environment.NewLine, Encoding.UTF8);
        }
    }
}
