namespace Alify.Core.Features.Analysis.Engine.Gemini;

/// <summary>
/// Consent declaration for external BYOK LLM processing (PRD-011 / DATA-009).
/// Explains that lyrics leave the local device and confirms user permission.
/// </summary>
public record ProviderConsent
{
    public bool IsConsented { get; init; } = false;
    public string ProviderName { get; init; } = "Gemini";
    public string ModelName { get; init; } = "gemini-2.5-flash";
    public DateTime? ConsentedAtUtc { get; init; }
    public string ScopeDisclosed { get; init; } =
        "Transmits normalized lyric lines and line numbers outside the local environment for semantic stance classification. " +
        "No Spotify identifiers, tokens, user accounts, or private metadata are included.";

    public static ProviderConsent Granted(string modelName = "gemini-2.5-flash") =>
        new()
        {
            IsConsented = true,
            ModelName = modelName,
            ConsentedAtUtc = DateTime.UtcNow
        };

    public static ProviderConsent Denied => new() { IsConsented = false };
}

/// <summary>
/// Transactional budget ledger preventing concurrent or runaway external API spend (PRD-011 / DATA-009).
/// </summary>
public class BudgetLedger
{
    private readonly int _dailyLimit;
    private int _reservedCount;
    private int _completedCount;
    private readonly object _lock = new();

    public BudgetLedger(int dailyLimit = 50)
    {
        if (dailyLimit <= 0) throw new ArgumentOutOfRangeException(nameof(dailyLimit), "Daily limit must be positive.");
        _dailyLimit = dailyLimit;
    }

    public int DailyLimit => _dailyLimit;

    public int ReservedCount
    {
        get { lock (_lock) return _reservedCount; }
    }

    public int CompletedCount
    {
        get { lock (_lock) return _completedCount; }
    }

    public int RemainingCapacity
    {
        get { lock (_lock) return Math.Max(0, _dailyLimit - (_reservedCount + _completedCount)); }
    }

    /// <summary>
    /// Atomically attempts to reserve capacity for a single API request.
    /// </summary>
    public bool TryReserve()
    {
        lock (_lock)
        {
            if (_reservedCount + _completedCount >= _dailyLimit)
            {
                return false;
            }
            _reservedCount++;
            return true;
        }
    }

    /// <summary>
    /// Confirms that a reserved request completed and increments completed count.
    /// </summary>
    public void CompleteReservation()
    {
        lock (_lock)
        {
            if (_reservedCount > 0)
            {
                _reservedCount--;
            }
            _completedCount++;
        }
    }

    /// <summary>
    /// Releases a reservation when a request fails before submission or cannot complete.
    /// </summary>
    public void ReleaseReservation()
    {
        lock (_lock)
        {
            if (_reservedCount > 0)
            {
                _reservedCount--;
            }
        }
    }
}

/// <summary>
/// Contract for sending prompts to the Gemini model (PRD-011).
/// </summary>
public interface IGeminiModelClient
{
    string ModelName { get; }
    Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken = default);
}
