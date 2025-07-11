using Alify.Models;
using Alify.Services;
using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;

public class SpotifyService
{
    private readonly IConfiguration _config;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LyricService _lyricService;
    private readonly IMemoryCache _cache;
    private readonly SpotifyRequestCache _requestCache;
    private SpotifyClient _spotifyClient;
    private AuthorizationCodeAuthenticator _authenticator;

    public SpotifyService(IConfiguration config, IHttpContextAccessor httpContextAccessor, LyricService lyricService, IMemoryCache cache, SpotifyRequestCache requestCache)
    {
        _config = config;
        _httpContextAccessor = httpContextAccessor;
        _lyricService = lyricService;
        _cache = cache;
        _requestCache = requestCache;
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
            Scope = [Scopes.UserReadCurrentlyPlaying, Scopes.UserReadPlaybackState, Scopes.UserModifyPlaybackState]
        };

        // Use Task.FromResult to return a completed task with the result
        return await Task.FromResult(loginRequest.ToUri().ToString());
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

    private async Task<Track> CreateTrackFromFullTrackAsync(FullTrack fullTrack)
    {
        string lyrics = null;
        
        // Check if artists collection is not null and has at least one artist
        if (fullTrack.Artists != null && fullTrack.Artists.Count > 0)
        {
            lyrics = await _lyricService.GetLyricsAsync(fullTrack.Artists[0].Name, fullTrack.Name);
        }
        
        var isFlagged = false;
        var flagReason = "";

        // First check if the track is marked as explicit by Spotify
        if (fullTrack.Explicit)
        {
            isFlagged = true;
            flagReason = "Explicit content marker";
        }

        // Then check lyrics if available
        if (!string.IsNullOrWhiteSpace(lyrics))
        {
            var moderation = await _lyricService.ModerateLyricsAsync(lyrics);
            if (moderation != null && !moderation.suitable_for_kids)
            {
                isFlagged = true;
                flagReason = isFlagged ? flagReason + " + Lyrics content" : "Lyrics content";
            }
        }

        // Log flagging for debugging
        if (isFlagged)
        {
            Console.WriteLine($"Track flagged: {fullTrack.Name} by {fullTrack.Artists[0].Name} - Reason: {flagReason}");
        }

        return new Track
        {
            FullTrack = fullTrack,
            Lyrics = lyrics,
            IsFlagged = isFlagged
        };
    }

    private string GetUserSpecificCacheKey(string baseKey)
    {
        var userId = _httpContextAccessor.HttpContext?.Session.Id ?? "anonymous";
        return $"{baseKey}_{userId}";
    }

    public async Task<SpotifyPlaybackInfo> GetCurrentPlaybackAsync(SpotifyClient spotify)
    {
        var cacheKey = GetUserSpecificCacheKey("PlaybackInfo");
        if (_cache.TryGetValue(cacheKey, out SpotifyPlaybackInfo playbackInfo))
        {
            return playbackInfo;
        }

        playbackInfo = new SpotifyPlaybackInfo();
        var currentlyPlayingResponse = await _requestCache.GetCurrentlyPlayingAsync(spotify);
        if (currentlyPlayingResponse?.Item is FullTrack currentFullTrack)
        {
            playbackInfo.CurrentlyPlaying = await CreateTrackFromFullTrackAsync(currentFullTrack);
            playbackInfo.RemainingTimeMs = currentFullTrack.DurationMs - currentlyPlayingResponse.ProgressMs;
        }

        try
        {
            var queueResponse = await _requestCache.GetQueueAsync(spotify);
            if (queueResponse?.Queue != null)
            {
                var queueTasks = queueResponse.Queue.OfType<FullTrack>().Select(CreateTrackFromFullTrackAsync);
                playbackInfo.Queue = [.. (await Task.WhenAll(queueTasks))];
            }
        }
        catch (APIException ex) { Console.WriteLine($"Queue not available: {ex.Message}"); }

        _cache.Set(cacheKey, playbackInfo, TimeSpan.FromMinutes(10));
        return playbackInfo;
    }

    public async Task<SpotifyPlaybackInfo> UpdatePlaybackWithNewSongAsync(SpotifyClient spotify)
    {
        var cacheKey = GetUserSpecificCacheKey("PlaybackInfo");
        var existingPlaybackInfo = _cache.Get<SpotifyPlaybackInfo>(cacheKey);
        if (existingPlaybackInfo == null)
        {
            return await GetCurrentPlaybackAsync(spotify);
        }

        var currentlyPlayingResponse = await _requestCache.GetCurrentlyPlayingAsync(spotify);
        if (currentlyPlayingResponse?.Item is FullTrack currentFullTrack)
        {
            if (existingPlaybackInfo.CurrentlyPlaying?.FullTrack?.Id != currentFullTrack.Id)
            {
                // Clear request cache when song changes to ensure fresh queue data
                _requestCache.ClearCache();
                
                var trackInQueue = existingPlaybackInfo.Queue.FirstOrDefault(t => t.FullTrack.Id == currentFullTrack.Id);
                if (trackInQueue != null)
                {
                    existingPlaybackInfo.CurrentlyPlaying = trackInQueue;
                    existingPlaybackInfo.Queue.Remove(trackInQueue);
                }
                else
                {
                    existingPlaybackInfo.CurrentlyPlaying = await CreateTrackFromFullTrackAsync(currentFullTrack);
                }
            }
            existingPlaybackInfo.RemainingTimeMs = currentFullTrack.DurationMs - currentlyPlayingResponse.ProgressMs;
        }

        try
        {
            var queueResponse = await _requestCache.GetQueueAsync(spotify);
            if (queueResponse?.Queue != null)
            {
                // Replace the entire queue with fresh data from Spotify instead of just adding new tracks
                var freshQueueTracks = queueResponse.Queue.OfType<FullTrack>().ToList();
                var freshQueueTasks = freshQueueTracks.Select(CreateTrackFromFullTrackAsync);
                var freshlyFetchedTracks = await Task.WhenAll(freshQueueTasks);
                existingPlaybackInfo.Queue = freshlyFetchedTracks.ToList();
            }
        }
        catch (APIException ex) { Console.WriteLine($"Queue not available: {ex.Message}"); }

        _cache.Set(cacheKey, existingPlaybackInfo, TimeSpan.FromMinutes(10));
        return existingPlaybackInfo;
    }

    public async Task<FullTrack> SkipIfFlaggedAsync(SpotifyPlaybackInfo playbackInfo, SpotifyClient spotify)
    {
        var nextTrack = playbackInfo.Queue?.FirstOrDefault();
        if (nextTrack == null || !nextTrack.IsFlagged) return null;

        await spotify.Player.SkipNext(new PlayerSkipNextRequest());
        
        var artistName = nextTrack.FullTrack.Artists != null && nextTrack.FullTrack.Artists.Count > 0 
            ? nextTrack.FullTrack.Artists[0].Name 
            : "Unknown Artist";
        
        Console.WriteLine($"Skipped flagged track: {nextTrack.FullTrack.Name} by {artistName}");
        return nextTrack.FullTrack;
    }

    public async Task<bool> SkipCurrentTrackIfFlaggedAsync(SpotifyClient spotify)
    {
        try
        {
            var currentlyPlaying = await _requestCache.GetCurrentlyPlayingAsync(spotify);
            if (currentlyPlaying?.Item is FullTrack track)
            {
                var trackInfo = await CreateTrackFromFullTrackAsync(track);
                if (trackInfo.IsFlagged)
                {
                    await spotify.Player.SkipNext(new PlayerSkipNextRequest());
                    Console.WriteLine($"Skipped flagged current track: {track.Name} by {track.Artists[0].Name}");
                    return true;
                }
            }
        }
        catch (APIException ex)
        {
            Console.WriteLine($"API error while checking current track: {ex.Message}");
        }
        return false;
    }

    public async Task<SpotifyPlaybackInfo> GetCurrentPlaybackInfoAsync(SpotifyClient spotify)
    {
        try
        {
            var currentlyPlaying = await _requestCache.GetCurrentlyPlayingAsync(spotify);
            if (currentlyPlaying?.Item is not FullTrack currentTrack) return null;

            var playbackInfo = new SpotifyPlaybackInfo
            {
                CurrentlyPlaying = await CreateTrackFromFullTrackAsync(currentTrack),
                RemainingTimeMs = currentTrack.DurationMs - currentlyPlaying.ProgressMs
            };

            try
            {
                var queue = await _requestCache.GetQueueAsync(spotify);
                if (queue?.Queue != null)
                {
                    var queueTasks = queue.Queue.OfType<FullTrack>().Select(CreateTrackFromFullTrackAsync);
                    playbackInfo.Queue = (await Task.WhenAll(queueTasks)).ToList();
                }
            }
            catch (APIException ex)
            {
                Console.WriteLine($"Queue not available: {ex.Message}");
            }

            return playbackInfo;
        }
        catch (APIException ex)
        {
            Console.WriteLine($"Error getting playback info: {ex.Message}");
            return null;
        }
    }
}