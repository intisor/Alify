using System.Collections.Concurrent;
using System.Threading;

namespace Alify.Features.Spotify.Services;

public class PlaybackDashboardStats
{
    private static int _totalAudited;
    private static int _totalFlagged;
    private static int _totalSkipped;
    private static int _totalNoLyrics;
    private static readonly ConcurrentDictionary<string, bool> _recordedTracks = new();

    public int TotalAudited => _totalAudited;
    public int TotalFlagged => _totalFlagged;
    public int TotalSkipped => _totalSkipped;
    public int TotalNoLyrics => _totalNoLyrics;

    public static void RecordAudit(string trackId, bool isFlagged, bool isSkipped, bool hasLyrics)
    {
        if (string.IsNullOrWhiteSpace(trackId)) return;

        // Ensure each track is recorded once per session/cycle
        if (_recordedTracks.TryAdd(trackId, true))
        {
            Interlocked.Increment(ref _totalAudited);
            if (isFlagged) Interlocked.Increment(ref _totalFlagged);
            if (isSkipped) Interlocked.Increment(ref _totalSkipped);
            if (!hasLyrics) Interlocked.Increment(ref _totalNoLyrics);
        }
        else if (isSkipped && !_recordedTracks.ContainsKey($"{trackId}_skipped"))
        {
            // Record skip if track was previously audited but now skipped
            _recordedTracks.TryAdd($"{trackId}_skipped", true);
            Interlocked.Increment(ref _totalSkipped);
        }
    }

    public static void Reset()
    {
        Interlocked.Exchange(ref _totalAudited, 0);
        Interlocked.Exchange(ref _totalFlagged, 0);
        Interlocked.Exchange(ref _totalSkipped, 0);
        Interlocked.Exchange(ref _totalNoLyrics, 0);
        _recordedTracks.Clear();
    }
}
