using Alify.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alify.Controllers
{
    /// <summary>
    /// Controller for handling authentication-related actions.
    /// </summary>
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly SpotifyService _spotifyService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="spotifyService">The Spotify service.</param>
        public AuthController(SpotifyService spotifyService)
        {
            _spotifyService = spotifyService;
        }

        /// <summary>
        /// Gets the current authentication status.
        /// </summary>
        /// <returns>An object indicating whether the user is authenticated.</returns>
        [HttpGet("status")]
        public IActionResult GetAuthStatus()
        {
            return Ok(new { isAuthenticated = _spotifyService.IsAuthenticated() });
        }

        /// <summary>
        /// Logs the user out by clearing their authentication data.
        /// </summary>
        /// <returns>A success message.</returns>
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            _spotifyService.ClearAuthentication();
            return Ok(new { message = "Successfully logged out." });
        }

        /// <summary>
        /// Initiates the login process by providing a Spotify login URL.
        /// </summary>
        /// <returns>An object containing the login URL.</returns>
        [HttpGet("login")]
        public IActionResult Login()
        {
            var loginUrl = _spotifyService.StartAuth();
            return Ok(new { loginUrl });
        }
    }
}