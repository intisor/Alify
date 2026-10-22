using Alify.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alify.Controllers
{
  
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly SpotifyService _spotifyService;

       
        public AuthController(SpotifyService spotifyService)
        {
            _spotifyService = spotifyService;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetAuthStatus()
        {
            var isAuthenticated = await _spotifyService.EnsureAuthenticatedAsync();
            return Ok(new { isAuthenticated });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            _spotifyService.ClearAuthentication();
            return Ok(new { message = "Successfully logged out." });
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            var loginUrl = _spotifyService.StartAuth();
            return Ok(new { loginUrl });
        }
    }
}