using Alify.Core.Models;
using Alify.Features.Spotify.Models;
using Alify.Features.Spotify.Services;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SpotifyAPI.Web;

namespace Alify.Controllers
{
    /// <summary>
    /// API controller for playback-related operations including queue management,
    /// repeat control, and sleep timer — addressing top Spotify Community requests.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PlaybackController : ControllerBase
    {
        private readonly SpotifyService _spotifyService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly SleepTimerService _sleepTimerService;
        private readonly ILogger<PlaybackController> _logger;

        public PlaybackController(
            SpotifyService spotifyService,
            IHttpContextAccessor httpContextAccessor,
            SleepTimerService sleepTimerService,
            ILogger<PlaybackController> logger)
        {
            _spotifyService = spotifyService;
            _httpContextAccessor = httpContextAccessor;
            _sleepTimerService = sleepTimerService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current playback information including track/episode, queue, and sleep timer status.
        /// </summary>
        [HttpGet("current")]
        [OutputCache(Duration = 10)]
        public async Task<ActionResult<SpotifyPlaybackInfo>> GetCurrentPlayback()
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized();
            }

            var spotify = new SpotifyClient(accessToken);
            var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotify);

            if (playbackInfo == null)
            {
                return NoContent();
            }

            // Attach sleep timer status to the response
            playbackInfo.SleepTimer = _sleepTimerService.GetStatus();

            return Ok(playbackInfo);
        }

        /// <summary>
        /// Adds an item (track or episode) to the playback queue.
        /// Addresses the popular "Play Next" / queue management requests from the Spotify Community.
        /// </summary>
        [HttpPost("add-to-queue")]
        public async Task<IActionResult> AddToQueue([FromBody] AddToQueueRequest request)
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized();
            }

            if (string.IsNullOrEmpty(request?.Uri))
            {
                return BadRequest("Spotify URI is required (e.g., spotify:track:xxx or spotify:episode:xxx).");
            }

            var spotify = new SpotifyClient(accessToken);
            var success = await _spotifyService.AddToQueueAsync(spotify, request.Uri);
            return success
                ? Ok(new { message = "Item added to queue." })
                : StatusCode(500, "Failed to add item to queue.");
        }

        /// <summary>
        /// Sets the repeat mode for playback. Works for both tracks and episodes.
        /// Addresses the community request for a repeat button on podcast episodes.
        /// </summary>
        [HttpPut("repeat")]
        public async Task<IActionResult> SetRepeatMode([FromBody] RepeatModeRequest request)
        {
            var accessToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized();
            }

            if (string.IsNullOrEmpty(request?.State))
            {
                return BadRequest("Repeat state is required: 'track', 'context', or 'off'.");
            }

            var spotify = new SpotifyClient(accessToken);
            var success = await _spotifyService.SetRepeatModeAsync(spotify, request.State);
            return success
                ? Ok(new { message = $"Repeat mode set to '{request.State}'." })
                : StatusCode(500, "Failed to set repeat mode.");
        }

        // --- Sleep Timer Endpoints ---

        /// <summary>
        /// Starts a sleep timer that will pause playback after the specified duration.
        /// Replaces Spotify's buggy native sleep timer (recurring community complaint since 2024).
        /// </summary>
        [HttpPost("sleep-timer/start")]
        public IActionResult StartSleepTimer([FromBody] SleepTimerRequest request)
        {
            if (request?.DurationMinutes is null or <= 0)
            {
                return BadRequest("Duration in minutes is required and must be positive.");
            }

            _sleepTimerService.StartTimer(request.DurationMinutes.Value);
            return Ok(new
            {
                message = $"Sleep timer started for {request.DurationMinutes} minutes.",
                status = _sleepTimerService.GetStatus()
            });
        }

        /// <summary>
        /// Cancels the active sleep timer.
        /// </summary>
        [HttpPost("sleep-timer/cancel")]
        public IActionResult CancelSleepTimer()
        {
            _sleepTimerService.CancelTimer();
            return Ok(new { message = "Sleep timer cancelled." });
        }

        /// <summary>
        /// Gets the current sleep timer status.
        /// </summary>
        [HttpGet("sleep-timer/status")]
        public IActionResult GetSleepTimerStatus()
        {
            return Ok(_sleepTimerService.GetStatus());
        }
    }
}
