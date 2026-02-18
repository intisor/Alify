namespace Alify.Features.Spotify.Models;

/// <summary>
/// Request DTO for adding an item to the playback queue.
/// </summary>
public class AddToQueueRequest
{
    public string? Uri { get; set; }
}

/// <summary>
/// Request DTO for setting repeat mode.
/// </summary>
public class RepeatModeRequest
{
    public string? State { get; set; }
}

/// <summary>
/// Request DTO for starting a sleep timer.
/// </summary>
public class SleepTimerRequest
{
    public int? DurationMinutes { get; set; }
}
