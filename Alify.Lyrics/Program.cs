using Alify.Core.Infrastructure.Doppler;
using Alify.Core.Models;
using Alify.Core.Services;
using Alify.Services;
using Microsoft.Extensions.Options;
using Serilog;
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
    options.Conventions.AddPageRoute("/Lyrics/LyricsView", "/LyricsView");
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

builder.Services.AddHttpClient();
builder.Services.Configure<ApiKeys>(builder.Configuration.GetSection("ApiKeys"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ArtistLyricService>();

// Configure a dedicated HttpClient for LyricService with modern SSL protocols
builder.Services.AddHttpClient<LyricService>((serviceProvider, client) =>
{
    var apiKeys = serviceProvider.GetRequiredService<IOptions<ApiKeys>>().Value;
    // You can set base addresses or default headers here if needed
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

// Keep existing LyricService registration for Genius scraping
builder.Services.AddHttpClient<LyricService>();

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

app.Run();
