using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using SpotifyAPI.Web;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Alify.Services;
using Serilog;
using Alify.Core.Models;
using Alify.Core.Infrastructure.Security;
using Alify.Features.Spotify.Events;
using Alify.Features.Spotify.Services;
using Microsoft.Extensions.Logging;

[DebuggerDisplay("IsAuthenticated: {IsAuthenticated()}, ClientId: {_spotifyOptions.ClientId}")]
public class SpotifyService
{
    // Dependencies injected through the constructor
    private readonly SpotifyOptions _spotifyOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly SpotifyRequestCache _requestCache;
    private readonly ISpotifySubject _spotifySubject;
    private readonly QueueService _queueService;
    private readonly FreeLyricsProviderService _freeLyricsProvider;
    private readonly ILogger<SpotifyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpotifyService"/> class.
    /// </summary>
    /// <param name="spotifyOptions">The Spotify settings from configuration.</param>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context to manage user-specific session data.</param>
    /// <param name="cache">In-memory cache for storing data to reduce API calls and improve performance.</param>
    /// <param name="requestCache">Specialized cache for Spotify API requests to avoid rate limiting.</param>
    /// <param name="spotifySubject">Subject for notifying about Spotify events.</param>
    /// <param name="queueService">Service for managing the track queue.</param>
    /// <param name="freeLyricsProvider">Service for fetching lyrics from free providers.</param>
    /// <param name="logger">Logger for logging events and errors.</param>
    public SpotifyService(
        IOptions<SpotifyOptions> spotifyOptions,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache,
        SpotifyRequestCache requestCache,
        ISpotifySubject spotifySubject,
        QueueService queueService,
        FreeLyricsProviderService freeLyricsProvider,
        ILogger<SpotifyService> logger)
    {
        _spotifyOptions = spotifyOptions.Value;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _requestCache = requestCache;
        _spotifySubject = spotifySubject;
        _queueService = queueService;
        _freeLyricsProvider = freeLyricsProvider;
        _logger = logger;
    }

    // Defensive null check for HttpContext and Session
    private ISession Session
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                throw new InvalidOperationException("HttpContext is null. Ensure this service is used within a valid HTTP request scope.");
            var session = httpContext.Session;
            if (session == null)
                throw new InvalidOperationException("Session is null. Ensure session middleware is configured and enabled.");
            return session;
        }
    }

    /// <summary>
    /// Starts the Spotify authentication process by generating a login URL.
    /// </summary>
    /// <returns>The Spotify login URL that the user should be redirected to.</returns>
    /// <remarks>
    /// This initiates the OAuth 2.0 Authorization Code Flow which is recommended for 
    /// long-running applications where a user logs in once and the app maintains access.
    /// </remarks>
    public string StartAuth()
    {
        // Get configuration values from appsettings.json or user secrets
        var clientId = _spotifyOptions.ClientId!;
        // Determine redirect URI: use configured default or dynamic host if accessed via domain/tunnel
        var redirectUri = _spotifyOptions.RedirectUri ?? "http://127.0.0.1:5001/callback";
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Request?.Host.HasValue == true)
        {
            var host = httpContext.Request.Host.Value;
            var scheme = httpContext.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? httpContext.Request.Scheme;
            if (!host.StartsWith("127.0.0.1") && !host.StartsWith("localhost"))
            {
                redirectUri = $"{scheme}://{host}/callback";
            }
        }
        Session.SetString("SpotifyRedirectUri", redirectUri);
        
        // Generate a unique state to prevent CSRF attacks
        var state = Guid.NewGuid().ToString("N");
        Session.SetString("SpotifyState", state);

        // Generate PKCE code_verifier and S256 code_challenge
        var codeVerifier = PkceUtil.GenerateCodeVerifier();
        var codeChallenge = PkceUtil.GenerateCodeChallenge(codeVerifier);
        Session.SetString("SpotifyCodeVerifier", codeVerifier);

        // Create the authorization request with least-privilege scopes and PKCE S256 challenge
        var loginRequest = new LoginRequest(new Uri(redirectUri), clientId, LoginRequest.ResponseType.Code)
        {
            CodeChallengeMethod = "S256",
            CodeChallenge = codeChallenge,
            Scope = new[]
            {
                Scopes.UserReadCurrentlyPlaying,    // Allows reading the currently playing track
                Scopes.UserReadPlaybackState,       // Allows reading the playback state (volume, repeat, etc.)
                Scopes.UserModifyPlaybackState      // Allows controlling playback (play, pause, skip, etc.)
            },
            State = state
        };
        return loginRequest.ToUri().ToString();
    }

    /// <summary>
    /// Handles the callback from Spotify after user authorization, exchanging the authorization code for an access token via PKCE.
    /// </summary>
    /// <param name="code">The authorization code from Spotify.</param>
    /// <param name="state">The state parameter for CSRF protection.</param>
    /// <returns>True if authentication was successful, otherwise false.</returns>
    public async Task<bool> UpdateAuthAsync(string code, string state)      
    {
        // Verify state to prevent cross-site request forgery attacks
        var storedState = Session.GetString("SpotifyState");
        if (string.IsNullOrEmpty(storedState) || state != storedState)
        {
            return false;
        }

        var codeVerifier = Session.GetString("SpotifyCodeVerifier");
        if (string.IsNullOrEmpty(codeVerifier))
        {
            return false;
        }

        var redirectUri = Session.GetString("SpotifyRedirectUri") ?? _spotifyOptions.RedirectUri!;

        // State and verifier are single-use, remove them from session
        Session.Remove("SpotifyState");
        Session.Remove("SpotifyCodeVerifier");
        Session.Remove("SpotifyRedirectUri");

        // Exchange the authorization code for access and refresh tokens via PKCE (no client secret needed)
        var tokenResponse = await new OAuthClient().RequestToken(new PKCETokenRequest(
            _spotifyOptions.ClientId!,
            code,
            new Uri(redirectUri),
            codeVerifier
        ));

        // Store tokens and expiry date in the session for future use
        Session.SetString("SpotifyAccessToken", tokenResponse.AccessToken);
        Session.SetString("SpotifyRefreshToken", tokenResponse.RefreshToken ?? "");
        Session.SetString("SpotifyTokenExpiry", DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("o"));

        // Store the access token in IMemoryCache for background services
        _cache.Set("SpotifyAuthToken", tokenResponse.AccessToken, TimeSpan.FromHours(1));

        return true;
    }

    /// <summary>
    /// Gets a SpotifyClient instance for making API calls. Refreshes the access token if it's expired.
    /// </summary>
    /// <param name="accessTokenOverride">Optional access token to use instead of session/cached token. Used for background services.</param>
    /// <returns>A configured SpotifyClient, or null if authentication fails.</returns>
    /// <remarks>
    /// This is the main entry point for making Spotify API calls. It handles token refresh
    /// automatically so other methods don't need to worry about authentication details.
    /// </remarks>
    public async Task<SpotifyClient?> GetSpotifyClientAsync(string? accessTokenOverride = null)
    {
        string? accessToken = accessTokenOverride;
        if (string.IsNullOrEmpty(accessToken))
        {
            // Try session first (for HTTP context)
            try
            {
                accessToken = Session.GetString("SpotifyAccessToken");
            }
            catch (InvalidOperationException)
            {
                // If no HTTP context, fallback to IMemoryCache
                _cache.TryGetValue("SpotifyAuthToken", out accessToken);
            }
        }
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }
        // No refresh logic for background (cache) tokens, only for session tokens
        if (accessTokenOverride == null && IsTokenExpired() && !await RefreshTokenAsync())
        {
            return null;
        }
        return new SpotifyClient(accessToken!);
    }

    /// <summary>
    /// Checks if the current access token is expired or close to expiring.
    /// </summary>
    /// <returns>True if the token is expired or will expire within 5 minutes, otherwise false.</returns>
    /// <remarks>
    /// We check for expiration 5 minutes in advance to avoid edge cases where the token
    /// expires while making a request or between closely-timed requests.
    /// </remarks>
    private bool IsTokenExpired()
    {
        var tokenExpiryStr = Session.GetString("SpotifyTokenExpiry");
        // Check if the token expires within the next 5 minutes
        return DateTime.TryParse(tokenExpiryStr, out var tokenExpiry) && tokenExpiry <= DateTime.UtcNow.AddMinutes(5);
    }

    /// <summary>
    /// Refreshes the Spotify access token using the refresh token.
    /// </summary>
    /// <returns>True if the token was refreshed successfully, otherwise false.</returns>
    /// <remarks>
    /// The OAuth 2.0 flow allows refreshing an expired access token without requiring the user to log in again.
    /// This method is called automatically by GetSpotifyClientAsync when needed.
    /// </remarks>
    private async Task<bool> RefreshTokenAsync()
    {
        var refreshToken = Session.GetString("SpotifyRefreshToken");
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        // Request a new access token using the refresh token via PKCE (no client secret needed)
        var newResponse = await new OAuthClient().RequestToken(new PKCETokenRefreshRequest(
            _spotifyOptions.ClientId!,
            refreshToken
        ));

        // Update the session with the new tokens
        Session.SetString("SpotifyAccessToken", newResponse.AccessToken);
        Session.SetString("SpotifyTokenExpiry", DateTime.UtcNow.AddSeconds(newResponse.ExpiresIn).ToString("o"));
        
        // A new refresh token might be issued, so we update it if available
        // This is not always provided but should be saved when it is
        if (!string.IsNullOrEmpty(newResponse.RefreshToken))
        {
            Session.SetString("SpotifyRefreshToken", newResponse.RefreshToken);
        }
        return true;
    }

    /// <summary>
    /// Checks if the user is currently authenticated with Spotify.
    /// </summary>
    /// <returns>True if an access token exists in the session, otherwise false.</returns>
    /// <remarks>
    /// Used to determine if the UI should show login options or authorized user features.
    /// </remarks>
    public bool IsAuthenticated() => !string.IsNullOrEmpty(Session.GetString("SpotifyAccessToken"));

    /// <summary>
    /// Clears all Spotify authentication data from the session.
    /// </summary>
    /// <remarks>
    /// Used for logging out or when authentication has failed irreparably.
    /// </remarks>
    public void ClearAuthentication()
    {
        Session.Remove("SpotifyAccessToken");
        Session.Remove("SpotifyRefreshToken");
        Session.Remove("SpotifyTokenExpiry");
        Session.Remove("SpotifyState");
        Session.Remove("SpotifyCodeVerifier");

        // Clear cached background auth token (AUD-005)
        _cache.Remove("SpotifyAuthToken");
    }

    /// <summary>
    /// Creates a custom Track object from a Spotify FullTrack, enriching it with lyrics and moderation info.
    /// </summary>
    /// <param name="fullTrack">The FullTrack object from the Spotify API.</param>
    /// <returns>A custom Track object with additional data.</returns>
    /// <remarks>
    /// This method enhances the basic track data from Spotify with lyrics and content moderation
    /// information, which is used for the content filtering feature of the application.
    /// </remarks>
    private static Track CreateTrackFromFullTrack(FullTrack fullTrack)
    {
        return new Track
        {
            FullTrack = fullTrack,
            IsFlagged = fullTrack.Explicit,
            Lyrics = null,
            MappedLyrics = null
        };
    }

    /// <summary>
    /// Gets the current playback information, including the currently playing track and the queue.
    /// Uses caching to avoid excessive API calls.
    /// </summary>
    /// <param name="spotify">The SpotifyClient instance.</param>
    /// <returns>A SpotifyPlaybackInfo object, or null if no track is playing.</returns>
    public async Task<SpotifyPlaybackInfo?> GetCurrentPlaybackInfoAsync(SpotifyClient spotify)
    {
        _cache.TryGetValue("SpotifyUserId", out string? userId);
        if (string.IsNullOrEmpty(userId))
        {
            var user = await CurrentUserAsync();
            userId = user?.Id;
        }
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }
        _cache.Set("SpotifyUserId", userId, TimeSpan.FromHours(1));

        var currentlyPlayingResponse = await _requestCache.GetCurrentlyPlayingAsync(spotify, userId);
        if (currentlyPlayingResponse?.Item is not FullTrack currentTrack) return null;

        var isQueueValid = await _queueService.ValidateQueueAsync(userId, spotify);
        if (!isQueueValid)
        {
            Log.Logger.Warning("SpotifyService: Queue validation failed for user {UserId}. Rebuilding queue.", userId);
            _queueService.InvalidateQueue(userId);
        }

        var queue = await _queueService.GetQueueAsync(userId, spotify);
        if (queue == null || queue.IsEmpty) return null;
        var playbackInfo = new SpotifyPlaybackInfo
        {
            CurrentlyPlaying = queue.CurrentTrack ?? CreateTrackFromFullTrack(currentTrack),
            RemainingTimeMs = currentTrack.DurationMs - (currentlyPlayingResponse.ProgressMs ?? 0),
            Queue = [.. queue.Tracks.Skip(1)], 
        };
        await _spotifySubject.NotifyPlaybackInfoAsync(playbackInfo);
        Log.Logger.Debug("Notified observers of playback change");

        return playbackInfo;
    }

    /// <summary>
    /// Skips the currently playing track if it is flagged as explicit or inappropriate.
    /// </summary>
    /// <param name="spotify">The SpotifyClient instance.</param>
    /// <returns>The Track that was skipped, or null if no track was skipped.</returns>
    /// <remarks>
    /// This method is used by the SpotifyQueueMonitorService to automatically skip
    /// inappropriate content, implementing the parental control feature.
    /// </remarks>
    public async Task<Track?> SkipIfFlaggedAsync(SpotifyClient spotify)
    {
        _cache.TryGetValue("SpotifyUserId", out string? userId);
        if (string.IsNullOrEmpty(userId))
        {
            var user = await CurrentUserAsync();
            userId = user?.Id;
        }
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var result = await _queueService.SkipFlaggedSongsAsync(userId, spotify);
        if (result.Skipped && result.TargetTrack != null)
        {
            Log.Logger.Information("SkipIfFlaggedAsync: Skipped flagged track: {TrackName} by {Artist}",
                result.TargetTrack.FullTrack?.Name ?? "Unknown",
                result.TargetTrack.FullTrack?.Artists?.FirstOrDefault()?.Name ?? "Unknown");
            await _spotifySubject.NotifyTrackSkippedEventAsync(result.TargetTrack);

            return result.TargetTrack;
        }
        else
        {
            Log.Logger.Information("SkipIfFlaggedAsync: No flagged track to skip.");
            return null;
        }
    }

    public async Task<PrivateUser?> CurrentUserAsync()
    {
        string? cacheKey = null;
        try
        {
            cacheKey = GetUserSpecificCacheKey("CurrentUserProfile");
            if (_cache.TryGetValue(cacheKey, out PrivateUser? cachedUser) && cachedUser != null)
            {
                return cachedUser;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve user cache key or cached profile");
        }

        SpotifyClient? client = await GetSpotifyClientAsync();
        if (client == null)
        {
            return null;
        }

        // Retry loop for transient SSL / transport stream EOF hiccups
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var user = await client.UserProfile.Current();
                if (user != null)
                {
                    if (cacheKey != null)
                    {
                        _cache.Set(cacheKey, user, TimeSpan.FromMinutes(30));
                    }
                    try
                    {
                        Session.SetString("SpotifyUserId", user.Id);
                    }
                    catch { }
                    return user;
                }
            }
            catch (Exception ex) when (attempt == 1)
            {
                _logger.LogWarning(ex, "Transient error fetching Spotify user profile on attempt {Attempt}. Retrying once...", attempt);
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Spotify user profile after {Attempt} attempts", attempt);
            }
        }

        // Fallback: If we have cached the user's ID in Session, create a minimal user so caller does not crash
        try
        {
            var storedUserId = Session.GetString("SpotifyUserId");
            if (!string.IsNullOrEmpty(storedUserId))
            {
                return new PrivateUser { Id = storedUserId, DisplayName = storedUserId };
            }
        }
        catch { }

        return null;
    }
    /// <summary>
    /// Creates a user-specific cache key using the session ID.
    /// </summary>
    /// <param name="baseKey">The base key for the cache entry.</param>
    /// <returns>A unique cache key for the current user.</returns>
    /// <remarks>
    /// This ensures that cached data is specific to each user session and prevents
    /// data leakage between different users of the application.
    /// </remarks>
    private string GetUserSpecificCacheKey(string baseKey)
    {
        return $"{baseKey}_{Session.Id}";
    }
}