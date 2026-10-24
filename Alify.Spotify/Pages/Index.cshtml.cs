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

    public async Task<IActionResult> OnGetAsync()
    {
        if (await _spotifyService.EnsureAuthenticatedAsync())
        {
            return RedirectToPage("/Dashboard/Dashboard");
        }

        IsAuthenticated = false;
        return Page();
    }public IActionResult OnPostLogin(string? byokClientId = null)
    {
        try
        {
            var loginUrl = _spotifyService.StartAuth(byokClientId);
            _logger.LogInformation("Starting Spotify login BYOK={UseByok} ClientId={ClientId}", !string.IsNullOrEmpty(byokClientId), byokClientId);
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
