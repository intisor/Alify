using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

public class IndexModel : PageModel
{
    private readonly SpotifyService _spotifyService;

    public IndexModel(SpotifyService spotifyService)
    {
        _spotifyService = spotifyService;
    }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool IsAuthenticated { get; set; }

    public void OnGet([FromQuery] string? error, [FromQuery] string? success)
    {
        // Check if user is already authenticated
        IsAuthenticated = _spotifyService.IsAuthenticated();
        
        // Get messages from query parameters or TempData
        ErrorMessage = error ?? TempData["ErrorMessage"] as string;
        SuccessMessage = success ?? TempData["SuccessMessage"] as string;
    }

    public IActionResult OnGetLogin()
    {
        try
        {
            var loginUrl = _spotifyService.StartAuth();
            return Redirect(loginUrl);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to initiate Spotify login: {ex.Message}";
            return RedirectToPage();
        }
    }

    public IActionResult OnPostLogoutAsync()
    {
        _spotifyService.ClearAuthentication();
        TempData["SuccessMessage"] = "Successfully logged out from Spotify.";
        return RedirectToPage();
    }
}
