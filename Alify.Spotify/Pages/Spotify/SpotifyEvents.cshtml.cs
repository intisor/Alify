using Alify.Features.Spotify.Services;
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

        public SpotifyEventsModel(SseService sseService, SpotifyService spotifyService, ILogger<SpotifyEventsModel> logger)
        {
            _sseService = sseService;
            _spotifyService = spotifyService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGet()
        {
            if (!_spotifyService.IsAuthenticated())
            {
                _logger.LogWarning("Unauthorized SSE connection attempt rejected.");
                return Unauthorized();
            }

            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no");

            string connectionId = _sseService.AddConnection(Response);
            HttpContext.Items["SseConnectionId"] = connectionId;
            
            _logger.LogInformation("SSE connection established: {ConnectionId}", connectionId);

            // Send initial connection confirmation
            await _sseService.SendEventAsync("connected", new { connectionId, timestamp = DateTime.UtcNow });

            // Keep the connection alive by waiting for cancellation
            HttpContext.RequestAborted.Register(() =>
            {
                _sseService.RemoveConnection(connectionId);
                _logger.LogInformation("SSE connection cancelled: {ConnectionId}", connectionId);
            });

            // Wait for the request to be cancelled (client disconnect)
            try
            {
                await Task.Delay(Timeout.Infinite, HttpContext.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SSE connection completed: {ConnectionId}", connectionId);
            }
            finally
            {
                _sseService.RemoveConnection(connectionId);
            }

            return new EmptyResult();
        }
    }
}