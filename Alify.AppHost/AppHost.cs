var builder = DistributedApplication.CreateBuilder(args);

// Pin Spotify to port 7236 so the OAuth redirect URI (http://127.0.0.1:7236/callback)
// continues to match the registered URI in the Spotify Developer portal when run via Aspire.
var spotify = builder.AddProject<Projects.Alify_Spotify>("spotify")
    .WithHttpEndpoint(port: 7236, name: "http")
    .WithEnvironment("Spotify__RedirectUri", "http://127.0.0.1:7236/callback");

var lyrics = builder.AddProject<Projects.Alify_Lyrics>("lyrics")
    .WithHttpEndpoint(port: 5143, name: "http");

builder.Build().Run();
