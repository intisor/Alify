using System.Collections.Concurrent;
using System.Threading;

namespace Alify.Features.Spotify.Services;

public class PlaybackDashboardStats
{
    private static int _totalAudited;
    private static int _totalClean;
    private static int _totalFlagged;
    private static int _totalNoLyrics;
    private static readonly ConcurrentDictionary<string, bool> _recordedTracks = new();

    public int TotalAudited => _totalAudited;
    public int TotalClean => _totalClean;
    public int TotalFlagged => _totalFlagged;
    public int TotalNoLyrics => _totalNoLyrics;

    public static void RecordAudit(string trackId, bool isFlagged, bool isSkipped, bool hasLyrics)
    {
        if (string.IsNullOrWhiteSpace(trackId)) return;

        if (_recordedTracks.TryAdd(trackId, true))
        {
            Interlocked.Increment(ref _totalAudited);
            if (isFlagged || isSkipped) 
            {
                Interlocked.Increment(ref _totalFlagged);
            }
            else 
            {
                Interlocked.Increment(ref _totalClean);
            }

            if (!hasLyrics) 
            {
                Interlocked.Increment(ref _totalNoLyrics);
            }
        }
        else if ((isFlagged || isSkipped) && !_recordedTracks.ContainsKey($"{trackId}_flagged"))
        {
            _recordedTracks.TryAdd($"{trackId}_flagged", true);
            Interlocked.Increment(ref _totalFlagged);
            if (_totalClean > 0) 
            {
                Interlocked.Decrement(ref _totalClean);
            }
        }
    }

    public static void Reset()
    {
        Interlocked.Exchange(ref _totalAudited, 0);
        Interlocked.Exchange(ref _totalClean, 0);
        Interlocked.Exchange(ref _totalFlagged, 0);
        Interlocked.Exchange(ref _totalNoLyrics, 0);
        _recordedTracks.Clear();
    }
}
