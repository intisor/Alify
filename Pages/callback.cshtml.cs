using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alify.Pages
{
    /// <summary>
    /// Page model for the Spotify callback page.
    /// </summary>
    public class callbackModel : PageModel
    {
        private readonly SpotifyService _spotifyService;

        /// <summary>
        /// Initializes a new instance of the <see cref="callbackModel"/> class.
        /// </summary>
        /// <param name="spotifyService">The Spotify service.</param>
        public callbackModel(SpotifyService spotifyService)
        {
            _spotifyService = spotifyService;
        }

        /// <summary>
        /// Handles the GET request for the callback page.
        /// </summary>
        /// <param name="code">The authorization code from Spotify.</param>
        /// <param name="state">The state parameter for CSRF protection.</param>
        /// <param name="error">Any error returned from Spotify.</param>
        /// <returns>A redirect to the appropriate page.</returns>
        public async Task<IActionResult> OnGetAsync(string code, string state, string error)
        {
            // Handle authorization errors
            if (!string.IsNullOrEmpty(error))
            {
                TempData["ErrorMessage"] = $"Spotify authorization failed: {error}";
                return RedirectToPage("/Index");
            }

            // Validate required parameters
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                TempData["ErrorMessage"] = "Invalid authorization response from Spotify.";
                return RedirectToPage("/Index");
            }

            try
            {
                var success = await _spotifyService.UpdateAuthAsync(code, state);
                if (success)
                {
                    TempData["SuccessMessage"] = "Successfully connected to Spotify!";
                    return RedirectToPage("/Dashboard");
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to authenticate with Spotify. Please try again.";
                    return RedirectToPage("/Index");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Authentication error: {ex.Message}";
                return RedirectToPage("/Index");
            }
        }
    }
}
