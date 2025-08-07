using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Alify.Core.Infrastructure.Doppler;

public class DopplerConfigurationProvider : ConfigurationProvider
{
    private readonly string? _dopplerToken;
    private readonly ILogger<DopplerConfigurationProvider>? _logger;
    private const string _dopplerApiUrl = "https://api.doppler.com/v3/configs/config/secrets/download?format=json";
    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

    public DopplerConfigurationProvider(string? dopplerToken, ILogger<DopplerConfigurationProvider>? logger = null)
    {
        _dopplerToken = dopplerToken;
        _logger = logger;
    }

    public override void Load()
    {
        if (string.IsNullOrEmpty(_dopplerToken))
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var cacheKey = $"DopplerSecrets_{_dopplerToken}";

        Data = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _dopplerToken);

                var response = client.GetAsync(_dopplerApiUrl).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return secrets?.ToDictionary(
                    k => k.Key.Replace("__", ":"),
                    v => v.Value,
                    StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading secrets from Doppler. Returning empty configuration.");
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        });
    }
}

public class DopplerConfigurationSource : IConfigurationSource
{
    private readonly string? _dopplerToken;
    private readonly ILogger<DopplerConfigurationProvider>? _logger;

    public DopplerConfigurationSource(string? dopplerToken, ILogger<DopplerConfigurationProvider>? logger = null)
    {
        _dopplerToken = dopplerToken;
        _logger = logger;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DopplerConfigurationProvider(_dopplerToken, _logger);
    }
}
