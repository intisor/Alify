using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alify.Pages
{
    public class SpotifyEventsModel : PageModel
    {
        private readonly SseService _sseService;
        private readonly SpotifyService _spotifyService;
        private readonly ILogger<SpotifyEventsModel> _logger;
        private CancellationTokenSource _cts;
        public SpotifyEventsModel(SseService sseService,SpotifyService spotifyService, ILogger<SpotifyEventsModel> logger)
        {
            _sseService = sseService;
            _logger = logger;
            _spotifyService = spotifyService;
            _cts = new CancellationTokenSource();
        }
        public async Task<IActionResult> OnGet()
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            string connectionId = _sseService.AddConnection(Response);
            HttpContext.Items["SseConnectionId"] = connectionId;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        var spotify = await _spotifyService.GetSpotifyClientAsync();
                        if (spotify != null)
                        {
                            var playbackInfo = await _spotifyService.GetCurrentPlaybackInfoAsync(spotify);
                            if (playbackInfo != null)
                            {
                                await _sseService.SendPlaybackInfoAsync(playbackInfo);
                            }
                            else
                            {
                                _logger.LogWarning("No playback info available.");
                            }
                        }
                        else
                        {
                            _logger.LogError("Failed to get Spotify client.");
                        }
                        await Task.Delay(TimeSpan.FromSeconds(1)); // Adjust the delay as needed
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("SSE task cancelled.");
                }
                finally
                {
                    _sseService.RemoveConnection(connectionId);
                }

            });

            _cts.Cancel();
            _sseService.RemoveConnection(connectionId);
            return new EmptyResult(); // Return an empty result to prevent further processing
        }
    }
}
