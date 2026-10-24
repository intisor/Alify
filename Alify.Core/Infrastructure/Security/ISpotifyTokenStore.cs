namespace Alify.Core.Infrastructure.Security;

/// <summary>
/// Persistent authentication data for Spotify OAuth 2.0 PKCE.
/// </summary>
public record SpotifyAuthData
{
    public string? ClientId { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public string? UserId { get; init; }
    public string? Scope { get; init; }
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    public bool IsExpired => DateTime.UtcNow.AddMinutes(5) >= ExpiresAtUtc;
}

/// <summary>
/// Contract for persisting encrypted Spotify tokens locally across app reboots and browser refreshes.
/// </summary>
public interface ISpotifyTokenStore
{
    Task SaveTokensAsync(SpotifyAuthData data, CancellationToken cancellationToken = default);
    Task<SpotifyAuthData?> GetTokensAsync(CancellationToken cancellationToken = default);
    Task ClearTokensAsync(CancellationToken cancellationToken = default);
}
