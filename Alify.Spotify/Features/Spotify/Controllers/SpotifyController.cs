using Alify.Core.Services;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alify.Controllers
{
    /// <summary>
    /// Controller for handling Spotify-related actions, such as monitoring playback and content filtering.
    /// </summary>
    [Route("api/spotify")]
    [ApiController]
    public class SpotifyController : ControllerBase
    {
        private readonly SpotifyPlaybackMonitorService _monitorService;
        private readonly SpotifyService _spotifyService;
        private readonly ArtistLyricService _artistLyricService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpotifyController"/> class.
        /// </summary>
        /// <param name="monitorService">The unified service for monitoring Spotify playback and content filtering.</param>
        /// <param name="spotifyService">The service for interacting with the Spotify API.</param>
        /// <param name="artistLyricService">The service for fetching artist lyrics.</param>
        public SpotifyController(SpotifyPlaybackMonitorService monitorService, SpotifyService spotifyService, ArtistLyricService artistLyricService)
        {
            _monitorService = monitorService;
            _spotifyService = spotifyService;
            _artistLyricService = artistLyricService;
        }

        /// <summary>
        /// Starts the Spotify playback monitor for content filtering and real-time updates.
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
            return Ok("Playback monitoring started.");
        }

        /// <summary>
        /// Stops the Spotify playback monitor.
        /// </summary>
        /// <returns>A message indicating that monitoring has stopped.</returns>
        [HttpPost("stop-monitor")]
        public async Task<IActionResult> StopMonitor()
        {
            await _monitorService.StopMonitoringAsync();
            return Ok("Playback monitoring stopped.");
        }

        /// <summary>
        /// Gets the current status of the playback monitor.
        /// </summary>
        /// <returns>An object with the monitoring status and service health.</returns>
        [HttpGet("monitor-status")]
        public IActionResult GetMonitorStatus()
        {
            return Ok(new 
            {
                IsMonitoring = _monitorService.IsMonitoring,
                IsRunning = _monitorService.IsRunning,
                Status = _monitorService.IsMonitoring ? "Monitoring" : "Idle",
                ServiceStatus = _monitorService.IsRunning ? "Running" : "Stopped"
            });
        }
    }
}