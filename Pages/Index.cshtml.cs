using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

public class IndexModel : PageModel
{
    private readonly SpotifyService _spotifyService;

    public IndexModel(SpotifyService spotifyService)
    {
        _spotifyService = spotifyService;
    }

    public async Task<IActionResult> OnGetLoginAsync()
    {
        var loginUrl = await _spotifyService.StartAuthAsync();
        return Redirect(loginUrl);
    }
}
