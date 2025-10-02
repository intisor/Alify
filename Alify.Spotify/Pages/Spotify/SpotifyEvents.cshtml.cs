using Alify.Features.Spotify.Services;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alify.Pages
{
    public class SpotifyEventsModel : PageModel
    {
        private readonly SseService _sseService;
        private readonly ILogger<SpotifyEventsModel> _logger;

        public SpotifyEventsModel(SseService sseService, ILogger<SpotifyEventsModel> logger)
        {
            _sseService = sseService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGet()
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("Access-Control-Allow-Origin", "*");

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