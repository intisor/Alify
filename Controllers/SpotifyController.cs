using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using SpotifyAPI.Web;

namespace Alify.Controllers
{
    [Route("api/spotify")]
    [ApiController]
    public class SpotifyController : ControllerBase
    {
        private readonly SpotifyService _spotifyService;
        private CancellationTokenSource _cts = new();

        public SpotifyController(SpotifyService spotifyService)
        {
            _spotifyService = spotifyService;
        }

        [HttpPost("monitor-queue")]
        public async Task<IActionResult> MonitorQueue()
        {
            var spotify = _spotifyService.GetSpotifyClient();
            if (spotify == null) return StatusCode(500, "Authentication required");

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var playbackInfo = await _spotifyService.GetCurrentPlaybackAsync(spotify);
                    if(playbackInfo is not null  && playbackInfo.RemaininTimeMs <= 15000)
                    {

                    }
                    if (await _spotifyService.SkipIfFlaggedAsync(playbackInfo, spotify) != null)
                    {
                        Console.WriteLine("cleaned some stuffs");
                    }
                    await Task.Delay(2000, _cts.Token);
                }
            }
            catch (TaskCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                Console.WriteLine("Background monitor failed: " + ex.Message);
            }

            return Ok("Monitoring started.");
        }

        [HttpPost("stop-monitor")]
        public IActionResult StopMonitor()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            return Ok("Monitoring stopped");
        }
    }
}