using Alify.Models;

namespace Alify.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlaybackController : ControllerBase
    {
        private readonly SpotifyService _spotifyService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PlaybackController(SpotifyService spotifyService, IHttpContextAccessor httpContextAccessor)
        {
            _spotifyService = spotifyService;
            _httpContextAccessor = httpContextAccessor;
        }

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

            return Ok(playbackInfo);
        }
    }
}
