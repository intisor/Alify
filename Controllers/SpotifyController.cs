using Alify.Services;

namespace Alify.Controllers
{
    /// <summary>
    /// Controller for handling Spotify-related actions, such as monitoring the queue.
    /// </summary>
    [Route("api/spotify")]
    [ApiController]
    public class SpotifyController : ControllerBase
    {
        private readonly SpotifyQueueMonitorService _monitorService;
        private readonly SpotifyService _spotifyService;
        private readonly ArtistLyricService _artistLyricService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpotifyController"/> class.
        /// </summary>
        /// <param name="monitorService">The service for monitoring the Spotify queue.</param>
        /// <param name="spotifyService">The service for interacting with the Spotify API.</param>
        /// <param name="artistLyricService">The service for fetching artist lyrics.</param>
        public SpotifyController(SpotifyQueueMonitorService monitorService, SpotifyService spotifyService, ArtistLyricService artistLyricService)
        {
            _monitorService = monitorService;
            _spotifyService = spotifyService;
            _artistLyricService = artistLyricService;
        }

        /// <summary>
        /// Starts the Spotify queue monitor.
        /// </summary>
        /// <returns>A message indicating that monitoring has started, or a bad request if not authenticated.</returns>
        [HttpPost("start-monitor")]
        public async Task<IActionResult> StartMonitor()
        {
            if (!_spotifyService.IsAuthenticated())
            {
                return BadRequest("Authentication required. Please login to Spotify first.");
            }

            await _monitorService.StartMonitoringAsync();
            return Ok("Queue monitoring started.");
        }

        /// <summary>
        /// Stops the Spotify queue monitor.
        /// </summary>
        /// <returns>A message indicating that monitoring has stopped.</returns>
        [HttpPost("stop-monitor")]
        public async Task<IActionResult> StopMonitor()
        {
            await _monitorService.StopMonitoringAsync();
            return Ok("Queue monitoring stopped.");
        }

        /// <summary>
        /// Gets the current status of the queue monitor.
        /// </summary>
        /// <returns>An object with the monitoring status.</returns>
        [HttpGet("monitor-status")]
        public IActionResult GetMonitorStatus()
        {
            return Ok(new {
                IsMonitoring = _monitorService.IsMonitoring,
                Status = _monitorService.IsMonitoring ? "Running" : "Stopped"
            });
        }
    }
}