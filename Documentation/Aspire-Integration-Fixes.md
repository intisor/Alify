# Aspire Integration — Bug Report & Fix Log

**Date:** 2025-07-14  
**Branch:** `feature/episode-support-and-enhancements`  
**Affected projects:** `Alify.AppHost`, `Alify.Spotify`, `Alify.Lyrics`

---

## Background

The solution already had the scaffolding for .NET Aspire in place:

- `Alify.AppHost` — the Aspire orchestration entry-point  
- `Alify.ServiceDefaults` — shared OpenTelemetry, health-check and resilience defaults  
- Both `Alify.Spotify` and `Alify.Lyrics` already called `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`

Despite this, running the AppHost produced **no logs in the Aspire dashboard**, the **OAuth callback broke** when started through Aspire, and the **Lyrics HTTP client** silently ignored its SSL/connection configuration. Three distinct root causes were identified.

---

## Bug 1 — Serilog replaced the entire `ILoggerFactory`, wiping out OpenTelemetry

### Files
- `Alify.Spotify\Program.cs`
- `Alify.Lyrics\Program.cs`

### Root Cause

`builder.AddServiceDefaults()` registers an OpenTelemetry `ILoggerProvider` through `builder.Logging.AddOpenTelemetry(...)`. This is the provider the Aspire dashboard reads from via the OTLP exporter.

The subsequent call to `builder.Host.UseSerilog()` does **not** add Serilog alongside the existing providers — it installs a `SerilogLoggerFactory` that **replaces** the whole `ILoggerFactory`. The OpenTelemetry provider that was registered by `AddServiceDefaults()` is discarded at that point. From the Aspire dashboard's perspective, the services emitted no structured log data at all.

```
AddServiceDefaults()     → registers OTel ILoggerProvider  ✓
builder.Host.UseSerilog() → replaces ILoggerFactory entirely ✗
                             └─ OTel provider is gone
                             └─ Aspire dashboard sees no logs
```

### Fix

Replace `builder.Host.UseSerilog()` with `builder.Logging.AddSerilog()`.  
`AddSerilog` registers Serilog as **one of many** `ILoggerProvider` implementations inside the standard Microsoft `ILoggerFactory`. The factory fans out to all registered providers, so both Serilog (console) and OpenTelemetry (Aspire dashboard) receive every log record.

```csharp
// ❌ Before — UseSerilog() installs SerilogLoggerFactory, replacing ILoggerFactory
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// ✅ After — AddSerilog() registers alongside the OTel provider already added by AddServiceDefaults()
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.AddSerilog(Log.Logger, dispose: true);
```

**Result:** Aspire dashboard now receives all structured log events from both services in real time. Console output from Serilog is unchanged.

---

## Bug 2 — Duplicate `AddHttpClient<LyricService>` silently discarded the handler configuration

### File
- `Alify.Lyrics\Program.cs`

### Root Cause

`AddHttpClient<T>()` registers a *named* typed client where the name is derived from the type (`LyricService`). Calling it twice on the same type results in two registrations under the same name. The **last** registration wins in the DI pipeline.

The first call configured a `SocketsHttpHandler` with explicit TLS settings and a `PooledConnectionLifetime`. The second bare call registered a default handler with no configuration and silently overwrote the first:

```csharp
// First registration — correct SSL and pooling configuration
builder.Services.AddHttpClient<LyricService>((serviceProvider, client) =>
{
    var apiKeys = serviceProvider.GetRequiredService<IOptions<ApiKeys>>().Value;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    SslOptions = new SslClientAuthenticationOptions
    {
        EnabledSslProtocols = SslProtocols.None  // let OS negotiate best protocol
    },
    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
});

// ❌ Second registration — same type name, default handler, overwrites the above
builder.Services.AddHttpClient<LyricService>();
```

At runtime `LyricService` received an `HttpClient` backed by the unconfigured default handler. The `PooledConnectionLifetime` and the TLS negotiation settings were never applied.

### Fix

Remove the second `AddHttpClient<LyricService>()` call entirely. The typed client only needs one registration.

```csharp
// ✅ After — single registration, handler configuration is preserved
builder.Services.AddHttpClient<LyricService>((serviceProvider, client) =>
{
    var apiKeys = serviceProvider.GetRequiredService<IOptions<ApiKeys>>().Value;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    SslOptions = new SslClientAuthenticationOptions
    {
        EnabledSslProtocols = SslProtocols.None
    },
    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
});
```

---

## Bug 3 — AppHost did not pin ports, breaking Spotify OAuth when launched through Aspire

### File
- `Alify.AppHost\AppHost.cs`

### Root Cause

When Aspire orchestrates a project it sets `ASPNETCORE_URLS` to an Aspire-managed address with a **randomly assigned port** (unless told otherwise). The Spotify OAuth flow requires the redirect URI sent to Spotify's authorisation server to exactly match a pre-registered URI in the Spotify Developer portal:

```
http://127.0.0.1:7236/callback
```

Without a pinned port, Aspire assigned a different port on every run. The authorisation redirect arrived at the correct host but the wrong port, so Spotify rejected the callback with a `INVALID_CLIENT: Invalid redirect URI` error.

The original `AppHost.cs` also left the `spotify` and `lyrics` resource variables completely unused after declaring them, providing no configuration whatsoever:

```csharp
// ❌ Before — variables declared but no endpoint or environment configuration
var spotify = builder.AddProject<Projects.Alify_Spotify>("spotify");
var lyrics  = builder.AddProject<Projects.Alify_Lyrics>("lyrics");
```

### Fix

Pin both services to their existing development ports using `WithHttpEndpoint`. For Spotify, additionally inject the redirect URI as an environment variable so the value is consistent regardless of how the project is started (directly or through Aspire):

```csharp
// ✅ After
var spotify = builder.AddProject<Projects.Alify_Spotify>("spotify")
    .WithHttpEndpoint(port: 7236, name: "http")
    .WithEnvironment("Spotify__RedirectUri", "http://127.0.0.1:7236/callback");

var lyrics = builder.AddProject<Projects.Alify_Lyrics>("lyrics")
    .WithHttpEndpoint(port: 5143, name: "http");
```

**Result:** Aspire always launches Spotify on port 7236. The redirect URI environment variable overrides `appsettings.json` at runtime and matches the registered value in the Spotify Developer portal exactly.

---

## Summary Table

| # | File | Problem | Impact | Fix |
|---|------|---------|--------|-----|
| 1 | `Alify.Spotify\Program.cs`<br>`Alify.Lyrics\Program.cs` | `UseSerilog()` replaced `ILoggerFactory`, discarding the OTel provider | Aspire dashboard showed no logs from either service | Use `builder.Logging.AddSerilog(Log.Logger, dispose: true)` |
| 2 | `Alify.Lyrics\Program.cs` | Second `AddHttpClient<LyricService>()` overrode handler config | `SocketsHttpHandler` SSL/pooling settings silently ignored at runtime | Remove the duplicate bare registration |
| 3 | `Alify.AppHost\AppHost.cs` | No port pinning; Aspire assigned a random port per run | Spotify OAuth callback rejected due to redirect URI mismatch | `WithHttpEndpoint(port: 7236)` + `WithEnvironment(...)` for redirect URI |

---

## How to verify the fixes

1. Set **`Alify.AppHost`** as the startup project.
2. Run (`F5` or `dotnet run`). The Aspire dashboard URL is printed to the console.
3. Open the dashboard. Under **Resources** both `spotify` and `lyrics` should show a green *Running* state.
4. Navigate to **Console Logs** — structured log entries from `ILogger<T>` appear in real time.
5. Navigate to **Traces** — ASP.NET Core request traces and any custom `ActivitySource` spans are visible.
6. Trigger a Spotify login. The OAuth callback should complete without a redirect URI error.
