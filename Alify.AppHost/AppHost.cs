// Aspire reads these via Environment.GetEnvironmentVariable before IConfiguration is available.
// The launch profile should supply them; these are fallbacks for VS/terminal runs that don't apply the profile.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:15286");
    Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
}

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL")) &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL")))
    Environment.SetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL", "http://localhost:19043");

var builder = DistributedApplication.CreateBuilder(args);

// Pin Spotify to port 7236 so the OAuth redirect URI (http://127.0.0.1:7236/callback)
// continues to match the registered URI in the Spotify Developer portal when run via Aspire.
var spotify = builder.AddProject<Projects.Alify_Spotify>("spotify")
    .WithEndpoint("http", e => e.Port = 7236)
    .WithEnvironment("Spotify__RedirectUri", "http://127.0.0.1:7236/callback");

var lyrics = builder.AddProject<Projects.Alify_Lyrics>("lyrics")
    .WithEndpoint("http", e => e.Port = 5143);

builder.Build().Run();
