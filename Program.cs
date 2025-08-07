using Alify.Controllers;
using Alify.Core.Infrastructure.Doppler;
using Alify.Core.Models;
using Alify.Features.Spotify.Services;
using Alify.Features.Spotify.Events;
using Serilog;
using SpotifyAPI.Web;
using Alify.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Doppler integration: Read the token and add the custom configuration provider.
// The token can come from user secrets, environment variables, or launchSettings.json.
var dopplerToken = builder.Configuration["DOPPLER_TOKEN"];
builder.Configuration.AddDoppler(dopplerToken);

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddOutputCache();

// Configure HttpLogging for automatic HTTP request/response logging
builder.Services.AddHttpLogging(options =>
{
    // Log request and response details for debugging external APIs
    options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestHeaders |
                           Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseHeaders;

    // Security: Exclude sensitive headers to prevent API key exposure
    options.RequestHeaders.Add("User-Agent");
    options.RequestHeaders.Add("Accept");
    options.RequestHeaders.Add("Content-Type");
    // Explicitly exclude Authorization headers to protect API keys
    
    options.ResponseHeaders.Add("Content-Type");
    options.ResponseHeaders.Add("Cache-Control");
    
    // Exclude request/response bodies initially (can enable later for specific debugging)
    // This prevents large payloads and sensitive data from being logged
    
    // Set appropriate log level (Information for development, Warning for production)
    options.CombineLogs = true; // Combine request/response in single log entry
});

//builder.Services.AddSignalR(); // Add SignalR support
builder.Services.AddResponseCompression(options =>
{
	options.EnableForHttps = true;
});
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromMinutes(30); // Increased timeout for better UX
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
	options.Cookie.SameSite = SameSiteMode.Lax; // Better for OAuth flows
});
builder.Services.AddHttpClient(); // for making HTTP requests (lyrics, AI, etc.)
builder.Services.Configure<ApiKeys>(builder.Configuration.GetSection("ApiKeys"));
builder.Services.Configure<SpotifyOptions>(builder.Configuration.GetSection("Spotify"));
builder.Services.AddSingleton<SpotifyService>();
builder.Services.AddHttpClient<LyricService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ArtistLyricService>();
builder.Services.AddSingleton(SpotifyClientConfig.CreateDefault());
builder.Services.AddScoped<SpotifyController>();
builder.Services.AddMemoryCache();

// Add SSE and Spotify services
builder.Services.AddSingleton<SseService>(); 
builder.Services.AddSingleton<ISseService>(sp => sp.GetRequiredService<SseService>());
builder.Services.AddSingleton<ISpotifySubject>(sp => sp.GetRequiredService<SseService>());
builder.Services.AddSingleton<SpotifyRequestCache>();

// Register the unified Spotify playback monitoring service (replaces both SpotifyBackgroundService and SpotifyQueueMonitorService)
builder.Services.AddSingleton<SpotifyPlaybackMonitorService>();
builder.Services.AddHostedService<SpotifyPlaybackMonitorService>(sp => sp.GetRequiredService<SpotifyPlaybackMonitorService>());

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
	app.UseResponseCompression();
}

// Add HttpLogging middleware early in the pipeline to capture all HTTP traffic
app.UseHttpLogging();

app.UseOutputCache();

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); // enable session usage
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();

// Add graceful shutdown handling for SSE connections
app.Lifetime.ApplicationStopping.Register(() =>
{
    var sseService = app.Services.GetService<ISseService>();
    sseService?.Cleanup();
});

app.Run();
