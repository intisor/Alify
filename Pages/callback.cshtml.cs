using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alify.Pages
{
    public class callbackModel : PageModel
    {
        private readonly SpotifyService _spotifyService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public callbackModel(SpotifyService spotifyService, IHttpContextAccessor httpContextAccessor)
        {
            _spotifyService = spotifyService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IActionResult> OnGetAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
                return BadRequest("Missing code.");

            // Fix: Adjusted to match the actual return type of ProcessCallbackAsync  
            var result = await _spotifyService.ProcessCallbackAsync(code);

            // Assuming ProcessCallbackAsync returns a tuple (SpotifyClient, string)  
            var spotifyClient = result.Item1;
            var accessToken = result.Item2;

            if (spotifyClient == null || string.IsNullOrEmpty(accessToken))
                return BadRequest("Failed to authenticate.");

            // Save access token to session for reuse  
            _httpContextAccessor.HttpContext?.Session.SetString("SpotifyAccessToken", accessToken);

            // Redirect to dashboard or wherever next  
            return RedirectToPage("/Dashboard");
        }
    }
}
