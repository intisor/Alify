using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Alify.Core.Infrastructure.Security;

/// <summary>
/// Encrypted file-backed token store using ASP.NET Core Data Protection (Windows DPAPI / local key ring).
/// Stores long-lived Spotify tokens safely encrypted at rest across browser refreshes and app restarts.
/// </summary>
public class ProtectedFileSpotifyTokenStore : ISpotifyTokenStore
{
    private readonly IDataProtector _protector;
    private readonly string _filePath;
    private readonly ILogger<ProtectedFileSpotifyTokenStore>? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ProtectedFileSpotifyTokenStore(
        IDataProtectionProvider protectionProvider,
        string? customFilePath = null,
        ILogger<ProtectedFileSpotifyTokenStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(protectionProvider);
        _protector = protectionProvider.CreateProtector("Alify.Spotify.TokenStore.v1");
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _filePath = customFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _filePath = Path.Combine(appData, "Alify", "spotify_auth.dat");
        }
    }

    public async Task SaveTokensAsync(SpotifyAuthData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(data);
            var protectedData = _protector.Protect(json);

            var tempPath = $"{_filePath}.tmp";
            await File.WriteAllTextAsync(tempPath, protectedData, cancellationToken);
            File.Move(tempPath, _filePath, overwrite: true);

            _logger?.LogInformation("Spotify tokens successfully saved and encrypted at rest: {FilePath}", _filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to encrypt and save Spotify auth tokens to {FilePath}", _filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SpotifyAuthData?> GetTokensAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var protectedData = await File.ReadAllTextAsync(_filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(protectedData))
            {
                return null;
            }

            var decryptedJson = _protector.Unprotect(protectedData);
            var data = JsonSerializer.Deserialize<SpotifyAuthData>(decryptedJson);
            return data;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to decrypt stored Spotify tokens from {FilePath}. The file may be corrupt or encrypted with old keys.", _filePath);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearTokensAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
                _logger?.LogInformation("Stored Spotify auth tokens cleared from {FilePath}", _filePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
