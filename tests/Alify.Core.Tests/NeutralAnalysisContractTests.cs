using Alify.Core.Features.Analysis.Models;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Alify.Core.Tests;

public class NeutralAnalysisContractTests
{
    [Fact]
    public void AnalysisResult_DoesNotContainShouldSkipOrPlaybackDependencies()
    {
        // Enforce ALI-014 architectural boundary:
        // Analyzer output describes meaning and uncertainty only. It must NEVER have ShouldSkip or Spotify controls.
        var type = typeof(AnalysisResult);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(properties, p => p.Name.Equals("ShouldSkip", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, p => p.Name.Equals("Skip", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, p => p.PropertyType.FullName?.Contains("Spotify") == true);
    }

    [Fact]
    public void AnalysisResult_SerializationRoundtrip_PreservesEvidenceAndStance()
    {
        var result = new AnalysisResult
        {
            AnalysisId = "analysis-123",
            SourceId = "source-456",
            Status = AnalysisStatus.Completed,
            EngineKind = "LocalRules",
            Language = "en",
            Findings =
            [
                new Finding
                {
                    FindingId = "finding-1",
                    Topic = "Violence",
                    Stance = Stance.Rejected,
                    Expression = ExpressionType.Literal,
                    Rationale = "Direct criticism of violence in urban neighborhoods",
                    Evidence =
                    [
                        new EvidenceReference
                        {
                            StartLine = 12,
                            EndLine = 14,
                            QuotedText = "Put down the weapons, stop the war",
                            LineHash = "sha256:abc123"
                        }
                    ],
                    Confidence = 0.95
                }
            ],
            Limitations = ["Evaluated with baseline v1 dictionary"]
        };

        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<AnalysisResult>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("analysis-123", deserialized.AnalysisId);
        Assert.Equal(AnalysisStatus.Completed, deserialized.Status);
        Assert.Single(deserialized.Findings);

        var finding = deserialized.Findings[0];
        Assert.Equal("Violence", finding.Topic);
        Assert.Equal(Stance.Rejected, finding.Stance);
        Assert.Equal(ExpressionType.Literal, finding.Expression);
        Assert.Single(finding.Evidence);
        Assert.Equal(12, finding.Evidence[0].StartLine);
        Assert.Equal("Put down the weapons, stop the war", finding.Evidence[0].QuotedText);
    }

    [Theory]
    [InlineData(PolicyDecision.Allow)]
    [InlineData(PolicyDecision.RecommendSkip)]
    [InlineData(PolicyDecision.Abstain)]
    public void PolicyEvaluation_CreatesValidDecisions(PolicyDecision decision)
    {
        var policy = decision switch
        {
            PolicyDecision.Allow => PolicyEvaluation.Allow("StandaloneLyrics", "a1", "Clean content"),
            PolicyDecision.RecommendSkip => PolicyEvaluation.RecommendSkip("StandaloneLyrics", "Endorsed hate speech", "a1"),
            PolicyDecision.Abstain => PolicyEvaluation.Abstain("StandaloneLyrics", "Unsupported language", "a1"),
            _ => throw new ArgumentOutOfRangeException(nameof(decision))
        };

        Assert.Equal(decision, policy.Decision);
        Assert.Equal("StandaloneLyrics", policy.Context);
        Assert.NotEmpty(policy.ReasonCodes);
    }
}
