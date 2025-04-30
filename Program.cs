using Alify.Services;
using SpotifyAPI.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
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
builder.Configuration.AddJsonFile("appsettings.Development.json");

builder.Services.AddSingleton(provider =>
{
	var config = builder.Configuration;
	return SpotifyClientConfig
		.CreateDefault()
		.WithAuthenticator(new ClientCredentialsAuthenticator(
			config["Spotify:ClientId"],
			config["Spotify:ClientSecret"]
		));
});

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); // enable session usage
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
			Scopes.UserReadPlaybackState
		}
	};

	var loginUri = loginRequest.ToUri();
	context.Response.Redirect(loginUri.ToString());
	return Task.CompletedTask;
});

app.Run();
