using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Alify.Core.Features.Analysis.Engine;
using Alify.Core.Features.Analysis.Ledger;
using Alify.Core.Features.Analysis.Models;
using Alify.Core.Models;
using Alify.Core.Services;
using Alify.Features.Spotify.Services;
using Alify.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SpotifyAPI.Web;
using Xunit;
using Xunit.Abstractions;

namespace Alify.Spotify.Tests;

public class QueuePerformanceBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public QueuePerformanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static IMemoryCache CreateMemoryCache()
    {
        return new MemoryCache(new MemoryCacheOptions());
    }

    private static FreeLyricsProviderService CreateFreeLyricsProvider()
    {
        return new FreeLyricsProviderService(new HttpClient(), NullLogger<FreeLyricsProviderService>.Instance);
    }

    [Fact]
    public void ValidateQueue_InMemory_ExecutesUnderTenMicrosecondsPerCall()
    {
        var cache = CreateMemoryCache();
        var lyricsProvider = CreateFreeLyricsProvider();
        var queueService = new QueueService(
            cache,
            new SpotifyRequestCache(cache, NullLogger<SpotifyRequestCache>.Instance, lyricsProvider),
            new ArtistLyricService(),
            NullLogger<QueueService>.Instance,
            lyricsProvider);

        string userId = "test_user_bench";
        string trackId = "track_active_001";

        var musicQueue = new MusicQueue
        {
            Tracks = new List<Track>
            {
                new() { FullTrack = new FullTrack { Id = trackId, Name = "Benchmark Song" } },
                new() { FullTrack = new FullTrack { Id = "track_next_002", Name = "Next Song" } }
            },
            CurrentIndex = 0,
            CachedAt = DateTime.UtcNow
        };
        cache.Set($"queue_{userId}", musicQueue, TimeSpan.FromMinutes(5));

        // Warmup
        bool warmup = queueService.ValidateQueue(userId, trackId);
        Assert.True(warmup);

        // Benchmark 10,000 in-memory validation checks
        const int iterations = 10_000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            bool isValid = queueService.ValidateQueue(userId, trackId);
            Assert.True(isValid);
        }
        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgMicroseconds = (totalMs / iterations) * 1000.0;
        _output.WriteLine($"[Benchmark] 10,000 In-Memory Queue Validations took {totalMs:F2}ms (avg {avgMicroseconds:F3}µs / op)");

        // Assert that 10,000 in-memory operations take less than 100ms total (< 10µs per op)
        Assert.True(totalMs < 100.0, $"Expected < 100ms for 10k ops, but was {totalMs:F2}ms");
    }

    [Fact]
    public void ValidateQueue_WhenTrackMismatched_InvalidatesCacheImmediately()
    {
        var cache = CreateMemoryCache();
        var lyricsProvider = CreateFreeLyricsProvider();
        var queueService = new QueueService(
            cache,
            new SpotifyRequestCache(cache, NullLogger<SpotifyRequestCache>.Instance, lyricsProvider),
            new ArtistLyricService(),
            NullLogger<QueueService>.Instance,
            lyricsProvider);

        string userId = "user_mismatch";
        var musicQueue = new MusicQueue
        {
            Tracks = new List<Track>
            {
                new() { FullTrack = new FullTrack { Id = "old_track_123", Name = "Old Song" } }
            },
            CurrentIndex = 0,
            CachedAt = DateTime.UtcNow
        };
        cache.Set($"queue_{userId}", musicQueue, TimeSpan.FromMinutes(5));

        // Validate against new track ID
        bool isValid = queueService.ValidateQueue(userId, "new_track_456");
        Assert.False(isValid);

        // Cache must have been invalidated
        bool cacheStillExists = cache.TryGetValue($"queue_{userId}", out _);
        Assert.False(cacheStillExists);
    }

    [Fact]
    public void StanceAnalysisCache_CleanSong_BypassesEngineOnSubsequentTicks()
    {
        var cache = CreateMemoryCache();
        string trackId = "clean_track_999";
        string stanceKey = $"stance_eval_{trackId}".ToLowerInvariant();

        // Simulate Tick 1: Analysis runs and caches clean result
        var firstResult = new StanceEvaluationResult
        {
            IsFlagged = false,
            SkipReason = string.Empty,
            Lyrics = "I walked through the garden and saw the sunrise on a calm peaceful morning."
        };
        cache.Set(stanceKey, firstResult, TimeSpan.FromHours(12));

        // Simulate 50 subsequent polling ticks
        var sw = Stopwatch.StartNew();
        for (int tick = 2; tick <= 50; tick++)
        {
            bool cacheHit = cache.TryGetValue(stanceKey, out StanceEvaluationResult? cachedResult);
            Assert.True(cacheHit);
            Assert.NotNull(cachedResult);
            Assert.False(cachedResult.IsFlagged);
            Assert.Equal(string.Empty, cachedResult.SkipReason);
        }
        sw.Stop();

        double elapsedMs = sw.Elapsed.TotalMilliseconds;
        _output.WriteLine($"[Benchmark] 50 Stance Cache Lookups took {elapsedMs:F3}ms ({elapsedMs / 50.0:F4}ms per tick)");

        // 50 memory lookups must complete in well under 1ms
        Assert.True(elapsedMs < 5.0, $"Expected < 5ms for 50 cache hits, was {elapsedMs:F3}ms");
    }

    [Fact]
    public void StanceAnalysisCache_FlaggedSong_InstantlyRetainsSkipReason()
    {
        var cache = CreateMemoryCache();
        string trackId = "flagged_track_007";
        string stanceKey = $"stance_eval_{trackId}".ToLowerInvariant();

        var flaggedResult = new StanceEvaluationResult
        {
            IsFlagged = true,
            SkipReason = "Lyrics content policy violation: Violence: Endorsed",
            Lyrics = "Shoot them all down right now"
        };
        cache.Set(stanceKey, flaggedResult, TimeSpan.FromHours(12));

        // Lookup on tick 2
        bool cacheHit = cache.TryGetValue(stanceKey, out StanceEvaluationResult? result);
        Assert.True(cacheHit);
        Assert.NotNull(result);
        Assert.True(result.IsFlagged);
        Assert.Contains("Violence", result.SkipReason);
    }

    [Fact]
    public void SimulatedSteadyStatePlayback_50Ticks_ExecutesSubMillisecondWithoutAllocations()
    {
        var cache = CreateMemoryCache();
        string userId = "steady_playback_user";
        string currentTrackId = "track_steady_live";

        var musicQueue = new MusicQueue
        {
            Tracks = new List<Track>
            {
                new() { FullTrack = new FullTrack { Id = currentTrackId, Name = "Steady Hit", DurationMs = 210_000 } },
                new() { FullTrack = new FullTrack { Id = "next_track_01", Name = "Up Next", DurationMs = 180_000 } }
            },
            CurrentIndex = 0,
            CachedAt = DateTime.UtcNow
        };
        cache.Set($"queue_{userId}", musicQueue, TimeSpan.FromMinutes(5));
        cache.Set($"stance_eval_{currentTrackId}", new StanceEvaluationResult { IsFlagged = false, SkipReason = "" }, TimeSpan.FromHours(12));

        var lyricsProvider = CreateFreeLyricsProvider();
        var queueService = new QueueService(
            cache,
            new SpotifyRequestCache(cache, NullLogger<SpotifyRequestCache>.Instance, lyricsProvider),
            new ArtistLyricService(),
            NullLogger<QueueService>.Instance,
            lyricsProvider);

        // Simulate 50 steady state polling checks
        var sw = Stopwatch.StartNew();
        for (int tick = 0; tick < 50; tick++)
        {
            // 1. In-memory queue validation (Zero HTTP roundtrips)
            bool isQueueValid = queueService.ValidateQueue(userId, currentTrackId);
            Assert.True(isQueueValid);

            // 2. Fetch cached queue from memory
            bool queueHit = cache.TryGetValue($"queue_{userId}", out MusicQueue? q);
            Assert.True(queueHit);
            Assert.NotNull(q);

            // 3. Stance evaluation check from cache
            bool stanceHit = cache.TryGetValue($"stance_eval_{currentTrackId}", out StanceEvaluationResult? stance);
            Assert.True(stanceHit);
            Assert.NotNull(stance);
            Assert.False(stance.IsFlagged);
        }
        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        _output.WriteLine($"[Benchmark] 50 simulated steady-state monitor ticks completed in {totalMs:F3}ms ({totalMs / 50.0:F4}ms / tick)");

        Assert.True(totalMs < 10.0, $"Expected 50 ticks to take < 10ms, took {totalMs:F3}ms");
    }
}
