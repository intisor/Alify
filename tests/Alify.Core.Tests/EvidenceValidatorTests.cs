using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Xunit;

namespace Alify.Core.Tests;

public class EvidenceValidatorTests
{
    private readonly CanonicalLyricNormalizer _normalizer = new();

    private SourceVersion CreateSampleSource()
    {
        var lyrics = @"[Verse 1: Kendrick]
I remember syrup sandwiches and crime allowances
Finesse a ninja with some counterfeits
[Chorus]
Be humble, sit down";

        return _normalizer.Normalize(lyrics);
    }

    [Fact]
    public void ValidateCitation_MatchingCitation_Succeeds()
    {
        var source = CreateSampleSource();

        var citation = new EvidenceReference
        {
            StartLine = 2,
            EndLine = 3,
            QuotedText = "I remember syrup sandwiches and crime allowances\nFinesse a ninja with some counterfeits"
        };

        var result = EvidenceValidator.ValidateCitation(citation, source);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateCitation_FabricatedOrAlteredQuote_Fails()
    {
        var source = CreateSampleSource();

        var citation = new EvidenceReference
        {
            StartLine = 2,
            EndLine = 2,
            QuotedText = "This quote was hallucinated by an LLM"
        };

        var result = EvidenceValidator.ValidateCitation(citation, source);

        Assert.False(result.IsValid);
        Assert.Contains("Fabricated citation rejected", result.ErrorMessage);
    }

    [Fact]
    public void ValidateCitation_OutOfBoundsLines_Fails()
    {
        var source = CreateSampleSource();

        var citation = new EvidenceReference
        {
            StartLine = 1,
            EndLine = 100, // source only has 5 lines
            QuotedText = "any text"
        };

        var result = EvidenceValidator.ValidateCitation(citation, source);

        Assert.False(result.IsValid);
        Assert.Contains("exceeds document length", result.ErrorMessage);
    }

    [Fact]
    public void ValidateCitation_SectionHeaderOnly_Fails()
    {
        var source = CreateSampleSource();

        // Line 4 is "[Chorus]"
        var citation = new EvidenceReference
        {
            StartLine = 4,
            EndLine = 4,
            QuotedText = "[Chorus]"
        };

        var result = EvidenceValidator.ValidateCitation(citation, source);

        Assert.False(result.IsValid);
        Assert.Contains("contains only section headers", result.ErrorMessage);
    }

    [Fact]
    public void ValidateAnalysis_StanceWithoutEvidence_Fails()
    {
        var source = CreateSampleSource();

        var analysis = new AnalysisResult
        {
            AnalysisId = "a1",
            SourceId = source.SourceId,
            Status = AnalysisStatus.Completed,
            EngineKind = "Test",
            Findings =
            [
                new Finding
                {
                    FindingId = "f1",
                    Topic = "Crime",
                    Stance = Stance.Mentioned,
                    Expression = ExpressionType.Literal,
                    Rationale = "No evidence provided",
                    Evidence = [] // Empty evidence violates DATA-003 rule
                }
            ]
        };

        var result = EvidenceValidator.ValidateAnalysis(analysis, source);

        Assert.False(result.IsValid);
        Assert.Contains("without any supporting evidence citations", result.ErrorMessage);
    }
}
