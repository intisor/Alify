using Alify.Core.Features.Analysis.Engine.Gemini;
using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Xunit;

namespace Alify.Core.Tests;

public class GeminiStanceAnalysisEngineTests
{
    private class FakeGeminiClient : IGeminiModelClient
    {
        public string ModelName { get; init; } = "gemini-2.5-flash";
        public Func<string, Task<string>> ResponseProvider { get; set; } = _ => Task.FromResult("{}");
        public int CallCount { get; private set; }

        public Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ResponseProvider(prompt);
        }
    }

    private static SourceVersion CreateSampleSource(params string[] lines)
    {
        var sourceLines = lines.Select((text, i) => new SourceLine
        {
            LineNumber = i + 1,
            RawText = text,
            CanonicalText = text,
            Kind = text.StartsWith("[") && text.EndsWith("]") ? SourceLineKind.Section : SourceLineKind.Lyric,
            SectionName = text.StartsWith("[") && text.EndsWith("]") ? text.Trim('[', ']') : null
        }).ToList();

        return new SourceVersion
        {
            SourceId = "src-gemini-test",
            SourceVersionId = "ver-1",
            RawContentHash = "raw",
            CanonicalContentHash = "canon",
            NormalizerVersion = "1.0.0",
            Lines = sourceLines
        };
    }

    [Fact]
    public async Task AnalyzeAsync_WhenConsentDenied_ReturnsUnavailableWithoutCallingClient()
    {
        var client = new FakeGeminiClient();
        var consent = ProviderConsent.Denied;
        var budget = new BudgetLedger(10);
        var engine = new GeminiStanceAnalysisEngine(client, consent, budget);

        var source = CreateSampleSource("We fight for freedom", "Never surrender");
        var result = await engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Unavailable, result.Status);
        Assert.Equal("CONSENT_NOT_GRANTED", result.ReasonCode);
        Assert.Equal(0, client.CallCount);
        Assert.Equal(10, budget.RemainingCapacity);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenBudgetExceeded_ReturnsUnavailableWithoutCallingClient()
    {
        var client = new FakeGeminiClient();
        var consent = ProviderConsent.Granted();
        var budget = new BudgetLedger(1);
        var engine = new GeminiStanceAnalysisEngine(client, consent, budget);

        var source = CreateSampleSource("We fight for freedom", "Never surrender");

        // First call consumes the 1 available slot
        client.ResponseProvider = _ => Task.FromResult(@"{ ""findings"": [] }");
        var result1 = await engine.AnalyzeAsync(source);
        Assert.Equal(AnalysisStatus.Completed, result1.Status);
        Assert.Equal(0, budget.RemainingCapacity);

        // Second call should be denied by budget ledger
        var result2 = await engine.AnalyzeAsync(source);
        Assert.Equal(AnalysisStatus.Unavailable, result2.Status);
        Assert.Equal("DAILY_BUDGET_EXCEEDED", result2.ReasonCode);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenUnsupportedLanguage_ReturnsUnsupportedLanguage()
    {
        var client = new FakeGeminiClient();
        var consent = ProviderConsent.Granted();
        var budget = new BudgetLedger(10);
        var engine = new GeminiStanceAnalysisEngine(client, consent, budget);

        var source = CreateSampleSource("Konnichiwa sekai");
        var result = await engine.AnalyzeAsync(source, targetLanguage: "ja");

        Assert.Equal(AnalysisStatus.UnsupportedLanguage, result.Status);
        Assert.Equal("LANGUAGE_NOT_SUPPORTED", result.ReasonCode);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenInstrumental_ReturnsCompletedWithNoFindings()
    {
        var client = new FakeGeminiClient();
        var consent = ProviderConsent.Granted();
        var budget = new BudgetLedger(10);
        var engine = new GeminiStanceAnalysisEngine(client, consent, budget);

        var source = CreateSampleSource("[Instrumental Break]");
        var result = await engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        Assert.Equal("NO_SUNG_LYRICS_FOUND", result.ReasonCode);
        Assert.Empty(result.Findings);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenProviderFails_ReleasesBudgetReservation()
    {
        var client = new FakeGeminiClient
        {
            ResponseProvider = _ => throw new HttpRequestException("Gemini service unavailable")
        };
        var consent = ProviderConsent.Granted();
        var budget = new BudgetLedger(5);
        var engine = new GeminiStanceAnalysisEngine(client, consent, budget);

        var source = CreateSampleSource("Some lyric text");
        var result = await engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Unavailable, result.Status);
        Assert.StartsWith("PROVIDER_REQUEST_FAILED", result.ReasonCode);
        // Budget reservation must be released, restoring capacity
        Assert.Equal(5, budget.RemainingCapacity);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenInvalidJson_ReturnsInvalidOutput()
    {
        var client = new FakeGeminiClient
        {
            ResponseProvider = _ => Task.FromResult("I am an AI and cannot analyze this song.")
        };
        var consent = ProviderConsent.Granted();
        var budget = new BudgetLedger(10);
        var engine = new GeminiStanceAnalysisEngine(client, consent, budget);

        var source = CreateSampleSource("Some lyric text");
        var result = await engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.InvalidOutput, result.Status);
        Assert.Equal("INVALID_JSON_OUTPUT", result.ReasonCode);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenHallucinatedEvidence_RejectsFindingAsInvalidOutput()
    {
        // Model claims quote is "shoot everyone" which is NOT in the source lyrics!
        var client = new FakeGeminiClient
        {
            ResponseProvider = _ => Task.FromResult(@"{
                ""language"": ""en"",
                ""findings"": [
                    {
                        ""findingId"": ""f-1"",
                        ""topic"": ""violence"",
                        ""stance"": ""Endorsed"",
                        ""expression"": ""Literal"",
                        ""rationale"": ""Advocates violence"",
                        ""evidence"": [
                            { ""startLine"": 1, ""endLine"": 1, ""quotedText"": ""shoot everyone"" }
                        ]
                    }
                ]
            }")
        };
        var consent = ProviderConsent.Granted();
        var budget = new BudgetLedger(10);
        var engine = new GeminiStanceAnalysisEngine(client, consent, budget);

        var source = CreateSampleSource("peace and love to all");
        var result = await engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.InvalidOutput, result.Status);
        Assert.Equal("FABRICATED_OR_INVALID_EVIDENCE_CITATION", result.ReasonCode);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenValidEvidenceAndJson_ReturnsCompleted()
    {
        var client = new FakeGeminiClient
        {
            ResponseProvider = _ => Task.FromResult(@"```json
            {
                ""language"": ""en"",
                ""findings"": [
                    {
                        ""findingId"": ""f-101"",
                        ""topic"": ""violence"",
                        ""stance"": ""Rejected"",
                        ""expression"": ""Literal"",
                        ""rationale"": ""Explicit condemnation of violence"",
                        ""confidence"": 0.98,
                        ""evidence"": [
                            { ""startLine"": 1, ""endLine"": 1, ""quotedText"": ""lay down your weapons"" }
                        ]
                    }
                ],
                ""limitations"": [""Analyzed verse only""]
            }
            ```")
        };
        var consent = ProviderConsent.Granted();
        var budget = new BudgetLedger(10);
        var engine = new GeminiStanceAnalysisEngine(client, consent, budget);

        var source = CreateSampleSource("lay down your weapons", "no more fighting");
        var result = await engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        Assert.Single(result.Findings);
        Assert.Equal("violence", result.Findings[0].Topic);
        Assert.Equal(Stance.Rejected, result.Findings[0].Stance);
        Assert.Equal(ExpressionType.Literal, result.Findings[0].Expression);
        Assert.Single(result.Findings[0].Evidence);
        Assert.Equal("lay down your weapons", result.Findings[0].Evidence[0].QuotedText);
        Assert.Equal(1, result.Findings[0].Evidence[0].StartLine);
        Assert.Equal(1, budget.CompletedCount);
        Assert.Equal(9, budget.RemainingCapacity);
    }
}
