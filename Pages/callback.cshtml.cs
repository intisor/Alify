namespace Alify.Pages
{
    public class callbackModel : PageModel
    {
        private readonly SpotifyService _spotifyService;

        public callbackModel(SpotifyService spotifyService)
        {
            _spotifyService = spotifyService;
        }

        public async Task<IActionResult> OnGetAsync(string code, string state, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                TempData["ErrorMessage"] = $"Spotify authorization failed: {error}";
                return RedirectToPage("/Index");
            }
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
