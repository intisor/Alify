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

        public async Task<IActionResult> OnGetAsync(string code, string state)
        {
            var storedState = HttpContext.Session.GetString("State");
            if (state != storedState) return BadRequest("Invalid state parameter");

            var spotifyService = HttpContext.RequestServices.GetRequiredService<SpotifyService>();
            await spotifyService.UpdateAuthAsync(code);

            return RedirectToPage("/Dashboard");
        }
    }
}
