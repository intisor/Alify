using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alify.Spotify.Pages.Authentication
{
    public class CallbackModel : PageModel
    {
        private readonly SpotifyService _spotifyService;
        private readonly ILogger<CallbackModel> _logger;

        public CallbackModel(SpotifyService spotifyService, ILogger<CallbackModel> logger)
        {
            _spotifyService = spotifyService;
            _logger = logger;
        }

        public bool IsAuthenticated { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Spotify authentication error: {Error}", error);
                ErrorMessage = $"Spotify authentication was cancelled or failed: {error}";
                return Page();
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                _logger.LogWarning("Missing code or state parameter in callback");
                ErrorMessage = "Invalid callback request. Missing required parameters.";
                return Page();
            }

            try
            {
                var success = await _spotifyService.UpdateAuthAsync(code, state);
                
                if (success)
                {
                    _logger.LogInformation("Spotify authentication successful");
                    IsAuthenticated = true;
                    
                    // Redirect to dashboard after 2 seconds
                    Response.Headers.Append("Refresh", "2; url=/Dashboard");
                    return Page();
                }
                else
                {
                    _logger.LogWarning("Spotify authentication failed: Invalid state or token exchange failed");
                    ErrorMessage = "Authentication failed. Please try again.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Spotify authentication callback");
                ErrorMessage = "An error occurred during authentication. Please try again.";
                return Page();
            }
        }
    }
}
