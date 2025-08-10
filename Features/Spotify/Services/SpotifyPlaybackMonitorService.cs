using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Alify.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Alify.Features.Spotify.Events;
using Alify.Features.Spotify.Services;

namespace Alify.Services;

/// <summary>
/// A comprehensive service that monitors Spotify playback in the background,
/// automatically skips flagged content, and broadcasts real-time updates via SSE.
/// Enhanced with modern .NET approaches and comprehensive edge case handling.
/// </summary>
/// <param name="serviceProvider">Service provider for scoped service creation</param>
/// <param name="logger">Logger instance for this service</param>
/// <param name="cache">Memory cache for storing auth tokens and state</param>
/// <param name="spotifySubject">SSE notification subject</param>
/// <param name="queueService">Service for managing track queue and flagged content</param>
/// <param name="options">Configuration options for monitoring behavior</param>
[DebuggerDisplay("IsMonitoring: {_monitoringState.IsMonitoring}, IsRunning: {_monitoringState.IsRunning}, AuthWarningCooldown: {_authManager.IsInCooldown}")]
public sealed class SpotifyPlaybackMonitorService(
    IServiceProvider serviceProvider,
    ILogger<SpotifyPlaybackMonitorService> logger,
    IMemoryCache cache,
    ISpotifySubject spotifySubject,
    QueueService queueService,
    IOptions<PlaybackMonitorOptions>? options = null) : BackgroundService
{
    private readonly PlaybackMonitorOptions _options = options?.Value ?? new PlaybackMonitorOptions();

    // Modern record types for better state management
    private readonly MonitoringState _monitoringState = new();
    private readonly AuthenticationManager _authManager = new(logger);
    private readonly TrackingState _trackingState = new();
    private readonly EdgeCaseState _edgeCaseState = new();

    // Cache keys as constants (modern approach)
    private const string AUTH_TOKEN_KEY = "SpotifyAuthToken";
    private const string MONITORING_STATUS_KEY = "MonitoringStatus";

    /// <summary>
    /// Gets a value indicating whether monitoring is currently enabled.
    /// </summary>
    public bool IsMonitoring => _monitoringState.IsMonitoring;

    /// <summary>
    /// Gets a value indicating whether the background service is running.
    /// </summary>
    public bool IsRunning => _monitoringState.IsRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _monitoringState.IsRunning = true;
        logger.LogInformation("Modern Spotify Playback Monitor Service started");

        await foreach (var delayMs in RunMonitoringLoopAsync(stoppingToken))
        {
            try
            {
                await Task.Delay(delayMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Modern Spotify Playback Monitor Service is stopping");
                break;
            }
        }

        _monitoringState.IsRunning = false;
        logger.LogInformation("Modern Spotify Playback Monitor Service stopped");
    }

    /// <summary>
    /// Modern async enumerable approach for the monitoring loop
    /// </summary>
    private async IAsyncEnumerable<int> RunMonitoringLoopAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            int delayMs;
            try
            {
                var nextDelayMs = await MonitorPlaybackAsync(cancellationToken);
                delayMs = CalculateNextDelay(nextDelayMs);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in Modern Spotify Playback Monitor Service");
                _trackingState.IncrementErrorCount();
                delayMs = CalculateErrorBackoffDelay();
            }

            yield return delayMs;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Modern Spotify Playback Monitor Service is stopping");
        _monitoringState.IsRunning = false;
        await base.StopAsync(cancellationToken);
    }

    #region Public Control Methods

    /// <summary>
    /// Starts monitoring Spotify playback for content filtering and real-time updates.
    /// </summary>
    public Task StartMonitoringAsync()
    {
        _monitoringState.IsMonitoring = true;
        cache.Set(MONITORING_STATUS_KEY, "Running", TimeSpan.FromHours(1));
        logger.LogInformation("Modern Spotify playback monitoring enabled");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops monitoring Spotify playback.
    /// </summary>
    public Task StopMonitoringAsync()
    {
        _monitoringState.IsMonitoring = false;
        cache.Set(MONITORING_STATUS_KEY, "Stopped", TimeSpan.FromHours(1));
        logger.LogInformation("Modern Spotify playback monitoring disabled");
        return Task.CompletedTask;
    }

    #endregion

    #region Core Monitoring Logic

    private async Task<int?> MonitorPlaybackAsync(CancellationToken cancellationToken)
    {
        // Modern pattern matching for post-skip state management
        if (_edgeCaseState is { JustSkippedTrack: true } &&
            DateTime.UtcNow - _edgeCaseState.LastSkipTime > TimeSpan.FromMilliseconds(_options.PostSkipWindowMs))
        {
            _edgeCaseState.ExitPostSkipMode();
            logger.LogDebug("Exited post-skip monitoring mode");
        }

        // Early return pattern for inactive monitoring
        if (!_monitoringState.IsMonitoring)
        {
            return _options.InactivePollingIntervalMs;
        }

        // Modern null-conditional and pattern matching for auth check
        if (!cache.TryGetValue(AUTH_TOKEN_KEY, out string? spotifyToken) || string.IsNullOrEmpty(spotifyToken))
        {
            _authManager.LogWarningIfNeeded("Spotify authentication token not found. Authentication may be required.");
            return _options.NoAuthPollingIntervalMs;
        }

        // Modern using declaration
        using var scope = serviceProvider.CreateScope();
        var spotifyService = scope.ServiceProvider.GetRequiredService<SpotifyService>();

        var spotify = await spotifyService.GetSpotifyClientAsync(spotifyToken);
        if (spotify is null)
        {
            _authManager.LogWarningIfNeeded("Spotify client not available. Authentication may be required.");
            return _options.NoAuthPollingIntervalMs;
        }

        try
        {
            // Reset state on successful API call
            _trackingState.ResetErrorCount();
            _trackingState.UpdateLastSuccessfulPoll();

            // Get current playback info
            var playbackInfo = await spotifyService.GetCurrentPlaybackInfoAsync(spotify);
            var (trackChanged, playbackStateChanged) = ProcessPlaybackInfo(playbackInfo);

            // Modern tuple deconstruction and conditional SSE updates
            if (ShouldSendSSEUpdate(playbackInfo, trackChanged, playbackStateChanged))
            {
                if (playbackInfo != null) await spotifySubject.NotifyPlaybackInfoAsync(playbackInfo);
                _trackingState.UpdateLastPlaybackInfoUpdate();
                logger.LogDebug("Sent enhanced playback info SSE update (trackChanged: {TrackChanged}, stateChanged: {StateChanged})",
                    trackChanged, playbackStateChanged);
            }

            // Content filtering with modern pattern matching
            var skipResult = await ProcessContentFiltering(spotify, trackChanged);
            if (skipResult.HasValue)
            {
                return skipResult.Value;
            }

            // Enhanced intelligent delay calculation
            return playbackInfo?.CurrentlyPlaying is not null && playbackInfo.RemainingTimeMs > 0
                ? CalculateIntelligentDelay(playbackInfo)
                : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error monitoring Spotify playback");
            throw; // Let the main loop handle this exception with backoff
        }
    }

    /// <summary>
    /// Modern tuple return approach for tracking state changes
    /// </summary>
    private (bool TrackChanged, bool PlaybackStateChanged) ProcessPlaybackInfo(SpotifyPlaybackInfo? playbackInfo)
    {
        if (playbackInfo?.CurrentlyPlaying is null)
        {
            bool wasPlaying = _trackingState.WasPlayingLastCheck;
            _trackingState.UpdatePlaybackState(false, false);
            _edgeCaseState.ResetTrackState();

            if (wasPlaying)
            {
                logger.LogDebug("Playback stopped detected");
            }

            return (false, wasPlaying);
        }

        string currentTrackId = playbackInfo.CurrentlyPlaying.FullTrack.Id;
        bool trackChanged = currentTrackId != _trackingState.LastTrackId;

        // User skip detection with modern null-conditional operators
        if (trackChanged)
        {
            var previousRemaining = _edgeCaseState.PreviousTrackRemainingMs;
            if (previousRemaining > _options.SkipDetectionRemainingTimeThresholdMs)
            {
                logger.LogInformation("Detected possible manual skip (remaining ms: {Remaining})", previousRemaining);
            }

            _trackingState.UpdateTrackId(currentTrackId);
            _edgeCaseState.OnTrackChanged(playbackInfo.RemainingTimeMs ?? 0);
            logger.LogDebug("Track changed detected: {TrackName}", playbackInfo.CurrentlyPlaying.FullTrack.Name);
        }
        else if (playbackInfo.RemainingTimeMs is int remainingMs)
        {
            _edgeCaseState.UpdateRemainingTime(remainingMs);
        }

        // Modern pattern matching for playback state detection
        bool isCurrentlyPlaying = playbackInfo.RemainingTimeMs is > 0 && playbackInfo.CurrentlyPlaying is not null;
        bool playbackStateChanged = isCurrentlyPlaying != _trackingState.WasPlayingLastCheck;

        _trackingState.UpdatePlaybackState(isCurrentlyPlaying, playbackStateChanged);
        _edgeCaseState.UpdatePlaybackState(isCurrentlyPlaying, playbackStateChanged);

        return (trackChanged, playbackStateChanged);
    }

    /// <summary>
    /// Modern approach with explicit conditions for SSE updates
    /// </summary>
    private bool ShouldSendSSEUpdate(SpotifyPlaybackInfo? playbackInfo, bool trackChanged, bool playbackStateChanged) =>
        playbackInfo is not null &&
        (trackChanged ||
         playbackStateChanged ||
         DateTime.UtcNow - _trackingState.LastPlaybackInfoUpdate > TimeSpan.FromMilliseconds(_options.MinimumSSEUpdateIntervalMs));

    /// <summary>
    /// Modern async approach with optional return for content filtering
    /// </summary>
    private async Task<int?> ProcessContentFiltering(dynamic spotify, bool trackChanged)
    {
        var currentUser = await spotify.UserProfile.Current();
        string? userId = currentUser?.Id;

        return userId switch
        {
            not null => await ProcessFlaggedContent(userId, spotify),
            null when trackChanged => LogMissingUserId(),
            _ => null
        };
    }

    private async Task<int?> ProcessFlaggedContent(string userId, dynamic spotify)
    {
        var skippedTrack = await queueService.SkipFlaggedSongsAsync(userId, spotify);

        if (skippedTrack?.IsFlagged is true)
        {
            _edgeCaseState.OnTrackSkipped();

            await spotifySubject.NotifyTrackSkippedEventAsync(skippedTrack);
            logger.LogInformation("Track skipped and observers notified: {TrackName} by {Artist}",
                (object)skippedTrack.FullTrack.Name,
                (object)(skippedTrack.FullTrack.Artists.FirstOrDefault()?.Name ?? "Unknown"));

            return _options.PostSkipCheckDelayMs;
        }

        return null;
    }

    private int? LogMissingUserId()
    {
        logger.LogWarning("Spotify userId not found but track changed. Cannot check queue for flagged songs.");
        return null;
    }

    #endregion

    #region Enhanced Timing Calculation Methods

    /// <summary>
    /// Modern pattern matching approach for intelligent delay calculation
    /// </summary>
    private int? CalculateIntelligentDelay(SpotifyPlaybackInfo playbackInfo)
    {
        var remainingTimeMs = playbackInfo.RemainingTimeMs ?? 0;
        bool isPlaying = remainingTimeMs > 0 && playbackInfo.CurrentlyPlaying is not null;

        // Modern switch expressions for edge case handling
        return remainingTimeMs switch
        {
            var time when time < _options.ShortRemainingThresholdMs => HandleShortRemainingTime(time),
            _ when !isPlaying => HandlePausedPlayback(),
            _ when IsRecentTrackChange() => HandleRecentTrackChange(remainingTimeMs),
            _ => CalculateStandardIntelligentTiming(remainingTimeMs)
        };
    }

    private int HandleShortRemainingTime(int remainingTimeMs)
    {
        _edgeCaseState.IncrementShortRemainingCount();

        if (_edgeCaseState.ConsecutiveShortRemainingCount >= _options.MaxConsecutiveShortRemainingAttempts)
        {
            logger.LogDebug("Consecutive short remaining time detected ({Count} times), using frequent polling",
                _edgeCaseState.ConsecutiveShortRemainingCount);
            return _options.ShortRemainingTrackPollingMs;
        }

        var shortDelay = Math.Min(remainingTimeMs + _options.TrackTransitionBufferMs, _options.ShortRemainingTrackPollingMs);
        logger.LogDebug("Short remaining time ({RemainingMs}ms), using short delay: {DelayMs}ms",
            remainingTimeMs, shortDelay);
        return shortDelay;
    }

    private int HandlePausedPlayback()
    {
        _edgeCaseState.IncrementPausedAttempts();

        var pausedDelay = Math.Min(
            _options.PausedPlaybackPollingIntervalMs * Math.Min(_edgeCaseState.PausedPollingAttempts, _options.MaxPausedBackoffMultiplier),
            _options.MaximumPollingIntervalMs);

        logger.LogDebug("Paused playback detected (attempt {Attempts}), using paused interval: {DelayMs}ms",
            _edgeCaseState.PausedPollingAttempts, pausedDelay);
        return (int)pausedDelay;
    }

    private bool IsRecentTrackChange()
    {
        var timeSinceLastTrackChange = DateTime.UtcNow - _edgeCaseState.LastTrackChangeDetection;
        return timeSinceLastTrackChange.TotalMilliseconds < _options.RecentTrackChangeWindowMs;
    }

    private int HandleRecentTrackChange(int remainingTimeMs)
    {
        var timeSinceChange = DateTime.UtcNow - _edgeCaseState.LastTrackChangeDetection;
        var recentChangeDelay = Math.Min(_options.RecentTrackChangePollingMs, remainingTimeMs / 2);

        logger.LogDebug("Recent track change detected ({TimeSinceMs}ms ago), using frequent polling: {DelayMs}ms",
            timeSinceChange.TotalMilliseconds, recentChangeDelay);
        return recentChangeDelay;
    }

    private int CalculateStandardIntelligentTiming(int remainingTimeMs)
    {
        _edgeCaseState.ResetShortRemainingCount();
        _edgeCaseState.ResetPausedAttempts();

        // Modern range pattern for buffer calculation
        int bufferMs = Math.Max(
            (int)(remainingTimeMs * _options.TrackEndBufferPercentage),
            _options.MinimumTrackEndBufferMs);

        var nextCheckMs = Math.Max(
            remainingTimeMs - bufferMs,
            _options.MinimumPollingIntervalMs);

        nextCheckMs = Math.Min(nextCheckMs, _options.MaximumPollingIntervalMs);

        // Safety check with modern time arithmetic
        var maxAllowableDelay = (int)Math.Max(
            _options.MaxTimeBetweenSuccessfulPollsMs - (DateTime.UtcNow - _trackingState.LastSuccessfulPoll).TotalMilliseconds,
            _options.MinimumPollingIntervalMs);

        nextCheckMs = Math.Min(nextCheckMs, maxAllowableDelay);

        logger.LogDebug("Standard intelligent timing: remaining={RemainingMs}ms, buffer={BufferMs}ms, nextCheck={NextCheckMs}ms",
            remainingTimeMs, bufferMs, nextCheckMs);
        return nextCheckMs;
    }

    private int CalculateNextDelay(int? suggestedDelay) => suggestedDelay switch
    {
        not null when _edgeCaseState.JustSkippedTrack => _options.PostSkipCheckDelayMs,
        not null => suggestedDelay.Value,
        null when _edgeCaseState.WasPausedLastCheck => CalculatePausedDelay(),
        null => _trackingState.WasPlayingLastCheck ? _options.ActivePlaybackPollingIntervalMs : _options.PausedPlaybackPollingIntervalMs
    };

    private int CalculatePausedDelay()
    {
        var pausedDelay = _options.PausedPlaybackPollingIntervalMs * Math.Min(_edgeCaseState.PausedPollingAttempts + 1, _options.MaxPausedBackoffMultiplier);
        return Math.Min((int)pausedDelay, _options.MaximumPollingIntervalMs);
    }

    private int CalculateErrorBackoffDelay()
    {
        int backoffFactor = Math.Min(_trackingState.ConsecutiveErrorCount, 6);
        int backoffDelay = _options.BaseErrorBackoffIntervalMs * (1 << backoffFactor);
        return Math.Min(backoffDelay, _options.MaximumPollingIntervalMs);
    }

    #endregion
}

// Modern record types for better state management
internal sealed record MonitoringState
{
    public bool IsMonitoring { get; set; }
    public bool IsRunning { get; set; }
}
internal sealed record AuthenticationManager(ILogger Logger)
{
    private DateTime _lastAuthWarning = DateTime.MinValue;
    private readonly TimeSpan _authWarningCooldown = TimeSpan.FromMinutes(5);

    public bool IsInCooldown => DateTime.UtcNow - _lastAuthWarning <= _authWarningCooldown;

    public void LogWarningIfNeeded(string message)
    {
        if (!IsInCooldown)
        {
            Logger.LogWarning(message);
            _lastAuthWarning = DateTime.UtcNow;
        }
    }
}
internal sealed record TrackingState
{
    public int ConsecutiveErrorCount { get; private set; }
    public bool WasPlayingLastCheck { get; private set; }
    public DateTime LastPlaybackInfoUpdate { get; private set; } = DateTime.MinValue;
    public string LastTrackId { get; private set; } = string.Empty;
    public DateTime LastSuccessfulPoll { get; private set; } = DateTime.MinValue;

    public void IncrementErrorCount() => ConsecutiveErrorCount++;
    public void ResetErrorCount() => ConsecutiveErrorCount = 0;
    public void UpdateLastSuccessfulPoll() => LastSuccessfulPoll = DateTime.UtcNow;
    public void UpdateLastPlaybackInfoUpdate() => LastPlaybackInfoUpdate = DateTime.UtcNow;
    public void UpdateTrackId(string trackId) => LastTrackId = trackId;

    public void UpdatePlaybackState(bool isPlaying, bool stateChanged)
    {
        WasPlayingLastCheck = isPlaying;
        if (stateChanged)
        {
            if (isPlaying)
            {
                // Logger could be injected here if needed for state change logging
            }
        }
    }
}
internal sealed record EdgeCaseState
{
    public int ConsecutiveShortRemainingCount { get; private set; }
    public bool WasPausedLastCheck { get; private set; }
    public DateTime LastTrackChangeDetection { get; private set; } = DateTime.MinValue;
    public int PausedPollingAttempts { get; private set; }
    public int PreviousTrackRemainingMs { get; private set; }
    public bool JustSkippedTrack { get; private set; }
    public DateTime LastSkipTime { get; private set; } = DateTime.MinValue;

    public void IncrementShortRemainingCount() => ConsecutiveShortRemainingCount++;
    public void ResetShortRemainingCount() => ConsecutiveShortRemainingCount = 0;
    public void IncrementPausedAttempts() => PausedPollingAttempts++;
    public void ResetPausedAttempts() => PausedPollingAttempts = 0;
    public void UpdateRemainingTime(int remainingMs) => PreviousTrackRemainingMs = remainingMs;

    public void OnTrackChanged(int remainingMs)
    {
        LastTrackChangeDetection = DateTime.UtcNow;
        PreviousTrackRemainingMs = remainingMs;
        ResetShortRemainingCount();
        ResetPausedAttempts();
    }

    public void OnTrackSkipped()
    {
        JustSkippedTrack = true;
        LastSkipTime = DateTime.UtcNow;
    }

    public void ExitPostSkipMode() => JustSkippedTrack = false;

    public void UpdatePlaybackState(bool isPlaying, bool stateChanged)
    {
        if (stateChanged)
        {
            if (!isPlaying)
            {
                WasPausedLastCheck = true;
                ResetPausedAttempts();
            }
            else
            {
                WasPausedLastCheck = false;
                ResetPausedAttempts();
            }
        }
        else if (isPlaying)
        {
            ResetPausedAttempts();
        }
    }

    public void ResetTrackState()
    {
        WasPausedLastCheck = false;
        ResetPausedAttempts();
    }
}

/// <summary>
/// Enhanced configuration options with modern validation attributes
/// </summary>
public sealed record PlaybackMonitorOptions
{
    public int ActivePlaybackPollingIntervalMs { get; set; } = 5000;
    public int PausedPlaybackPollingIntervalMs { get; set; } = 15000;
    public int InactivePollingIntervalMs { get; set; } = 30000;
    public int NoAuthPollingIntervalMs { get; set; } = 10000;
    public int MinimumPollingIntervalMs { get; set; } = 1000;
    public int MaximumPollingIntervalMs { get; set; } = 120000;
    public int MinimumSSEUpdateIntervalMs { get; set; } = 10000;
    public int BaseErrorBackoffIntervalMs { get; set; } = 1000;
    public int PostSkipCheckDelayMs { get; set; } = 800;
    public int PostSkipWindowMs { get; set; } = 5000;
    public double TrackEndBufferPercentage { get; set; } = 0.05;
    public int MinimumTrackEndBufferMs { get; set; } = 2000;
    public int ShortRemainingThresholdMs { get; set; } = 3000;
    public int ShortRemainingTrackPollingMs { get; set; } = 2000;
    public int MaxConsecutiveShortRemainingAttempts { get; set; } = 3;
    public int TrackTransitionBufferMs { get; set; } = 500;
    public int MaxPausedBackoffMultiplier { get; set; } = 4;
    public int RecentTrackChangeWindowMs { get; set; } = 10000;
    public int RecentTrackChangePollingMs { get; set; } = 3000;
    public int MaxTimeBetweenSuccessfulPollsMs { get; set; } = 30000;
    public int SkipDetectionRemainingTimeThresholdMs { get; set; } = 2000;
}