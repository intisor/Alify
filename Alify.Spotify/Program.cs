using Alify.Controllers;
using Alify.Core.Infrastructure.Doppler;
using Alify.Core.Models;
using Alify.Core.Services;
using Alify.Features.Spotify.Events;
using Alify.Features.Spotify.Services;
using Alify.Services;
using Microsoft.Extensions.Options;
using Serilog;
using SpotifyAPI.Web;
using System.Security.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var dopplerToken = builder.Configuration["DOPPLER_TOKEN"];
builder.Configuration.AddDoppler(dopplerToken);

builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Pages";
    options.Conventions.AddPageRoute("/Authentication/callback", "/callback");
    options.Conventions.AddPageRoute("/Dashboard/Dashboard", "/Dashboard");
    options.Conventions.AddPageRoute("/Spotify/SpotifyEvents", "/SpotifyEvents");
});

builder.Services.AddControllers();
builder.Services.AddOutputCache();

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

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddHttpClient();
builder.Services.Configure<ApiKeys>(builder.Configuration.GetSection("ApiKeys"));
builder.Services.Configure<SpotifyOptions>(builder.Configuration.GetSection("Spotify"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(SpotifyClientConfig.CreateDefault());
builder.Services.AddScoped<SpotifyController>();
builder.Services.AddMemoryCache();

// Add core services
builder.Services.AddHttpClient<FreeLyricsProviderService>();
builder.Services.AddSingleton<FreeLyricsProviderService>();
builder.Services.AddSingleton<Alify.Core.Services.ArtistLyricService>();

// Add SSE and Spotify services
builder.Services.AddSingleton<QueueService>();
builder.Services.AddSingleton<SseService>();
builder.Services.AddSingleton<ISseService>(sp => sp.GetRequiredService<SseService>());
builder.Services.AddSingleton<ISpotifySubject>(sp => sp.GetRequiredService<SseService>());
builder.Services.AddSingleton<SpotifyRequestCache>();
builder.Services.AddScoped<SpotifyService>(); // Changed from Singleton to Scoped for HttpContext access

// Register the unified Spotify playback monitoring service
builder.Services.AddSingleton<SpotifyPlaybackMonitorService>();
builder.Services.AddHostedService<SpotifyPlaybackMonitorService>(sp => sp.GetRequiredService<SpotifyPlaybackMonitorService>());
builder.Services.Configure<PlaybackMonitorOptions>(options => {
    options.PostSkipCheckDelayMs = 800;
    options.ActivePlaybackPollingIntervalMs = 4000;
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
app.UseSession();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();

app.Lifetime.ApplicationStopping.Register(() =>
{
    var sseService = app.Services.GetService<ISseService>();
    sseService?.Cleanup();
});

app.Run();
