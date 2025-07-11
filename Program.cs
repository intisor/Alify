using Alify.Controllers;
using Alify.Services;
using SpotifyAPI.Web;
using System.Net;
using System.Net.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromMinutes(17);
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
});
builder.Services.AddHttpClient(); // for making HTTP requests (lyrics, AI, etc.)
builder.Services.AddSingleton<SpotifyService>(); // our main backend logic
builder.Services.AddHttpClient<LyricService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ArtistLyricService>();
builder.Services.AddSingleton(SpotifyClientConfig.CreateDefault());
builder.Services.AddScoped<SpotifyController>();
builder.Services.AddMemoryCache();

// Register singleton cache for Spotify API calls (changed from scoped to singleton)
builder.Services.AddSingleton<SpotifyRequestCache>();

// Register the background service
builder.Services.AddSingleton<SpotifyQueueMonitorService>();
builder.Services.AddHostedService<SpotifyQueueMonitorService>(provider => 
    provider.GetRequiredService<SpotifyQueueMonitorService>());

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); // enable session usage
app.MapControllers();
app.UseAuthorization();
app.MapRazorPages();
app.MapGet("/Index", (IConfiguration config, HttpContext context) =>
{
	var loginRequest = new LoginRequest(
		new Uri(config["Spotify:RedirectUri"]),
		config["Spotify:ClientId"],
		LoginRequest.ResponseType.Code
	)
	{
		Scope = new List<string> {
			Scopes.PlaylistModifyPrivate,
			Scopes.PlaylistModifyPublic,
			Scopes.UserReadCurrentlyPlaying,
			Scopes.UserReadPlaybackState,
			Scopes.UserModifyPlaybackState
		}
	};

	var loginUri = loginRequest.ToUri();
	context.Response.Redirect(loginUri.ToString());
	return Task.CompletedTask;
});

app.Run();
