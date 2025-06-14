using Alify.Models;
using Alify.Services;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

public class SpotifyService
{
    private readonly IConfiguration _config;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LyricService _lyricService;
    private SpotifyClient _spotifyClient;
    private AuthorizationCodeAuthenticator _authenticator;

    public SpotifyService(IConfiguration config, IHttpContextAccessor httpContextAccessor, LyricService lyricService)
    {
        _config = config;
        _httpContextAccessor = httpContextAccessor;
        _lyricService = lyricService;
    }

    public async Task<string> StartAuthAsync()
    {
        string clientId = _config["Spotify:ClientId"]!;
        string redirectUri = _config["Spotify:RedirectUri"]!;

        var loginRequest = new LoginRequest(
            new Uri(redirectUri),
            clientId,
            LoginRequest.ResponseType.Code
        )
        {
            Scope = new[] { Scopes.UserReadCurrentlyPlaying, Scopes.UserReadPlaybackState, Scopes.UserModifyPlaybackState }
        };

        return loginRequest.ToUri().ToString();
    }

    public async Task UpdateAuthAsync(string code)
    {
        var tokenResponse = await new OAuthClient().RequestToken(
            new AuthorizationCodeTokenRequest(
                _config["Spotify:ClientId"]!,
                _config["Spotify:ClientSecret"]!,
                code,
                new Uri(_config["Spotify:RedirectUri"]!)
            )
        );

        _authenticator = new AuthorizationCodeAuthenticator(_config["Spotify:ClientId"]!, _config["Spotify:ClientSecret"]!, tokenResponse);
        _spotifyClient = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithAuthenticator(_authenticator));
        _httpContextAccessor.HttpContext!.Session.SetString("SpotifyAccessToken", tokenResponse.AccessToken);
    }

    public SpotifyClient GetSpotifyClient()
    {
        if (_spotifyClient == null)
        {
            var sessionToken = _httpContextAccessor.HttpContext?.Session.GetString("SpotifyAccessToken");
            if (string.IsNullOrEmpty(sessionToken))
            {
                Console.WriteLine("No Spotify access token found. Authentication required.");
                return null;
            }
            _spotifyClient = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithToken(sessionToken));
        }
        return _spotifyClient;
    }

    public async Task<SpotifyPlaybackInfo> GetCurrentPlaybackAsync(SpotifyClient spotify)
    {
        var playbackInfo = new SpotifyPlaybackInfo();
        var currentlyPlaying = await spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
        if (currentlyPlaying?.Item is FullTrack currentTrack)
        {
            playbackInfo.CurrentlyPlaying = currentTrack;
            playbackInfo.RemaininTimeMs = currentTrack.DurationMs - currentlyPlaying.ProgressMs;
        }
    
        try
        {
            var queue = await spotify.Player.GetQueue();
            if (queue.Queue != null)
            {
                playbackInfo.Queue = [.. queue.Queue.OfType<FullTrack>()];
                var flaggedUrls = new List<string>();
                foreach (var track in playbackInfo.Queue)
                {
                    var lyrics = await _lyricService.GetLyricsAsync(track.Artists[0].Name, track.Name);
                    if (!string.IsNullOrWhiteSpace(lyrics))
                    {
                        var moderation = await _lyricService.ModerateLyricsAsync(lyrics);
                        if (moderation != null && !moderation.suitable_for_kids) flaggedUrls.Add(track.Uri);
                    }
                    else if (track.Explicit) flaggedUrls.Add(track.Uri);
                    playbackInfo.IsFlagged = flaggedUrls;
                }
            }
        }
        catch (APIException ex) { Console.WriteLine($"Queue not available: {ex.Message}"); }
        return playbackInfo;
    }

    public async Task<FullTrack> SkipIfFlaggedAsync(SpotifyPlaybackInfo playbackInfo, SpotifyClient spotify)
    {
        var nextTrack = playbackInfo.Queue?.FirstOrDefault();
        if (nextTrack == null || playbackInfo.IsFlagged?.Contains(nextTrack.Uri) != true) return null;
        await spotify.Player.SkipNext(new PlayerSkipNextRequest());
        Console.WriteLine($"Skipped flagged track: {nextTrack.Name} by {nextTrack.Artists[0].Name}");
        return nextTrack;
    }
}