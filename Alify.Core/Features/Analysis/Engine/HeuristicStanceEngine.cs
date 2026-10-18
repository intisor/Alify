using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using System.Text.RegularExpressions;

namespace Alify.Core.Features.Analysis.Engine;

/// <summary>
/// Conservative heuristic baseline stance analyzer for English lyrics (ALI-019 / PRD-007).
/// Evaluates stance (Endorsed, Mentioned, Rejected, Ambiguous) using deterministic phrase patterns
/// and attaches verifiable line-level citations. Never trusts title matching or outputs ShouldSkip.
/// </summary>
public partial class HeuristicStanceEngine : IStanceAnalysisEngine
{
    public string EngineKind => "HeuristicBaseline.v1";
    public IReadOnlyList<string> SupportedLanguages => ["en"];

    // Topic keywords
    [GeneratedRegex(@"\b(violence|gun|guns|shoot|shooting|killed|killer|murder|knife|knives|stab|blood|bullet|bullets|war|glock|pistol|rifle)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ViolenceRegex();

    [GeneratedRegex(@"\b(drugs|cocaine|heroin|fentanyl|weed|pills|liquor|whiskey|vodka|codeine|lean|meth|ecstasy)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SubstanceRegex();

    // Stance pattern indicators
    [GeneratedRegex(@"\b(stop|put down|say no|never|don't|quit|quitting|ruined|regret|condemn|waste of life|against|warn|peace|save|no more)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RejectionRegex();

    [GeneratedRegex(@"\b(love|proud|catch me|pop a|pour up|light up|aim and|sip that|flex|smoke that|let it spray|pull the trigger)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EndorsementRegex();

    [GeneratedRegex(@"\b(saw|heard|reported|he was|she was|they were|yesterday|history|in the news|remember when|watching)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MentionRegex();

    public Task<AnalysisResult> AnalyzeAsync(SourceVersion source, string? targetLanguage = "en", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Language boundary check
        string lang = targetLanguage?.ToLowerInvariant() ?? "en";
        if (!SupportedLanguages.Contains(lang))
        {
            return Task.FromResult(AnalysisResult.CreateUnsupportedLanguage(source.SourceId, EngineKind, lang));
        }

        var lyricLines = source.Lines.Where(l => l.Kind == SourceLineKind.Lyric).ToList();
        if (lyricLines.Count == 0)
        {
            return Task.FromResult(new AnalysisResult
            {
                AnalysisId = Guid.NewGuid().ToString("N"),
                SourceId = source.SourceId,
                SourceVersionId = source.SourceVersionId,
                Status = AnalysisStatus.InsufficientContext,
                EngineKind = EngineKind,
                Language = lang,
                ReasonCode = "NO_LYRIC_LINES",
                Limitations = ["Document contains no sung lyric lines (may be instrumental or headers-only)"]
            });
        }

        var findings = new List<Finding>();

        // Analyze for Violence
        var violenceFinding = EvaluateTopic("Violence", ViolenceRegex(), lyricLines);
        if (violenceFinding != null)
        {
            findings.Add(violenceFinding);
        }

        // Analyze for SubstanceUse
        var substanceFinding = EvaluateTopic("SubstanceUse", SubstanceRegex(), lyricLines);
        if (substanceFinding != null)
        {
            findings.Add(substanceFinding);
        }

        var result = new AnalysisResult
        {
            AnalysisId = Guid.NewGuid().ToString("N"),
            SourceId = source.SourceId,
            SourceVersionId = source.SourceVersionId,
            Status = AnalysisStatus.Completed,
            EngineKind = EngineKind,
            Language = lang,
            Findings = findings,
            Limitations =
            [
                "Rule-based heuristic baseline limited to English keyword/negation heuristics.",
                "Complex idioms, sarcasm, or multi-stanza narratives may require human reviewer audit."
            ]
        };

        return Task.FromResult(result);
    }

    private static Finding? EvaluateTopic(string topicName, Regex topicRegex, List<SourceLine> lyricLines)
    {
        var matchingLines = lyricLines.Where(l => topicRegex.IsMatch(l.CanonicalText)).ToList();
        if (matchingLines.Count == 0)
        {
            return null; // Topic not present in lyrics
        }

        // Determine stance across evidence lines
        int rejectionScore = 0;
        int endorsementScore = 0;
        int mentionScore = 0;

        var evidenceList = new List<EvidenceReference>();

        foreach (var line in matchingLines.Take(3)) // Cap evidence to top 3 representative lines
        {
            evidenceList.Add(new EvidenceReference
            {
                StartLine = line.LineNumber,
                EndLine = line.LineNumber,
                QuotedText = line.CanonicalText,
                LineHash = line.LineHash
            });

            bool hasRejection = RejectionRegex().IsMatch(line.CanonicalText);
            bool hasEndorsement = EndorsementRegex().IsMatch(line.CanonicalText);
            bool hasMention = MentionRegex().IsMatch(line.CanonicalText);

            if (hasRejection) rejectionScore++;
            if (hasEndorsement) endorsementScore++;
            if (hasMention) mentionScore++;
        }

        Stance stance;
        string rationale;

        if (rejectionScore > 0 && endorsementScore == 0)
        {
            stance = Stance.Rejected;
            rationale = $"Lyrics condemn, negate, or warn against {topicName.ToLowerInvariant()}.";
        }
        else if (endorsementScore > 0 && rejectionScore == 0)
        {
            stance = Stance.Endorsed;
            rationale = $"Lyrics express positive commitment, bragging, or approval of {topicName.ToLowerInvariant()}.";
        }
        else if (rejectionScore > 0 && endorsementScore > 0)
        {
            stance = Stance.Ambiguous;
            rationale = $"Lyrics contain conflicting endorsement and rejection markers for {topicName.ToLowerInvariant()}.";
        }
        else if (mentionScore > 0)
        {
            stance = Stance.Mentioned;
            rationale = $"Lyrics describe or reference {topicName.ToLowerInvariant()} neutrally without clear endorsement or condemnation.";
        }
        else
        {
            stance = Stance.Mentioned;
            rationale = $"{topicName} keywords detected in lyrics without explicit endorsement or rejection.";
        }

        return new Finding
        {
            FindingId = Guid.NewGuid().ToString("N"),
            Topic = topicName,
            Stance = stance,
            Expression = ExpressionType.Literal,
            Rationale = rationale,
            Evidence = evidenceList,
            Confidence = 0.85
        };
    }
}
