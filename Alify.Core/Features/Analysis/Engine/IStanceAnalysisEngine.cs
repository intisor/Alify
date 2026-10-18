using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;

namespace Alify.Core.Features.Analysis.Engine;

/// <summary>
/// Contract for semantic stance analysis engines (ALI-014 / ALI-019).
/// Produces neutral stance findings with verifiable evidence citations.
/// Never contains ShouldSkip, execution commands, or Spotify dependencies.
/// </summary>
public interface IStanceAnalysisEngine
{
    string EngineKind { get; }
    IReadOnlyList<string> SupportedLanguages { get; }

    Task<AnalysisResult> AnalyzeAsync(SourceVersion source, string? targetLanguage = "en", CancellationToken cancellationToken = default);
}
