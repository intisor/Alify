using Alify.Core.Features.Analysis.Models;
using Alify.Core.Features.Analysis.Policy;
using Alify.Core.Models;
using Alify.Core.Services;
using Alify.Features.Spotify.Services;
using Alify.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Alify.Spotify.Tests;

public class UserFilterPreferencesTests
{
    [Fact]
    public void DefaultPreferences_ReflectStandardSafetyBaselines()
    {
        var prefs = new UserFilterPreferences();

        Assert.True(prefs.FilterSpotifyExplicit);
        Assert.True(prefs.FilterViolenceEndorsed);
        Assert.False(prefs.FilterViolenceMentioned);
        Assert.True(prefs.FilterSubstanceEndorsed);
        Assert.False(prefs.FilterSubstanceMentioned);

        var policy = prefs.ToPolicyPreferences();
        Assert.True(policy.ExplicitFilterOptIn);
        Assert.Contains(Stance.Endorsed, policy.TriggerStancesByTopic["Violence"]);
        Assert.DoesNotContain(Stance.Mentioned, policy.TriggerStancesByTopic["Violence"]);
        Assert.Contains(Stance.Endorsed, policy.TriggerStancesByTopic["SubstanceUse"]);
        Assert.DoesNotContain(Stance.Mentioned, policy.TriggerStancesByTopic["SubstanceUse"]);
    }

    [Fact]
    public void ToPolicyPreferences_WhenAllDisabled_DisablesAllTriggers()
    {
        var prefs = new UserFilterPreferences
        {
            FilterSpotifyExplicit = false,
            FilterViolenceEndorsed = false,
            FilterViolenceMentioned = false,
            FilterSubstanceEndorsed = false,
            FilterSubstanceMentioned = false
        };

        var policy = prefs.ToPolicyPreferences();
        Assert.False(policy.ExplicitFilterOptIn);
        Assert.False(policy.TriggerStancesByTopic.ContainsKey("Violence"));
        Assert.False(policy.TriggerStancesByTopic.ContainsKey("SubstanceUse"));
    }

    [Fact]
    public void EvaluateSpotifyExplicit_RespectsUserExplicitFilterToggle()
    {
        var enabledPrefs = new UserFilterPreferences { FilterSpotifyExplicit = true }.ToPolicyPreferences();
        var disabledPrefs = new UserFilterPreferences { FilterSpotifyExplicit = false }.ToPolicyPreferences();

        var decisionWhenEnabled = DeterministicPolicyEvaluator.EvaluateSpotifyExplicit(true, enabledPrefs);
        var decisionWhenDisabled = DeterministicPolicyEvaluator.EvaluateSpotifyExplicit(true, disabledPrefs);

        Assert.Equal(PolicyDecision.RecommendSkip, decisionWhenEnabled.Decision);
        Assert.Equal(PolicyDecision.Abstain, decisionWhenDisabled.Decision);
    }

    [Fact]
    public void EvaluateStandaloneLyrics_RespectsViolenceMentionedToggle()
    {
        var mentionOnlyAnalysis = new AnalysisResult
        {
            AnalysisId = "eval-1",
            SourceId = "track-violence-story",
            Status = AnalysisStatus.Completed,
            EngineKind = "Heuristic",
            Findings =
            [
                new Finding
                {
                    FindingId = "f-1",
                    Topic = "Violence",
                    Stance = Stance.Mentioned,
                    Expression = ExpressionType.Literal,
                    Rationale = "He told the news report of the historical war"
                }
            ]
        };

        // Default: Mentioned is not filtered
        var defaultPrefs = new UserFilterPreferences { FilterViolenceMentioned = false }.ToPolicyPreferences();
        var defaultDecision = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(mentionOnlyAnalysis, defaultPrefs);
        Assert.Equal(PolicyDecision.Allow, defaultDecision.Decision);

        // Strict mode: Mentioned is filtered
        var strictPrefs = new UserFilterPreferences { FilterViolenceMentioned = true }.ToPolicyPreferences();
        var strictDecision = DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(mentionOnlyAnalysis, strictPrefs);
        Assert.Equal(PolicyDecision.RecommendSkip, strictDecision.Decision);
    }

    [Fact]
    public void EvaluateStandaloneLyrics_RespectsSubstanceEndorsedToggle()
    {
        var drugPromotionAnalysis = new AnalysisResult
        {
            AnalysisId = "eval-2",
            SourceId = "track-pills-promo",
            Status = AnalysisStatus.Completed,
            EngineKind = "Heuristic",
            Findings =
            [
                new Finding
                {
                    FindingId = "f-2",
                    Topic = "SubstanceUse",
                    Stance = Stance.Endorsed,
                    Expression = ExpressionType.Literal,
                    Rationale = "Take more pills pop till you drop"
                }
            ]
        };

        var skipPrefs = new UserFilterPreferences { FilterSubstanceEndorsed = true }.ToPolicyPreferences();
        var allowPrefs = new UserFilterPreferences { FilterSubstanceEndorsed = false }.ToPolicyPreferences();

        Assert.Equal(PolicyDecision.RecommendSkip, DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(drugPromotionAnalysis, skipPrefs).Decision);
        Assert.Equal(PolicyDecision.Allow, DeterministicPolicyEvaluator.EvaluateStandaloneLyrics(drugPromotionAnalysis, allowPrefs).Decision);
    }

    [Fact]
    public void QueueService_SaveAndGetUserFilterPreferences_PersistsAndInvalidatesQueue()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var lyricsProvider = new FreeLyricsProviderService(new HttpClient(), NullLogger<FreeLyricsProviderService>.Instance);
        var queueService = new QueueService(
            cache,
            new SpotifyRequestCache(cache, NullLogger<SpotifyRequestCache>.Instance, lyricsProvider),
            new ArtistLyricService(),
            NullLogger<QueueService>.Instance,
            lyricsProvider);

        const string userId = "test-user-123";

        // Initial default
        var initialPrefs = queueService.GetUserFilterPreferences(userId);
        Assert.True(initialPrefs.FilterSpotifyExplicit);
        Assert.False(initialPrefs.FilterViolenceMentioned);

        // Populate a queue in cache
        cache.Set($"queue_{userId}", new MusicQueue(), TimeSpan.FromMinutes(1));
        Assert.True(cache.TryGetValue($"queue_{userId}", out _));

        // Update preferences
        var newPrefs = new UserFilterPreferences
        {
            FilterSpotifyExplicit = false,
            FilterViolenceMentioned = true,
            FilterSubstanceMentioned = true
        };
        queueService.SaveUserFilterPreferences(userId, newPrefs);

        // Verify preferences persisted
        var savedPrefs = queueService.GetUserFilterPreferences(userId);
        Assert.False(savedPrefs.FilterSpotifyExplicit);
        Assert.True(savedPrefs.FilterViolenceMentioned);
        Assert.True(savedPrefs.FilterSubstanceMentioned);

        // Verify queue cache was invalidated on preference update
        Assert.False(cache.TryGetValue($"queue_{userId}", out _));
    }

    [Fact]
    public void IslamicProfile_EnforcesHalalGuardrails_AndStrictnessScale()
    {
        // Standard strictness (5)
        var islamicStandard = new UserFilterPreferences
        {
            Profile = FilterProfile.Islamic,
            StrictnessLevel = 5
        }.ToPolicyPreferences();

        Assert.True(islamicStandard.ExplicitFilterOptIn);
        Assert.Contains(Stance.Endorsed, islamicStandard.TriggerStancesByTopic["Blasphemy"]);
        Assert.DoesNotContain(Stance.Mentioned, islamicStandard.TriggerStancesByTopic["Blasphemy"]);
        Assert.Contains(Stance.Endorsed, islamicStandard.TriggerStancesByTopic["SubstanceUse"]);
        Assert.Contains(Stance.Endorsed, islamicStandard.TriggerStancesByTopic["SexualContent"]);

        // Strict mode (level 8)
        var islamicStrict = new UserFilterPreferences
        {
            Profile = FilterProfile.Islamic,
            StrictnessLevel = 8
        }.ToPolicyPreferences();

        Assert.Contains(Stance.Mentioned, islamicStrict.TriggerStancesByTopic["Blasphemy"]);
        Assert.Contains(Stance.Mentioned, islamicStrict.TriggerStancesByTopic["SubstanceUse"]);
        Assert.Contains(Stance.Mentioned, islamicStrict.TriggerStancesByTopic["SexualContent"]);
    }

    [Fact]
    public void ChristianProfile_EnforcesBiblicalGuardrails_AndStrictnessScale()
    {
        var christianStandard = new UserFilterPreferences
        {
            Profile = FilterProfile.Christian,
            StrictnessLevel = 4
        }.ToPolicyPreferences();

        Assert.True(christianStandard.ExplicitFilterOptIn);
        Assert.Contains(Stance.Endorsed, christianStandard.TriggerStancesByTopic["Blasphemy"]);
        Assert.Contains(Stance.Endorsed, christianStandard.TriggerStancesByTopic["Occult"]);
        Assert.DoesNotContain(Stance.Mentioned, christianStandard.TriggerStancesByTopic["Occult"]);

        // High strictness (level 9)
        var christianStrict = new UserFilterPreferences
        {
            Profile = FilterProfile.Christian,
            StrictnessLevel = 9
        }.ToPolicyPreferences();

        Assert.Contains(Stance.Mentioned, christianStrict.TriggerStancesByTopic["Blasphemy"]);
        Assert.Contains(Stance.Mentioned, christianStrict.TriggerStancesByTopic["Occult"]);
    }

    [Fact]
    public void FamilyFriendlyProfile_FiltersAllAdultThemes()
    {
        var familyPrefs = new UserFilterPreferences
        {
            Profile = FilterProfile.FamilyFriendly,
            StrictnessLevel = 5
        }.ToPolicyPreferences();

        Assert.True(familyPrefs.ExplicitFilterOptIn);
        Assert.Contains(Stance.Endorsed, familyPrefs.TriggerStancesByTopic["Violence"]);
        Assert.Contains(Stance.Endorsed, familyPrefs.TriggerStancesByTopic["SubstanceUse"]);
        Assert.Contains(Stance.Endorsed, familyPrefs.TriggerStancesByTopic["Vulgarity"]);
        Assert.Contains(Stance.Endorsed, familyPrefs.TriggerStancesByTopic["SexualContent"]);
    }
}
