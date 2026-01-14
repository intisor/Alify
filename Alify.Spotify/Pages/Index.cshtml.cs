using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alify.Spotify.Pages;

public class IndexModel : PageModel
{
    private readonly SpotifyService _spotifyService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(SpotifyService spotifyService, ILogger<IndexModel> logger)
    {
        _spotifyService = spotifyService;
        _logger = logger;
    }

    public bool IsAuthenticated { get; set; }

    public void OnGet()
    {
        IsAuthenticated = _spotifyService.IsAuthenticated();
    }

    public IActionResult OnPostLogin()
    {
        try
        {
            var loginUrl = _spotifyService.StartAuth();
            _logger.LogInformation("Redirecting to Spotify login: {LoginUrl}", loginUrl);
            return Redirect(loginUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Spotify authentication");
            TempData["ErrorMessage"] = "Failed to start Spotify login. Please try again.";
            return RedirectToPage();
        }
    }

    public IActionResult OnPostLogout()
    {
        try
        {
            _spotifyService.ClearAuthentication();
            _logger.LogInformation("User logged out from Spotify");
            TempData["SuccessMessage"] = "Successfully disconnected from Spotify.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            TempData["ErrorMessage"] = "Error during logout. Please try again.";
        }
        
        return RedirectToPage();
    }
}
