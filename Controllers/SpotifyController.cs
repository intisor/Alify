using Alify.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alify.Controllers
{
    [Route("api/spotify")]
    [ApiController]
    public class SpotifyController : ControllerBase
    {
        private readonly SpotifyQueueMonitorService _monitorService;
        private readonly SpotifyService _spotifyService;

        public SpotifyController(SpotifyQueueMonitorService monitorService, SpotifyService spotifyService)
        {
            _monitorService = monitorService;
            _spotifyService = spotifyService;
        }

        [HttpPost("start-monitor")]
        public IActionResult StartMonitor()
        {
            var spotify = _spotifyService.GetSpotifyClient();
            if (spotify == null) 
                return BadRequest("Authentication required. Please login to Spotify first.");

            _monitorService.StartMonitoring();
            return Ok("Queue monitoring started successfully.");
        }

        [HttpPost("stop-monitor")]
        public IActionResult StopMonitor()
        {
            _monitorService.StopMonitoring();
            return Ok("Queue monitoring stopped successfully.");
        }

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