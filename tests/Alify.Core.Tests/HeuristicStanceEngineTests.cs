using Alify.Core.Features.Analysis.Engine;
using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Xunit;

namespace Alify.Core.Tests;

public class HeuristicStanceEngineTests
{
    private readonly HeuristicStanceEngine _engine = new();
    private readonly CanonicalLyricNormalizer _normalizer = new();

    [Fact]
    public async Task Fixture01_Violence_Rejected_AntiViolenceSong()
    {
        var lyrics = @"[Verse 1]
Put down the guns, stop the violence and save our children
We must live in peace";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        var finding = Assert.Single(result.Findings, f => f.Topic == "Violence");
        Assert.Equal(Stance.Rejected, finding.Stance);
        Assert.NotEmpty(finding.Evidence);

        var val = EvidenceValidator.ValidateAnalysis(result, source);
        Assert.True(val.IsValid, val.ErrorMessage);
    }

    [Fact]
    public async Task Fixture02_Violence_Endorsed_Bragging()
    {
        var lyrics = @"[Chorus]
Catch me with the glock, love to pull the trigger
Ready for war";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        var finding = Assert.Single(result.Findings, f => f.Topic == "Violence");
        Assert.Equal(Stance.Endorsed, finding.Stance);

        var val = EvidenceValidator.ValidateAnalysis(result, source);
        Assert.True(val.IsValid, val.ErrorMessage);
    }

    [Fact]
    public async Task Fixture03_Violence_Mentioned_NewsNarrative()
    {
        var lyrics = @"[Verse 1]
Yesterday in the news they were reporting the shooting downtown
Watching the sirens fade away";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        var finding = Assert.Single(result.Findings, f => f.Topic == "Violence");
        Assert.Equal(Stance.Mentioned, finding.Stance);

        var val = EvidenceValidator.ValidateAnalysis(result, source);
        Assert.True(val.IsValid, val.ErrorMessage);
    }

    [Fact]
    public async Task Fixture04_Violence_Ambiguous_ConflictingMarkers()
    {
        var lyrics = @"[Verse]
Stop the murder in the street but catch me with the gun";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        var finding = Assert.Single(result.Findings, f => f.Topic == "Violence");
        Assert.Equal(Stance.Ambiguous, finding.Stance);

        var val = EvidenceValidator.ValidateAnalysis(result, source);
        Assert.True(val.IsValid, val.ErrorMessage);
    }

    [Fact]
    public async Task Fixture05_SubstanceUse_Rejected_RecoverySong()
    {
        var lyrics = @"[Verse 2]
Quit the cocaine and say no to pills, it ruined my family
I never want that poison again";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        var finding = Assert.Single(result.Findings, f => f.Topic == "SubstanceUse");
        Assert.Equal(Stance.Rejected, finding.Stance);

        var val = EvidenceValidator.ValidateAnalysis(result, source);
        Assert.True(val.IsValid, val.ErrorMessage);
    }

    [Fact]
    public async Task Fixture06_SubstanceUse_Endorsed_PartySong()
    {
        var lyrics = @"[Chorus]
Pour up the liquor, love to sip that whiskey all night long
Pop a pill and let's go";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        var finding = Assert.Single(result.Findings, f => f.Topic == "SubstanceUse");
        Assert.Equal(Stance.Endorsed, finding.Stance);

        var val = EvidenceValidator.ValidateAnalysis(result, source);
        Assert.True(val.IsValid, val.ErrorMessage);
    }

    [Fact]
    public async Task Fixture07_SubstanceUse_Mentioned_Observational()
    {
        var lyrics = @"[Verse]
Saw an old bottle of whiskey on the counter
Remember when the kitchen was full";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        var finding = Assert.Single(result.Findings, f => f.Topic == "SubstanceUse");
        Assert.Equal(Stance.Mentioned, finding.Stance);

        var val = EvidenceValidator.ValidateAnalysis(result, source);
        Assert.True(val.IsValid, val.ErrorMessage);
    }

    [Fact]
    public async Task Fixture08_MultiTopic_DistinctStancesSimultaneously()
    {
        var lyrics = @"[Verse]
Put down the guns and stop the war
Pour up the liquor and celebrate tonight";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        Assert.Equal(2, result.Findings.Count);

        var violence = Assert.Single(result.Findings, f => f.Topic == "Violence");
        Assert.Equal(Stance.Rejected, violence.Stance);

        var substance = Assert.Single(result.Findings, f => f.Topic == "SubstanceUse");
        Assert.Equal(Stance.Endorsed, substance.Stance);

        var val = EvidenceValidator.ValidateAnalysis(result, source);
        Assert.True(val.IsValid, val.ErrorMessage);
    }

    [Fact]
    public async Task Fixture09_CleanSong_NoSensitiveTopics()
    {
        var lyrics = @"[Verse 1]
Walking through the garden in the afternoon sun
Flowers blooming everywhere, summer has begun";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task Fixture10_Instrumental_HeadersOnly_ReturnsInsufficientContext()
    {
        var lyrics = @"[Intro]
[Instrumental Break]
[Outro]";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        Assert.Equal(AnalysisStatus.InsufficientContext, result.Status);
        Assert.Equal("NO_LYRIC_LINES", result.ReasonCode);
    }

    [Fact]
    public async Task Fixture11_UnsupportedLanguage_Spanish_ReturnsUnsupportedLanguage()
    {
        var lyrics = "Bailando en la playa bajo las estrellas";
        var source = _normalizer.Normalize(lyrics);

        var result = await _engine.AnalyzeAsync(source, targetLanguage: "es");

        Assert.Equal(AnalysisStatus.UnsupportedLanguage, result.Status);
        Assert.Equal("es", result.Language);
    }

    [Fact]
    public async Task Fixture12_UnsupportedLanguage_French_ReturnsUnsupportedLanguage()
    {
        var lyrics = "Je ne veux pas travailler, je veux seulement voyager";
        var source = _normalizer.Normalize(lyrics);

        var result = await _engine.AnalyzeAsync(source, targetLanguage: "fr");

        Assert.Equal(AnalysisStatus.UnsupportedLanguage, result.Status);
        Assert.Equal("fr", result.Language);
    }

    [Fact]
    public async Task Fixture13_EvidenceLineNumbers_MatchExactSourceLinesAcrossBlanks()
    {
        var lyrics = @"[Intro]

[Verse 1]

Stop the violence now";

        var source = _normalizer.Normalize(lyrics);
        var result = await _engine.AnalyzeAsync(source);

        var finding = Assert.Single(result.Findings);
        var evidence = Assert.Single(finding.Evidence);

        // "Stop the violence now" is on line 5
        Assert.Equal(5, evidence.StartLine);
        Assert.Equal(5, evidence.EndLine);
        Assert.Equal("Stop the violence now", evidence.QuotedText);

        var val = EvidenceValidator.ValidateAnalysis(result, source);
        Assert.True(val.IsValid, val.ErrorMessage);
    }
}
