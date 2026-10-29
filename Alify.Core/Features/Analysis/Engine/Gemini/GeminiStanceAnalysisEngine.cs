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
        sb.AppendLine("You are an expert computational sociolinguist, dialect forensic analyst, and lyrical content analysis engine.");
        sb.AppendLine($"Worldview Ethos Profile: {profile}. Strictness Level: {strictnessLevel} of 10.");
        sb.AppendLine("Analyze the provided song lyrics line by line for stances on topics: 'violence', 'substance_use', 'blasphemy', 'sexual_content', 'occult', 'profanity'.");
        
        if (profile == FilterProfile.Islamic)
        {
            sb.AppendLine("Islamic Ethos Focus: Rigorously examine for Shirk/Blasphemy, Intoxicants/Alcohol (Khamr), Sexual Explicitness/Fahisha (including coded bedroom innuendo), and Violence.");
        }
        else if (profile == FilterProfile.Christian)
        {
            sb.AppendLine("Christian Ethos Focus: Rigorously examine for Blasphemy/taking the Lord's name in vain, Demonic/Occult glorification, Sexual Immorality/Lust, and Violence.");
        }
        else if (profile == FilterProfile.FamilyFriendly)
        {
            sb.AppendLine("Family-Friendly Focus: Strictly identify vulgarity, sexual innuendo/references, drug use, and violence.");
        }

        sb.AppendLine($"Strictness guidance: {strictnessLevel}/10 (1=lenient: only gratuitous glorification flagged; 5=standard; 10=strict zero-tolerance: any mention, storytelling, or ambiguous allusion must be flagged).");
        sb.AppendLine();
        sb.AppendLine("FORENSIC SUBTEXT & DE-CLOAKING PROTOCOL:");
        sb.AppendLine("1. Dialect & Slang De-cloaking: Scrutinize non-standard dialects, regional street slang, idioms, and code-switching across any language or subculture. Reject literal dictionary translations; determine the operational subcultural meaning in adult/club contexts.");
        sb.AppendLine("2. Camouflage Stripping:");
        sb.AppendLine("   - Kinetic/Dance Decoys: Scrutinize movement and rhythm verbs where omitted direct objects refer to physical anatomy, twerking, or intimate contact.");
        sb.AppendLine("   - Consumption Metaphors: Examine eating, tasting, licking, or sweet food references used as euphemisms for sexual gratification.");
        sb.AppendLine("   - Transactional Dynamics: Detect framing where physical intimacy is exchanged for money, status, or luxury gifts.");
        sb.AppendLine("   - Compliment Decoys: Strip away polite flattery or conscious themes if the core operative bars drive toward physical arousal, vulgarity, or illicit acts.");
        sb.AppendLine("3. Unmasked English Equivalence: If the unvarnished meaning of any passage would warrant daytime radio censorship, commercial broadcast beeping, or an explicit/18+ advisory in plain English, it MUST be flagged.");
        sb.AppendLine();
        sb.AppendLine("CRITICAL SINGLE-VERSE ZERO-TOLERANCE RULE:");
        sb.AppendLine("Even if ONLY A SINGLE LINE OR VERSE violates the ethos, you MUST report it as a finding with Stance 'Endorsed' or 'Mentioned'. In Alify's policy engine, a single offending verse causes the ENTIRE song to be flagged and skipped. Never excuse, average out, or ignore a single explicit verse simply because the surrounding verses or chorus are clean.");
        sb.AppendLine();
        sb.AppendLine("CRITICAL EVIDENCE RULE:");
        sb.AppendLine("In the 'evidence' array, 'quotedText' MUST BE THE EXACT verbatim text from the numbered lyric line below (required for cryptographic ledger verification). Put your unmasked English explanation and dialect breakdown in the 'rationale' field.");
        sb.AppendLine("For each topic present, determine the Stance: 'Endorsed', 'Mentioned', 'Rejected', or 'Ambiguous'.");
        sb.AppendLine("Determine Expression: 'Literal', 'Figurative', or 'Unknown'.");
        sb.AppendLine("Output strictly valid JSON conforming to the schema without markdown formatting.");
        sb.AppendLine();
        sb.AppendLine("JSON Schema:");
        sb.AppendLine(@"{
  ""language"": ""en"",
  ""findings"": [
    {
      ""findingId"": ""f-1"",
      ""topic"": ""sexual_content"",
      ""stance"": ""Endorsed"",
      ""expression"": ""Figurative"",
      ""rationale"": ""Detailed sociolinguistic breakdown and unmasked English equivalent"",
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
                    var rawQuote = ev.QuotedText.Trim();
                    var candidateRef = new EvidenceReference
                    {
                        StartLine = ev.StartLine,
                        EndLine = ev.EndLine,
                        QuotedText = rawQuote
                    };

                    // Auto-snap line numbers / substring quotes to canonical lyric lines if the LLM
                    // had an off-by-one line index (due to headers/blank lines) or quoted a clause of a line,
                    // while preserving strict rejection for genuinely fabricated quotes.
                    if (!EvidenceValidator.ValidateCitation(candidateRef, source).IsValid)
                    {
                        var matchedLine = source.Lines.FirstOrDefault(l =>
                            l.Kind == SourceLineKind.Lyric &&
                            string.Equals(l.CanonicalText, rawQuote, StringComparison.OrdinalIgnoreCase))
                            ?? (rawQuote.Length >= 4
                                ? source.Lines.FirstOrDefault(l =>
                                    l.Kind == SourceLineKind.Lyric &&
                                    (l.CanonicalText.Contains(rawQuote, StringComparison.OrdinalIgnoreCase) ||
                                     (l.CanonicalText.Length >= 4 && rawQuote.Contains(l.CanonicalText, StringComparison.OrdinalIgnoreCase))))
                                : null);

                        if (matchedLine != null)
                        {
                            candidateRef = new EvidenceReference
                            {
                                StartLine = matchedLine.LineNumber,
                                EndLine = matchedLine.LineNumber,
                                QuotedText = matchedLine.CanonicalText
                            };
                        }
                    }

                    evidenceList.Add(candidateRef);
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

        // Strip <think>...</think> reasoning blocks emitted by Oct 2026 reasoning models (e.g., Qwen 3.8)
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"<think>[\s\S]*?</think>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        var trimmed = string.IsNullOrWhiteSpace(cleaned) ? text.Trim() : cleaned;

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
