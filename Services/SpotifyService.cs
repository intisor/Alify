using Alify.Models;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

public class SpotifyService
{
    private readonly IConfiguration _config;
    private static EmbedIOAuthServer _server;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SpotifyService(IConfiguration config, IHttpContextAccessor httpContextAccessor)
    {
        _config = config;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> StartAuthAsync()
    {
        string clientId = _config["Spotify:ClientId"]!;
        string redirectUri = _config["Spotify:RedirectUri"]!;

        _server = new EmbedIOAuthServer(new Uri(redirectUri), 7236);
        await _server.Start();

        var loginRequest = new LoginRequest(
            new Uri(redirectUri),
            clientId,
            LoginRequest.ResponseType.Code
        )
        {
            Scope = new[] {
                Scopes.PlaylistModifyPrivate,
                Scopes.PlaylistModifyPublic,
                Scopes.UserReadCurrentlyPlaying,
                Scopes.UserReadPlaybackState
            }
        };

        var uri = loginRequest.ToUri();
        return uri.ToString();
    }

    public async Task<(SpotifyClient, string)> ProcessCallbackAsync(string code)
    {
        var config = SpotifyClientConfig.CreateDefault();
        var tokenResponse = await new OAuthClient().RequestToken(
            new AuthorizationCodeTokenRequest(
                _config["Spotify:ClientId"]!,
                _config["Spotify:ClientSecret"]!,
                code,
                new Uri(_config["Spotify:RedirectUri"]!)
            )
        );

        var authenticator = new AuthorizationCodeAuthenticator(
       _config["Spotify:ClientId"]!,
       _config["Spotify:ClientSecret"]!,
       tokenResponse);

        _httpContextAccessor.HttpContext!.Session.SetString("SpotifyAccessToken", tokenResponse.AccessToken);
        var spotifyClient = new SpotifyClient(config.WithAuthenticator(authenticator));
        return (spotifyClient, tokenResponse.AccessToken);
    }
    public async Task<SpotifyPlaybackInfo> GetCurrentPlaybackAsync(SpotifyClient spotify)
    {
        var playbackInfo = new SpotifyPlaybackInfo();

        // 1. Get currently playing track
        var currentlyPlaying = await spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
        if (currentlyPlaying?.Item is FullTrack currentTrack)
        {
            playbackInfo.CurrentlyPlaying = currentTrack;
        }

        // 2. Get queue (optional: not supported in all regions/devices)
        try
        {
            var queue = await spotify.Player.GetQueue();
            if (queue.Queue?.Any() == true)
            {
                // Filter and cast only FullTrack items from the queue
                playbackInfo.Queue = queue.Queue
                    .OfType<FullTrack>()
                    .ToList();
            }
        }
        catch (APIException ex)
        {
            Console.WriteLine($"Queue not available: {ex.Message}");
        }

        return playbackInfo;
    }

}
