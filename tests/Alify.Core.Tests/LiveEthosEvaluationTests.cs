using Alify.Core.Features.Analysis.Engine.Gemini;
using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Analysis.Policy;
using Xunit;
using Xunit.Abstractions;

namespace Alify.Core.Tests;

public class LiveEthosEvaluationTests
{
    private readonly ITestOutputHelper _output;
    private const string GEMINI_API_KEY = "AIzaSyBEhdVCtri2j6YrF1hAKdZnPk6crcBy1CQ";

    public LiveEthosEvaluationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static SourceVersion CreateSource(string title, string rawLyrics)
    {
        var normalizer = new CanonicalLyricNormalizer();
        return normalizer.Normalize(rawLyrics, sourceId: title);
    }

    [Fact]
    public async Task Test_NairaMarley_Aye_Under_IslamicEthos_Strictness10()
    {
        using var httpClient = new HttpClient();
        var client = new HttpGeminiModelClient(httpClient, GEMINI_API_KEY, "gemini-2.5-flash");
        var engine = new GeminiStanceAnalysisEngine(client, ProviderConsent.Granted("gemini-2.5-flash"), new BudgetLedger(100));

        // Actual scraped lyrics of Naira Marley - Aye
        string lyrics = """
            Yo! Yo Rexxie pon this one
            Shebi tele tele, lama fo fun won wipe
            K'oma wa l'oru, toripe awa ti jade
            Amo ni sinyi o
            Ton ba wa losan, ton ba wa loru
            Awa n be ni be, awa o de le fo fun won
            Toripe awa tingasa, awa o de le sa fun won
            Aye yi o le o (aye yi o le)
            Aye lo m'aye le (aye lo m'aye le)
            Ninu aye lo ti di f'aye, aye lo m'aye le (Aye lo m'aye le)
            O n pe oku iya yi ni (eyin le ma sin iya yin)
            Oku your mother (eyin le ma sin iya yin)
            Oku momsi yin (eyin le ma sin iya yin)
            Oku your mama (eyin le ma sin iya yin)
            Nigba tin jale
            Tin mi o ba teni kan je, ti mo ni ti're ma da
            Wo ko n binu ori lasan
            Ori ma mu won
            Owo ma ba won
            Ti olorun ba ti gba'dura mi, bukata mi a fu'ye
            Aye yi o le o
            """;

        var source = CreateSource("Naira Marley - Aye", lyrics);

        var result = await engine.AnalyzeAsync(
            source,
            targetLanguage: "en",
            profile: FilterProfile.Islamic,
            strictnessLevel: 10
        );

        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"ReasonCode: {result.ReasonCode}");
        _output.WriteLine($"Limitations: {string.Join(", ", result.Limitations)}");
        _output.WriteLine($"Language: {result.Language}");
        _output.WriteLine($"Findings count: {result.Findings.Count}");

        foreach (var finding in result.Findings)
        {
            _output.WriteLine($" - Topic: {finding.Topic}, Stance: {finding.Stance}, Conf: {finding.Confidence:F2}");
            _output.WriteLine($"   Rationale: {finding.Rationale}");
            foreach (var ev in finding.Evidence)
            {
                _output.WriteLine($"   Evidence [L{ev.StartLine}-L{ev.EndLine}]: \"{ev.QuotedText}\"");
            }
        }

        var userPrefs = new UserFilterPreferences
        {
            Profile = FilterProfile.Islamic,
            StrictnessLevel = 10,
            FilterSpotifyExplicit = false
        };

        var policyEval = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(result, userPrefs.ToPolicyPreferences());
        _output.WriteLine($"Policy Decision: {policyEval.Decision}");
        _output.WriteLine($"Reasons: {string.Join(", ", policyEval.ReasonCodes)}");

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        Assert.NotEmpty(result.Findings);
        Assert.Equal(PolicyDecision.RecommendSkip, policyEval.Decision);
    }

    [Fact]
    public async Task Test_CleanInspirationalSong_Under_IslamicEthos_Strictness10()
    {
        using var httpClient = new HttpClient();
        var client = new HttpGeminiModelClient(httpClient, GEMINI_API_KEY, "gemini-2.5-flash");
        var engine = new GeminiStanceAnalysisEngine(client, ProviderConsent.Granted("gemini-2.5-flash"), new BudgetLedger(100));

        // Clean, wholesome, motivational lyrics
        string lyrics = """
            Wake up to the morning light
            Every day is a gift of life
            Patience in the heart, peace in the mind
            Treat every soul with kindness you find
            Work with gratitude, walk in humility
            Keep your family safe and near
            Thank the Creator for the atmosphere
            Good deeds will always shine so clear
            """;

        var source = CreateSource("Clean Inspirational Song", lyrics);

        var result = await engine.AnalyzeAsync(
            source,
            targetLanguage: "en",
            profile: FilterProfile.Islamic,
            strictnessLevel: 10
        );

        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"Findings count: {result.Findings.Count}");

        var userPrefs = new UserFilterPreferences
        {
            Profile = FilterProfile.Islamic,
            StrictnessLevel = 10,
            FilterSpotifyExplicit = true
        };

        var policyEval = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(result, userPrefs.ToPolicyPreferences());
        _output.WriteLine($"Policy Decision: {policyEval.Decision}");

        Assert.Equal(AnalysisStatus.Completed, result.Status);
        Assert.Equal(PolicyDecision.Allow, policyEval.Decision);
    }
}
