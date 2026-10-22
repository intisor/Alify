using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Analysis.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alify.Core.Features.Analysis.Engine.Gemini;

/// <summary>
/// Structured Gemini stance analysis adapter (ALI-020 / PRD-011 / DATA-009).
/// Features user consent validation, transactional budget ledger reservation, bounded execution,
/// exact evidence validation against the source ledger, and honest error mapping.
/// Strictly decoupled from Spotify and execution commands.
/// </summary>
public class GeminiStanceAnalysisEngine : IStanceAnalysisEngine
{
    private readonly IGeminiModelClient _client;
    private readonly ProviderConsent _consent;
    private readonly BudgetLedger _budgetLedger;
    private readonly TimeSpan _timeout;

    public string EngineKind => $"Gemini:{_client.ModelName}";
    public IReadOnlyList<string> SupportedLanguages => ["en", "es", "fr", "de"];

    public GeminiStanceAnalysisEngine(
        IGeminiModelClient client,
        ProviderConsent consent,
        BudgetLedger budgetLedger,
        TimeSpan? timeout = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _consent = consent ?? throw new ArgumentNullException(nameof(consent));
        _budgetLedger = budgetLedger ?? throw new ArgumentNullException(nameof(budgetLedger));
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public Task<AnalysisResult> AnalyzeAsync(
        SourceVersion source,
        string? targetLanguage = "en",
        CancellationToken cancellationToken = default)
    {
        return AnalyzeAsync(source, targetLanguage, FilterProfile.General, 5, cancellationToken);
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        SourceVersion source,
        string? targetLanguage,
        FilterProfile profile,
        int strictnessLevel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // 1. Language support check
        string lang = string.IsNullOrWhiteSpace(targetLanguage) ? "en" : targetLanguage.ToLowerInvariant();
        if (!SupportedLanguages.Contains(lang))
        {
            return AnalysisResult.CreateUnsupportedLanguage(source.SourceId, EngineKind, lang);
        }

        // 2. Filter out instrumental or empty sources
        bool hasSungLyrics = source.Lines.Any(l => l.Kind == SourceLineKind.Lyric);
        if (!hasSungLyrics)
        {
            return new AnalysisResult
            {
                AnalysisId = Guid.NewGuid().ToString("N"),
                SourceId = source.SourceId,
                SourceVersionId = source.SourceVersionId,
                Status = AnalysisStatus.Completed,
                EngineKind = EngineKind,
                Language = lang,
                ReasonCode = "NO_SUNG_LYRICS_FOUND",
                Findings = [],
                Limitations = ["Source contains no sung lyric lines."]
            };
        }

        // 3. User consent gate
        if (!_consent.IsConsented)
        {
            return AnalysisResult.CreateUnavailable(source.SourceId, EngineKind, "CONSENT_NOT_GRANTED");
        }

        // 4. Transactional budget reservation gate
        if (!_budgetLedger.TryReserve())
        {
            return AnalysisResult.CreateUnavailable(source.SourceId, EngineKind, "DAILY_BUDGET_EXCEEDED");
        }

        // 5. Construct structured prompt
        string prompt = BuildStructuredPrompt(source, lang, profile, strictnessLevel);

        string responseContent;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_timeout);

        try
        {
            responseContent = await _client.GenerateContentAsync(prompt, linkedCts.Token);
            _budgetLedger.CompleteReservation();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _budgetLedger.ReleaseReservation();
            return AnalysisResult.CreateUnavailable(source.SourceId, EngineKind, "PROVIDER_REQUEST_TIMEOUT");
        }
        catch (Exception ex)
        {
            _budgetLedger.ReleaseReservation();
            return AnalysisResult.CreateUnavailable(source.SourceId, EngineKind, $"PROVIDER_REQUEST_FAILED_{ex.GetType().Name}");
        }

        // 6. Parse and validate structured output
        return ParseAndValidateResponse(responseContent, source, lang);
    }

    private static string BuildStructuredPrompt(SourceVersion source, string language, FilterProfile profile = FilterProfile.General, int strictnessLevel = 5)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an advanced lyrical content analysis engine.");
        sb.AppendLine($"Worldview Ethos Profile: {profile}. Strictness Level: {strictnessLevel} of 10.");
        sb.AppendLine("Analyze the provided song lyrics line by line for stances on topics: 'violence', 'substance_use', 'blasphemy', 'sexual_content', 'occult', 'profanity'.");
        
        if (profile == FilterProfile.Islamic)
        {
            sb.AppendLine("Islamic Ethos Focus: Rigorously examine for Shirk/Blasphemy, Intoxicants/Alcohol (Khamr), Sexual Explicitness/Fahisha, and Violence.");
        }
        else if (profile == FilterProfile.Christian)
        {
            sb.AppendLine("Christian Ethos Focus: Rigorously examine for Blasphemy/taking the Lord's name in vain, Demonic/Occult glorification, Sexual Immorality, and Violence.");
        }
        else if (profile == FilterProfile.FamilyFriendly)
        {
            sb.AppendLine("Family-Friendly Focus: Strictly identify vulgarity, sexual references, drug use, and violence.");
        }

        sb.AppendLine($"Strictness guidance: {strictnessLevel}/10 (1=lenient: only gratuitous glorification flagged; 5=standard; 10=strict zero-tolerance: any mention, storytelling, or ambiguous allusion must be flagged).");
        sb.AppendLine("For each topic present, determine the Stance: 'Endorsed', 'Mentioned', 'Rejected', or 'Ambiguous'.");
        sb.AppendLine("Determine Expression: 'Literal', 'Figurative', or 'Unknown'.");
        sb.AppendLine("CRITICAL RULE: Every finding MUST include verifiable Evidence citations referencing the exact 1-based StartLine, EndLine, and exact QuotedText from the lines below.");
        sb.AppendLine("Do not invent, hallucinate, or rewrite quotes. Output strictly valid JSON conforming to the schema without markdown formatting.");
        sb.AppendLine();
        sb.AppendLine("JSON Schema:");
        sb.AppendLine(@"{
  ""language"": ""en"",
  ""findings"": [
    {
      ""findingId"": ""f-1"",
      ""topic"": ""violence"",
      ""stance"": ""Endorsed"",
      ""expression"": ""Literal"",
      ""rationale"": ""Detailed rationale"",
      ""confidence"": 0.95,
      ""evidence"": [
        { ""startLine"": 1, ""endLine"": 1, ""quotedText"": ""exact quoted text"" }
      ]
    }
  ],
  ""limitations"": [""any limitations""]
}");
        sb.AppendLine();
        sb.AppendLine("Numbered Lyrics:");
        foreach (var line in source.Lines)
        {
            if (line.Kind == SourceLineKind.Lyric)
            {
                sb.AppendLine($"[{line.LineNumber}] {line.CanonicalText}");
            }
        }

        return sb.ToString();
    }

    private AnalysisResult ParseAndValidateResponse(string rawResponse, SourceVersion source, string language)
    {
        string cleanedJson = CleanJsonText(rawResponse);

        GeminiResponseDto? dto;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            dto = JsonSerializer.Deserialize<GeminiResponseDto>(cleanedJson, options);
        }
        catch (JsonException)
        {
            return new AnalysisResult
            {
                AnalysisId = Guid.NewGuid().ToString("N"),
                SourceId = source.SourceId,
                SourceVersionId = source.SourceVersionId,
                Status = AnalysisStatus.InvalidOutput,
                EngineKind = EngineKind,
                Language = language,
                ReasonCode = "INVALID_JSON_OUTPUT",
                Limitations = ["Model returned malformed or non-JSON output."]
            };
        }

        if (dto == null)
        {
            return new AnalysisResult
            {
                AnalysisId = Guid.NewGuid().ToString("N"),
                SourceId = source.SourceId,
                SourceVersionId = source.SourceVersionId,
                Status = AnalysisStatus.InvalidOutput,
                EngineKind = EngineKind,
                Language = language,
                ReasonCode = "EMPTY_MODEL_RESPONSE"
            };
        }

        var findings = new List<Finding>();
        int findingIndex = 1;

        foreach (var findingDto in dto.Findings ?? [])
        {
            if (string.IsNullOrWhiteSpace(findingDto.Topic)) continue;

            if (!Enum.TryParse<Stance>(findingDto.Stance, true, out var stance))
            {
                stance = Stance.Ambiguous;
            }

            if (!Enum.TryParse<ExpressionType>(findingDto.Expression, true, out var expression))
            {
                expression = ExpressionType.Unknown;
            }

            var evidenceList = new List<EvidenceReference>();
            foreach (var ev in findingDto.Evidence ?? [])
            {
                if (ev.StartLine >= 1 && ev.EndLine >= ev.StartLine && !string.IsNullOrWhiteSpace(ev.QuotedText))
                {
                    evidenceList.Add(new EvidenceReference
                    {
                        StartLine = ev.StartLine,
                        EndLine = ev.EndLine,
                        QuotedText = ev.QuotedText.Trim()
                    });
                }
            }

            findings.Add(new Finding
            {
                FindingId = string.IsNullOrWhiteSpace(findingDto.FindingId) ? $"gemini-f-{findingIndex++}" : findingDto.FindingId,
                Topic = findingDto.Topic.Trim().ToLowerInvariant(),
                Stance = stance,
                Expression = expression,
                Rationale = findingDto.Rationale ?? "Extracted by Gemini structured adapter",
                Confidence = findingDto.Confidence,
                Evidence = evidenceList
            });
        }

        var result = new AnalysisResult
        {
            AnalysisId = Guid.NewGuid().ToString("N"),
            SourceId = source.SourceId,
            SourceVersionId = source.SourceVersionId,
            Status = AnalysisStatus.Completed,
            EngineKind = EngineKind,
            Language = dto.Language ?? language,
            ReasonCode = "ANALYSIS_COMPLETE",
            Findings = findings,
            Limitations = dto.Limitations ?? []
        };

        // 7. Strictly validate all evidence citations against the source ledger (reject hallucinations)
        var validation = EvidenceValidator.ValidateAnalysis(result, source);
        if (!validation.IsValid)
        {
            return new AnalysisResult
            {
                AnalysisId = result.AnalysisId,
                SourceId = source.SourceId,
                SourceVersionId = source.SourceVersionId,
                Status = AnalysisStatus.InvalidOutput,
                EngineKind = EngineKind,
                Language = language,
                ReasonCode = "FABRICATED_OR_INVALID_EVIDENCE_CITATION",
                Limitations = [validation.ErrorMessage ?? "Evidence citation failed verification against canonical ledger."]
            };
        }

        return result;
    }

    private static string CleanJsonText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var trimmed = text.Trim();

        // 1. Check for ```json ... ``` code fence anywhere in text
        int jsonBlockStart = trimmed.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonBlockStart >= 0)
        {
            int contentStart = jsonBlockStart + "```json".Length;
            int jsonBlockEnd = trimmed.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (jsonBlockEnd > contentStart)
            {
                return trimmed[contentStart..jsonBlockEnd].Trim();
            }
        }

        // 2. Check for generic ``` ... ``` code fence
        int genericBlockStart = trimmed.IndexOf("```", StringComparison.Ordinal);
        if (genericBlockStart >= 0)
        {
            int contentStart = genericBlockStart + "```".Length;
            int genericBlockEnd = trimmed.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (genericBlockEnd > contentStart)
            {
                return trimmed[contentStart..genericBlockEnd].Trim();
            }
        }

        // 3. Fallback: extract substring between first '{' and last '}'
        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
        }

        return trimmed;
    }

    private record GeminiResponseDto
    {
        public string? Language { get; init; }
        public List<GeminiFindingDto>? Findings { get; init; }
        public List<string>? Limitations { get; init; }
    }

    private record GeminiFindingDto
    {
        public string? FindingId { get; init; }
        public string? Topic { get; init; }
        public string? Stance { get; init; }
        public string? Expression { get; init; }
        public string? Rationale { get; init; }
        public double? Confidence { get; init; }
        public List<GeminiEvidenceDto>? Evidence { get; init; }
    }

    private record GeminiEvidenceDto
    {
        public int StartLine { get; init; }
        public int EndLine { get; init; }
        public string? QuotedText { get; init; }
    }
}
