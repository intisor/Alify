using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Aspire reads these via Environment.GetEnvironmentVariable before IConfiguration is available.
// The launch profile should supply them; these are fallbacks for VS/terminal runs that don't apply the profile.
// Aspire 13.x reads these directly from the process environment before IConfiguration is built.
// The launch profile should supply them; these are fallbacks when VS does not apply the profile.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:15286");

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT")))
    Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL")) &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL")))
    Environment.SetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL", "http://localhost:19043");

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL")))
    Environment.SetEnvironmentVariable("ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL", "http://localhost:20251");

var builder = DistributedApplication.CreateBuilder(args);

// AuxiliaryBackchannelService handles VS IDE integration. On machines with Windows
// networking issues (e.g. WSAENETDOWN / error 10050) it throws and — because the
// default BackgroundServiceExceptionBehavior is StopHost — kills the whole host before
// the DCP can spawn Spotify and Lyrics. Switching to Ignore keeps the DCP running so
// child projects still start; the only thing lost is VS run-control integration.
builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

// Pin Spotify to port 7236 so the OAuth redirect URI (http://127.0.0.1:7236/callback)
// continues to match the registered URI in the Spotify Developer portal when run via Aspire.
var spotify = builder.AddProject<Projects.Alify_Spotify>("spotify")
    .WithEndpoint("http", e => e.Port = 7236)
    .WithEnvironment("Spotify__RedirectUri", "http://127.0.0.1:7236/callback");

var lyrics = builder.AddProject<Projects.Alify_Lyrics>("lyrics")
    .WithEndpoint("http", e => e.Port = 5143);

builder.Build().Run();
