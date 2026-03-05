using Alify.Core.Infrastructure.Doppler;
using Alify.Core.Models;
using Alify.Core.Services;
using Alify.Services;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Serilog;
using System.Security.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (OpenTelemetry, health checks, resilience)
builder.AddServiceDefaults();

// Configure Serilog as an additional provider so OpenTelemetry (Aspire dashboard) is preserved.
// UseSerilog() replaces the entire ILoggerFactory (wiping OTel); AddSerilog() adds it alongside.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.AddSerilog(Log.Logger, dispose: true);

var dopplerToken = builder.Configuration["DOPPLER_TOKEN"];
builder.Configuration.AddDoppler(dopplerToken);

builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Pages";
    options.Conventions.AddPageRoute("/Lyrics/LyricsView", "/LyricsView");
});

builder.Services.AddControllers();
builder.Services.AddOutputCache();

// Feature flags — reads from IConfiguration["FeatureManagement:{FlagName}"].
// appsettings.json: all new Release A/B flags default to false (safe deploy).
// appsettings.Development.json: all flags true (develop everything locally).
builder.Services.AddFeatureManagement();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestHeaders |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseHeaders;

    options.RequestHeaders.Add("User-Agent");
    options.RequestHeaders.Add("Accept");
    options.RequestHeaders.Add("Content-Type");
    options.ResponseHeaders.Add("Cache-Control");
    options.CombineLogs = true;
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddHttpClient();
builder.Services.Configure<ApiKeys>(builder.Configuration.GetSection("ApiKeys"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ArtistLyricService>();

// Configure a dedicated HttpClient for LyricService with modern SSL protocols
// LyricService now handles BOTH lyrics fetching AND search (Issue #2) - no duplication!
builder.Services.AddHttpClient<LyricService>((serviceProvider, client) =>
{
    var apiKeys = serviceProvider.GetRequiredService<IOptions<ApiKeys>>().Value;
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.None
        },
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseResponseCompression();
}

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Incoming request: {Method} {Path}", context.Request.Method, context.Request.Path);
    await next();
    logger.LogInformation("Response status: {StatusCode}", context.Response.StatusCode);
});

app.UseHttpLogging();
app.UseOutputCache();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();

// Map Aspire health check endpoints
app.MapDefaultEndpoints();

app.Run();
