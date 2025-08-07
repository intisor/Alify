using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

public class IndexModel(SpotifyService spotifyService) : PageModel
{
    private readonly SpotifyService _spotifyService = spotifyService;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool IsAuthenticated { get; set; }

    public void OnGet([FromQuery] string? error, [FromQuery] string? success)
    {
        IsAuthenticated = _spotifyService.IsAuthenticated();
        ErrorMessage = error ?? TempData[key: "ErrorMessage"] as string;
        SuccessMessage = success ?? TempData[key: "SuccessMessage"] as string;
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
