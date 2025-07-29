using Alify.Controllers;
using Alify.Services;
using SpotifyAPI.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSignalR(); // Add SignalR support
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
builder.Services.AddSingleton<SpotifyService>(); // our main backend logic
builder.Services.AddHttpClient<LyricService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ArtistLyricService>();
builder.Services.AddSingleton(SpotifyClientConfig.CreateDefault());
builder.Services.AddScoped<SpotifyController>();
builder.Services.AddMemoryCache();

// Register singleton cache for Spotify API calls (changed from scoped to singleton)
builder.Services.AddSingleton<SpotifyRequestCache>();

// Register the background service for queue monitoring
builder.Services.AddSingleton<SpotifyQueueMonitorService>();
builder.Services.AddHostedService<SpotifyQueueMonitorService>(provider => 
    provider.GetRequiredService<SpotifyQueueMonitorService>());


var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); // enable session usage
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();

app.Run();
