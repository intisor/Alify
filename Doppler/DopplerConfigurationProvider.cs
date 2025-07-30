using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Alify.Doppler;

public class DopplerConfigurationProvider : ConfigurationProvider
{
    private readonly string? _dopplerToken;
    private const string DopplerApiUrl = "https://api.doppler.com/v3/configs/config/secrets/download?format=json";
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public DopplerConfigurationProvider(string? dopplerToken)
    {
        _dopplerToken = dopplerToken;
    }

    public override void Load()
    {
        if (string.IsNullOrEmpty(_dopplerToken))
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var cacheKey = $"DopplerSecrets_{_dopplerToken}";

        Data = Cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _dopplerToken);

                var response = client.GetAsync(DopplerApiUrl).GetAwaiter().GetResult();
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
                Console.WriteLine($"Error loading secrets from Doppler: {ex.Message}. Returning empty configuration.");
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        });
    }
}

public class DopplerConfigurationSource : IConfigurationSource
{
    private readonly string? _dopplerToken;

    public DopplerConfigurationSource(string? dopplerToken)
    {
        _dopplerToken = dopplerToken;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DopplerConfigurationProvider(_dopplerToken);
    }
}
