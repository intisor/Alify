using Alify.Core.Models;
using Alify.Features.Spotify.Events;
using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;

namespace Alify.Features.Spotify.Services;

/// <summary>
/// A client-side sleep timer that pauses Spotify playback after a set duration.
/// Addresses the recurring Spotify Community complaints about broken/missing sleep timers.
/// </summary>
public class SleepTimerService
{
    private readonly IMemoryCache _cache;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISpotifySubject _spotifySubject;
    private readonly ILogger<SleepTimerService> _logger;

    private CancellationTokenSource? _timerCts;
    private Task? _timerTask;
    private DateTime? _expiresAt;
    private string? _mode;

    private const string AUTH_TOKEN_KEY = "SpotifyAuthToken";

    public SleepTimerService(
        IMemoryCache cache,
        IServiceProvider serviceProvider,
        ISpotifySubject spotifySubject,
        ILogger<SleepTimerService> logger)
    {
        _cache = cache;
        _serviceProvider = serviceProvider;
        _spotifySubject = spotifySubject;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current sleep timer status.
    /// </summary>
    public SleepTimerInfo GetStatus()
    {
        if (_expiresAt == null || _timerCts == null || _timerCts.IsCancellationRequested)
        {
            return new SleepTimerInfo { IsActive = false };
        }

        var remaining = _expiresAt.Value - DateTime.UtcNow;
        return new SleepTimerInfo
        {
            IsActive = remaining.TotalSeconds > 0,
            RemainingSeconds = Math.Max(0, (int)remaining.TotalSeconds),
            ExpiresAt = _expiresAt,
            Mode = _mode
        };
    }

    /// <summary>
    /// Starts a sleep timer for the specified duration in minutes.
    /// </summary>
    /// <param name="durationMinutes">Duration in minutes (e.g., 15, 30, 45, 60).</param>
    public void StartTimer(int durationMinutes)
    {
        CancelTimer();

        _mode = "duration";
        _expiresAt = DateTime.UtcNow.AddMinutes(durationMinutes);
        _timerCts = new CancellationTokenSource();

        _timerTask = RunTimerAsync(TimeSpan.FromMinutes(durationMinutes), _timerCts.Token);
        _logger.LogInformation("Sleep timer started for {Duration} minutes. Will pause playback at {ExpiresAt:HH:mm:ss UTC}.",
            durationMinutes, _expiresAt);
    }

    /// <summary>
    /// Cancels the active sleep timer.
    /// </summary>
    public void CancelTimer()
    {
        if (_timerCts != null)
        {
            _timerCts.Cancel();
            _timerCts.Dispose();
            _timerCts = null;
        }
        _expiresAt = null;
        _mode = null;
        _timerTask = null;
        _logger.LogInformation("Sleep timer cancelled.");
    }

    private async Task RunTimerAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            // Timer expired — pause playback
            await PausePlaybackAsync();
            _logger.LogInformation("Sleep timer expired. Playback paused.");

            // Notify via SSE
            await _spotifySubject.NotifyPlaybackInfoAsync(new SpotifyPlaybackInfo
            {
                SleepTimer = new SleepTimerInfo
                {
                    IsActive = false,
                    RemainingSeconds = 0,
                    Mode = "expired"
                }
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Sleep timer was cancelled before expiring.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sleep timer execution.");
        }
        finally
        {
            _expiresAt = null;
            _mode = null;
        }
    }

    private async Task PausePlaybackAsync()
    {
        try
        {
            if (!_cache.TryGetValue(AUTH_TOKEN_KEY, out string? token) || string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Cannot pause playback: no auth token available.");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var spotifyService = scope.ServiceProvider.GetRequiredService<SpotifyService>();
            var client = await spotifyService.GetSpotifyClientAsync(token);

            if (client != null)
            {
                await client.Player.PausePlayback();
                _logger.LogInformation("Playback paused by sleep timer.");
            }
        }
        catch (APIException ex)
        {
            _logger.LogError(ex, "Spotify API error while pausing playback from sleep timer.");
        }
    }
}
