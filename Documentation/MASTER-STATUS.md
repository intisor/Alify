# Alify — Master Status & Comprehensive Documentation

> **Last Updated:** 2025  
> **Solution Version:** .NET 10 / C# 14  
> **Status:** Active Development — Pre-Database Phase

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Solution Architecture](#2-solution-architecture)
3. [Technology Stack](#3-technology-stack)
4. [Project Structure Deep-Dive](#4-project-structure-deep-dive)
5. [Feature Status Matrix](#5-feature-status-matrix)
6. [What Is Fully Implemented](#6-what-is-fully-implemented)
7. [What Is Partially Implemented or Off](#7-what-is-partially-implemented-or-off)
8. [What Is Not Yet Implemented (Roadmap)](#8-what-is-not-yet-implemented-roadmap)
9. [Database Strategy](#9-database-strategy)
10. [API & Configuration Reference](#10-api--configuration-reference)
11. [Infrastructure & DevOps](#11-infrastructure--devops)
12. [Security Model](#12-security-model)
13. [Performance Architecture](#13-performance-architecture)
14. [Known Issues & Technical Debt](#14-known-issues--technical-debt)
15. [Development Setup Guide](#15-development-setup-guide)

---

## 1. Project Overview

**Alify** is a music-centric .NET 10 web application that supercharges the Spotify listening experience. It provides real-time playback monitoring, AI-powered content moderation, intelligent lyrics fetching and parsing, a WhatsApp-style chat lyrics view, podcast episode management, and a sleep timer — features that Spotify natively does poorly or has removed entirely.

The long-term vision (documented in the Widgets PRD) is to evolve Alify from a web portal users must open into an **always-present, embeddable widget suite** — usable in OBS Browser Sources, MAUI desktop overlays, browser PWAs, and iframes.

### What Makes Alify Different

| Gap in Spotify | Alify's Answer |
|---|---|
| No real-time lyrics sync | Genius scraping + ArtistLyricService parser |
| Weak/binary explicit filter | Multi-provider AI moderation (Gemini → OpenRouter → Mistral) |
| Broken native sleep timer | Server-managed SleepTimerService with SSE notification |
| No multi-artist lyric attribution | State-machine parser with per-artist line mapping |
| Removed "New Episodes" feed | EpisodeController restores it via Spotify API |
| No content-aware auto-skip | SpotifyPlaybackMonitorService + SkipIfFlaggedAsync() |
| No embeddable "now playing" widget | Planned Blazor Server widget system (Phase 1 in roadmap) |

---

## 2. Solution Architecture

Alify is a **.NET Aspire-orchestrated microservices solution** containing five projects:

```
Alify.sln
├── Alify.AppHost          ← .NET Aspire orchestration host (ports, telemetry, health)
├── Alify.ServiceDefaults  ← Shared OpenTelemetry, health-checks, resilience
├── Alify.Core             ← Domain models, infrastructure services, shared utilities
├── Alify.Spotify          ← Spotify integration: auth, playback, moderation, SSE
└── Alify.Lyrics           ← Lyrics service: Genius API, scraping, AI moderation, chat view
```

> `Geniefy/` directory exists in the repo root but is currently **empty** (reserved for future use).

### Service Port Map

| Service | Port | Purpose |
|---|---|---|
| `Alify.Spotify` | `7236` | Main Spotify app (pinned for OAuth redirect URI) |
| `Alify.Lyrics` | `5143` | Lyrics microservice |
| `Alify.AppHost` | `15286` | Aspire orchestration host |
| Aspire Dashboard OTLP | `19043` | OpenTelemetry collector |
| Aspire Resource Service | `20251` | Resource management |

### Dependency Graph

```
Alify.AppHost
    └── references → Alify.Spotify, Alify.Lyrics

Alify.Spotify
    └── references → Alify.Core

Alify.Lyrics
    └── references → Alify.Core

Alify.Core
    └── (no project references — pure foundation layer)
```

---

## 3. Technology Stack

| Layer | Technology | Version |
|---|---|---|
| **Framework** | .NET / ASP.NET Core | 10.0 |
| **Language** | C# | 14 |
| **Web UI** | Razor Pages | — |
| **Orchestration** | .NET Aspire | 13.x |
| **Logging** | Serilog + ASP.NET Core HTTP Logging | 4.3.0 / 8.0.0 |
| **Observability** | OpenTelemetry (OTLP exporter) | 1.14.0 |
| **Caching** | `IMemoryCache` | in-process, no Redis yet |
| **Secrets Management** | Doppler + .NET User Secrets | — |
| **Configuration** | Doppler provider → User Secrets → appsettings.json | — |
| **Feature Flags** | `Microsoft.FeatureManagement.AspNetCore` | 4.4.0 |
| **Spotify API** | `SpotifyAPI.Web` + `SpotifyAPI.Web.Auth` | 7.2.1 / 7.1.1 |
| **Lyrics Scraping** | `Microsoft.Playwright` (headless Chromium) | 1.44.0 |
| **HTML Parsing** | `HtmlAgilityPack` | 1.11.62 |
| **AI Moderation** | Gemini (`Mscc.GenerativeAI`) / OpenRouter / Mistral | 3.0.2 |
| **Background Jobs** | `IHostedService` + `TickerQ` + `TickerQ.Dashboard` | 2.3.0 |
| **Real-Time Events** | Server-Sent Events (SSE) — custom implementation | — |
| **Authentication** | OAuth 2.0 Authorization Code Flow (Spotify) | — |
| **Session** | ASP.NET Core Session + cookie storage | — |
| **Database** | **None yet** (see §9) | — |

---

## 4. Project Structure Deep-Dive

### 4.1 Alify.Core — Foundation Layer

```
Alify.Core/
├── Models/
│   ├── LyricModels.cs          ← LyricLine, LyricMapping, LyricsModerationResult
│   └── SearchResult.cs         ← Generic search result wrapper
├── Services/
│   └── ArtistLyricService.cs   ← Lyrics parser (state machine + regex)
├── Infrastructure/
│   ├── Doppler/
│   │   ├── DopplerConfigurationProvider.cs  ← Custom IConfigurationProvider
│   │   └── DopplerConfigurationExtensions.cs← AddDoppler() extension
│   ├── FeatureFlags/
│   │   └── FeatureFlags.cs     ← Compile-time flag name constants
│   └── Logging/
│       └── HighPerformanceLogging.cs  ← Source-generated LoggerMessage attributes
└── Extensions/
    └── MethodInjectionExtensions.cs   ← On-demand DI resolution helpers
```

**Key Highlights:**
- `LyricMapping` exposes `IReadOnlyList<LyricLine>` (immutability principle)
- `DopplerConfigurationProvider` caches secrets for 1 hour using a static `MemoryCache`
- `HighPerformanceLogging` uses C# 10 `[LoggerMessage]` source generators — zero heap allocations
- `FeatureFlags` constants prevent magic string bugs across pages and controllers

### 4.2 Alify.Lyrics — Lyrics Microservice

```
Alify.Lyrics/
├── Features/
│   └── Lyrics/
│       ├── Controllers/
│       │   └── LyricsSearchController.cs  ← REST API for search
│       └── Services/
│           ├── LyricService.cs            ← Genius API + AI moderation
│           └── PlaywrightLyricsScraper.cs ← Headless browser scraping
├── Pages/
│   ├── Lyrics/
│   │   ├── LyricsView.cshtml              ← WhatsApp-style chat UI
│   │   └── LyricsView.cshtml.cs           ← Page model (LyricsViewModel)
│   └── Shared/                            ← Layout, nav partial
├── featureflags.json                      ← Prod defaults (flags off)
└── featureflags.Development.json          ← Dev overrides (all on)
```

### 4.3 Alify.Spotify — Spotify Integration Service

```
Alify.Spotify/
├── Features/
│   ├── Authentication/
│   │   └── AuthController.cs              ← /auth/* endpoints
│   └── Spotify/
│       ├── Controllers/
│       │   ├── SpotifyController.cs       ← Core Spotify actions
│       │   ├── PlaybackController.cs      ← Queue, repeat, sleep timer API
│       │   └── EpisodeController.cs       ← Podcast/episode endpoints
│       ├── Events/
│       │   ├── ISpotifySubject.cs         ← Observer subject interface
│       │   ├── ISPotifyObserver.cs        ← Observer interface
│       │   └── ISseService.cs             ← SSE broadcaster interface
│       ├── Models/
│       │   └── RequestDtos.cs             ← AddToQueueRequest, SleepTimerRequest, etc.
│       └── Services/
│           ├── SpotifyService.cs          ← OAuth, playback, track/episode creation
│           ├── SpotifyPlaybackMonitorService.cs ← IHostedService background monitor
│           ├── QueueService.cs            ← Stateful queue management
│           ├── SpotifyRequestCache.cs     ← API rate-limit deduplication cache
│           ├── SseService.cs              ← SSE hub (subject + broadcaster)
│           ├── SleepTimerService.cs       ← Server-managed sleep timer
│           └── FreeLyricsProviderService.cs ← lyrics.ovh fallback
├── Pages/
│   ├── Authentication/callback.cshtml     ← OAuth redirect handler
│   ├── Dashboard/Dashboard.cshtml        ← Main app dashboard
│   └── Spotify/SpotifyEvents.cshtml      ← SSE event stream page
├── SpotifyModels.cs                      ← Playlist, Track, SpotifyPlaybackInfo, MusicQueue
├── featureflags.json                     ← Prod defaults
└── featureflags.Development.json         ← Dev overrides (all on)
```

### 4.4 Alify.AppHost — Aspire Orchestration

```
Alify.AppHost/
└── AppHost.cs   ← Pins ports, injects OAuth redirect URI, configures background service behavior
```

Notable config:
- `BackgroundServiceExceptionBehavior.Ignore` — prevents `AuxiliaryBackchannelService` network errors from killing the host on Windows
- `WithEndpoint("http", e => e.Port = 7236)` — OAuth URI stability
- `WithEnvironment("Spotify__RedirectUri", "http://127.0.0.1:7236/callback")` — overrides appsettings.json at runtime

---

## 5. Feature Status Matrix

| Feature | Module | Status | Feature Flag | Notes |
|---|---|---|---|---|
| Spotify OAuth 2.0 login | Alify.Spotify | ✅ Done | `EnableSpotifyIntegration` | State CSRF, 5-min token buffer, auto-refresh |
| Current playback info | Alify.Spotify | ✅ Done | `EnableSpotifyIntegration` | Polls every 4s via background service |
| Queue management | Alify.Spotify | ✅ Done | — | 11-min cache, FIFO, semaphore-protected |
| Real-time SSE events | Alify.Spotify | ✅ Done | — | Observer pattern, ConcurrentDictionary of clients |
| Sleep timer | Alify.Spotify | ✅ Done | — | Server-managed, SSE notification on expiry |
| Add to queue API | Alify.Spotify | ✅ Done | — | Tracks + episodes |
| Repeat mode control | Alify.Spotify | ✅ Done | — | track/context/off |
| Episode feed (new episodes) | Alify.Spotify | ✅ Done | — | Restores removed Spotify feature |
| Show/podcast browsing | Alify.Spotify | ✅ Done | — | Saved shows + per-show episodes |
| AI content moderation | Alify.Lyrics | ✅ Done | `EnableSpotifyModeration` | Gemini→OpenRouter→Mistral failover chain |
| Auto-skip flagged tracks | Alify.Spotify | ✅ Done | `EnableSmartSkip` | Via `SkipIfFlaggedAsync()` in monitor |
| Genius API lyrics fetch | Alify.Lyrics | ✅ Done | `EnableGeniusSearch` | Search + Playwright scrape |
| Playwright lyrics scraping | Alify.Lyrics | ✅ Done | — | 35 performance optimizations |
| ArtistLyricService parser | Alify.Core | ✅ Done | — | State machine, regex, per-artist/section mapping |
| WhatsApp-style lyrics chat view | Alify.Lyrics | ✅ Done | `EnableLyricsChat` | Full dark theme, message bubbles, artist avatars |
| lyrics.ovh free fallback | Alify.Spotify | ✅ Done | `EnableMultipleLyricsSources` | No auth needed |
| Feature flag system | Both | ✅ Done | — | `Microsoft.FeatureManagement` + JSON files |
| Doppler secrets integration | Both | ✅ Done | — | 1h cached, custom IConfigurationProvider |
| Aspire orchestration | AppHost | ✅ Done | — | Port pinning, OTel, health checks, SSE fix |
| Serilog + OTel co-existence | Both | ✅ Done | — | Fixed: `AddSerilog()` not `UseSerilog()` |
| High-performance logging | Alify.Core | ✅ Done | — | Source-generated `[LoggerMessage]` |
| Response compression | Both | ✅ Done | — | GZIP/Brotli, production only |
| Output caching middleware | Both | ✅ Done | — | Applied selectively on endpoints |
| HTTP logging middleware | Both | ✅ Done | — | Fields filtered, combined logs |
| Session management | Alify.Spotify | ✅ Done | — | 30-min idle, HttpOnly cookie, SameSite=Lax |
| Dashboard page | Alify.Spotify | ✅ Done | — | Playback info, monitoring toggle, lyrics analysis |
| Intelligent polling delays | Alify.Spotify | ✅ Done | — | Dynamic delay based on remaining track time |
| Method injection extensions | Alify.Core | ✅ Done | — | 7 patterns for on-demand service resolution |
| Lyrics video export | Alify.Lyrics | ❌ Not Yet | `EnableVideoExport` | Flag defined, no implementation |
| Blazor Server widgets | Both | ❌ Not Yet | — | Full PRD written, Phase 1 not started |
| IPlaybackSource abstraction | Alify.Core | ❌ Not Yet | — | Needed for YouTube Music Phase 4 |
| YouTube Music support | — | ❌ Not Yet | — | Phase 5 in roadmap |
| Database (any) | — | ❌ Not Yet | — | Highest priority gap — see §9 |
| Redis distributed cache | — | ❌ Not Yet | — | Required for horizontal scaling |
| User accounts / preferences | — | ❌ Not Yet | — | Blocked by database |
| Theme presets (saved) | — | ❌ Not Yet | — | Blocked by database |
| Session/playback history | — | ❌ Not Yet | — | Blocked by database |
| Rate limiting (per-user) | Both | ❌ Not Yet | — | Rely on cache as stop-gap |
| Content Security Policy | Both | ❌ Not Yet | — | CSP headers not configured |
| CORS policy | Both | ❌ Not Yet | — | No CORS configuration |
| Unit/integration tests | Both | ❌ Not Yet | — | `tests/` folder exists, empty |
| Swagger/OpenAPI docs | Both | ❌ Not Yet | — | No Swashbuckle registered |
| Docker image | — | ❌ Not Yet | — | Playwright needs extra libs in Dockerfile |
| Twitch/YouTube chat song requests | — | ❌ Not Yet | — | Phase 6, paid Pro add-on |
| Monetisation / Pro tier | — | ❌ Not Yet | — | $4.99/mo planned, not implemented |

---

## 6. What Is Fully Implemented

### 6.1 Spotify Authentication & Session

OAuth 2.0 Authorization Code Flow is fully working end-to-end:

- `StartAuth()` generates a Spotify login URL with a CSRF `state` parameter
- `UpdateAuthAsync()` exchanges the code for access + refresh tokens
- `GetSpotifyClientAsync()` automatically refreshes expired tokens (5-minute buffer)
- Tokens are stored in both **ASP.NET Core Session** (for HTTP-context requests) and **IMemoryCache** (for the background `SpotifyPlaybackMonitorService` which has no HTTP context)
- Session cookies: `HttpOnly = true`, `SameSite = Lax`, `IsEssential = true`

### 6.2 Real-Time Playback Monitor

`SpotifyPlaybackMonitorService` runs as an `IHostedService` from app startup:

- Polls Spotify every **4 seconds** during active playback
- Uses **intelligent delay calculation**: adapts polling interval based on how much time remains in the current track (polls more frequently near track end to catch changes quickly)
- Detects track changes, playback state changes, and post-skip windows
- Fires SSE events to all connected browser clients on any change
- Calls `SkipIfFlaggedAsync()` when the current track is AI-flagged
- Contains 85+ internal methods organized into nested state classes (`MonitoringState`, `TrackingState`, `EdgeCaseState`)

### 6.3 AI Content Moderation (Multi-Provider Failover)

The moderation pipeline in `LyricService.ModerateLyricsWithRetryAsync()` is a full multi-provider chain:

```
Provider 1: Gemini (gemini-1.5-flash)
    Rate limited: 30 RPM → SemaphoreSlim + 2s minimum interval
    Up to 3 attempts with exponential backoff on 429
    If 503 twice → skip to next provider

Provider 2: OpenRouter (meta-llama/llama-3.1-8b-instruct)
    Standard retry logic

Provider 3: Mistral (mistral-moderation-latest)
    Different response format — ParseMistralResponse() handles category_scores

If all fail → returns null (graceful, no crash)
```

Result model: `{ violence, hate, profanity, sexual, suitable_for_kids, confidence }`  
Cache: 24 hours keyed by `moderation_{lyrics.GetHashCode()}`

### 6.4 Lyrics Fetching & Parsing

**Fetching pipeline** (`LyricService.GetLyricsAsync`):
1. Cache check (`lyrics_{artist}_{title}`, 24h)
2. Genius API search → extract first hit URL
3. Playwright headless Chromium scrapes lyrics from `div[class*='Lyrics__Container']`
4. 35 browser optimisations (GPU off, images/CSS/fonts blocked, DOMContentLoaded wait)
5. Result cached 24h

**Parsing** (`ArtistLyricService`):
- Source-generated regex patterns detect `[Section: Artist]` annotations
- State machine tracks `currentArtist` and `currentSection` across lines
- Outputs `LyricMapping` with per-line metadata + artist-to-line-numbers dictionary
- Singleton DI — stateless, fast

### 6.5 WhatsApp Lyrics Chat View

`LyricsView.cshtml` renders lyrics as a group chat:
- Dark theme matching WhatsApp dark mode (`#0b141a`, `#2a2f32`, `#005c4b`)
- Artist-colored avatars with initials — color assigned deterministically from artist name hash
- Message bubbles with tails, timestamps, blue checkmarks
- Section badges (Verse 1, Chorus, Bridge, etc.) and line number ranges
- Search bar at the bottom to fetch any song
- Demo mode at `/LyricsView` (no params) shows sample "Rumi" / "Jinu" conversation

### 6.6 Sleep Timer

`SleepTimerService` is a fully server-managed timer:
- `StartTimer(minutes)` uses `Task.Delay` + `CancellationToken`
- On expiry: calls `client.Player.PausePlayback()` + sends SSE notification
- `GetStatus()` returns `IsActive`, `RemainingSeconds`, `ExpiresAt`, `Mode`
- REST API: `POST /api/playback/sleep-timer/start`, `POST .../cancel`, `GET .../status`
- Addresses years of Spotify Community complaints about their broken native sleep timer

### 6.7 Episode / Podcast Support

`EpisodeController` provides three endpoints:
- `GET /api/episodes/shows` — user's followed shows (up to 50)
- `GET /api/episodes/shows/{showId}/episodes` — episodes for a specific show with resume points
- `GET /api/episodes/new` — cross-show "new episodes" feed, sorted by release date, unplayed/in-progress only (restores the Spotify feature that was removed)
- `POST /api/episodes/queue` — add episode to queue

### 6.8 Queue Management

`QueueService` maintains a stateful `MusicQueue`:
- FIFO queue with `CurrentIndex` pointer
- 4-case sync logic: no queue → create; current track matches → update list; track in queue but offset → adjust index; track not found → full rebuild
- Semaphore-protected skip (`SkipFlaggedSongsAsync`) prevents race conditions
- 11-minute cache with 10-minute expiry (cache outlives expiry to reduce rebuild frequency)

### 6.9 Feature Flag System

`Microsoft.FeatureManagement.AspNetCore` with two JSON files per project:
- `featureflags.json` → production defaults (**most flags OFF** for safe deployment)
- `featureflags.Development.json` → all flags ON for local development
- Flags hot-reload without restart (`reloadOnChange: true`)
- Compile-time constants in `FeatureFlags.cs` prevent magic string bugs

### 6.10 Aspire Orchestration (Fixed)

Three bugs were identified and resolved (see `Aspire-Integration-Fixes.md`):
1. **Serilog wipe**: Changed `UseSerilog()` → `AddSerilog()` — OTel logs now flow to dashboard
2. **Duplicate HttpClient**: Removed second bare `AddHttpClient<LyricService>()` — SSL/pooling config is no longer silently overwritten
3. **Port pinning**: `WithEndpoint("http", e => e.Port = 7236)` + `WithEnvironment(...)` — OAuth callback never breaks

---

## 7. What Is Partially Implemented or Off

### 7.1 Lyrics Video Export (`EnableVideoExport`)

**Status:** Feature flag defined in `FeatureFlags.cs` and `featureflags.json`, but **zero implementation exists.**

The architecture doc specifies the full design:
- SkiaSharp for 30fps frame rendering (blurred album art, gradient overlay, highlighted current line)
- FFMpegCore for H.264/MP4 encoding
- Background job pattern: POST → job ID → poll for completion → download link (1h expiry)
- 16:9 and 9:16 (TikTok/Reels) output aspect ratios

**Blocked by:** Missing NuGet packages (`SkiaSharp`, `FFMpegCore`) and missing `LyricsVideoService` class.

### 7.2 Blazor Server Widgets (Widget Suite)

**Status:** Fully documented (PRD + Architecture docs), **zero code written.**

The widget system (at `/w/{name}`) is the biggest planned evolution:
- 8 widget types: NowPlaying, Lyrics (karaoke), Controls, SleepTimer, ContentShield, History, Stats, EpisodeFeed
- URL-param theming system (`?bg=&accent=&font=`)
- OBS Browser Source, MAUI WebView, PWA, iframe targets
- 5-phase roadmap over ~12 weeks

**Blocked by:** Blazor Server not yet added to `Alify.Spotify.csproj`, `IPlaybackSource` interface not created, Blazor component structure not scaffolded.

### 7.3 AI Moderation in Spotify Service

**Status:** The moderation logic lives in `Alify.Lyrics/LyricService.cs`, but `Alify.Spotify` uses a simpler `FreeLyricsProviderService` + its own basic flagging in `SpotifyPlaybackMonitorService`. The **full Gemini/OpenRouter/Mistral pipeline is not wired into the Spotify monitoring loop** — the monitor calls `SkipIfFlaggedAsync()` which relies on the `Track.IsFlagged` property, but the actual AI moderation logic is in the Lyrics project, not called by the monitor.

**Impact:** The `EnableSpotifyModeration` and `EnableSmartSkip` feature flags exist and the skip mechanism is wired, but AI-powered flagging in real-time playback monitoring needs the moderation service accessible from `Alify.Spotify`.

### 7.4 Content Shield Sensitivity Controls

**Status:** The PRD defines `Family` / `Teen` / `Adult` sensitivity levels, but no sensitivity model or threshold comparison exists in code. The current flagging is binary (`IsFlagged = true/false`).

### 7.5 `IPlaybackSource` Abstraction

**Status:** Fully designed in `Widgets-Architecture.md` but **not yet created.** `SpotifyService` is used directly everywhere. This interface is the foundation for YouTube Music support and for making the widget layer source-agnostic.

### 7.6 Dashboard Monitoring Toggle

**Status:** `Dashboard.cshtml.cs` has `OnPostStartMonitorAsync()` and `OnPostStopMonitorAsync()`, and `IsMonitoring` property, but the monitor itself runs on startup as `IHostedService` — the toggle wires into `SpotifyPlaybackMonitorService.StartMonitoringAsync()` / `StopMonitoringAsync()`. **Functionally connected but needs end-to-end verification.**

---

## 8. What Is Not Yet Implemented (Roadmap)

### 8.1 Database (Highest Priority — Blocks Multiple Features)

**No database exists in the current codebase.** Everything is in-process memory.

See §9 for the full database strategy. Blocked features:
- User accounts and preferences
- Playback history persistence
- Theme preset saving
- Moderation audit log
- Session stats across restarts
- ContentShield daily filter counts
- Widget embed code generation per user

### 8.2 Widget Delivery System (Phase 1–2 of Widget Roadmap)

- `/w/{name}` route structure
- `_WidgetLayout.razor` (zero-chrome layout)
- URL-param CSS variable injection (`?bg=&accent=`)
- SSE → Blazor `StateHasChanged()` bridge
- Widget index page at `/w`

### 8.3 Now Playing Widget (`NowPlayingWidget.razor`)

- Album art / Spotify Canvas looping video
- Track/episode name, artist, content badge (`✓ Clean`, `⚠ Flagged`, `🔞 Explicit`, `🎙 Episode`)
- Crossfade animation on track change
- Compact and expanded modes

### 8.4 Karaoke/Synced Lyrics Widget (`LyricsWidget.razor`)

- Timed lyrics list with client-side position estimator (±300ms accuracy)
- Current line highlighted, past lines faded (40% opacity)
- Auto-scroll so current line is always centered
- "Export Video" button → triggers lyrics video export flow

### 8.5 Playback Controls Widget (`PlaybackControlsWidget.razor`)

- Play/Pause, Previous, Next, Like/Unlike buttons
- Volume slider
- Device switcher (Spotify Connect)
- Keyboard shortcut support (Space, ←/→)

### 8.6 Content Shield Widget (`ContentShieldWidget.razor`)

- On/off toggle with shield icon state
- Sensitivity selector (Family / Teen / Adult)
- Daily filter counter (resets at midnight — needs DB)
- "Last skipped" item with AI reasoning
- Podcast moderation toggle

### 8.7 History Widget (`HistoryWidget.razor`)

- Last N tracks (5/10/25 configurable)
- Album art thumbnail, track name, artist, duration, content badge
- Skipped tracks shown with skip icon
- Server-side session history (needs DB for cross-restart persistence)

### 8.8 Session Stats Widget (`SessionStatsWidget.razor`)

- Total listening time today, tracks played/skipped/flagged
- "Top artist today"
- 7-day sparkline bar chart
- Resets at local midnight (needs DB)

### 8.9 Episode Feed Widget (`EpisodeFeedWidget.razor`)

- New/in-progress episodes across all followed shows
- Podcast moderation badge per episode
- "Add to Queue" button per episode
- (Backend `EpisodeController` already exists — only the widget UI is missing)

### 8.10 Theme Customizer (F7)

- Live preview with URL-encoded params
- Named preset save/load (up to 10 per user — needs DB)
- 8 built-in theme presets
- Shareable theme URL

### 8.11 Animated Lyrics Video Export (F8)

- `LyricsVideoService` (SkiaSharp + FFMpegCore)
- `POST /api/lyrics/export` endpoint
- Background job queue + polling endpoint
- 1-hour expiry download link
- 16:9 and 9:16 output

### 8.12 YouTube Music Support (Phase 5)

- `IPlaybackSource` interface (first create this)
- `YouTubeMusicSource` implementation
- Source selector in all widgets

### 8.13 Rate Limiting

- Per-user API rate limits via `Microsoft.AspNetCore.RateLimiting`
- DDoS mitigation
- Currently relying on cache as accidental stop-gap

### 8.14 Distributed Cache (Redis)

- Replace `IMemoryCache` with `IDistributedCache` + Azure Redis Cache
- Required for horizontal scaling (multiple instances)
- Redis-backed session state

### 8.15 Unit & Integration Tests

- `tests/` directory exists but is empty
- Planned: xUnit + Moq for service layer
- Integration tests for controller endpoints
- BenchmarkDotNet for performance regressions

### 8.16 API Documentation (Swagger)

- No Swashbuckle registered
- Controllers and models have XML doc comments — ready to generate

### 8.17 Content Security Policy (CSP)

- No CSP headers configured
- Required before public deployment

### 8.18 Monetisation & Pro Tier

| Tier | Price | Features |
|---|---|---|
| **Free** | $0 | Watermarked video exports (max 3/day), 5 saved themes, all core widgets, 14-day history |
| **Pro** | $4.99/mo or $39/yr | Watermark-free exports (unlimited), unlimited presets, 90-day history, priority support |

---

## 9. Database Strategy

### 9.1 Current State

**There is no database.** All state is in-process `IMemoryCache` or session. This means:
- All data is lost on app restart
- No multi-user state isolation beyond session cookies
- No historical data, preferences, or audit logs
- Cannot scale horizontally (memory is per-instance)

### 9.2 Recommended Platform: Azure SQL

**Azure SQL** is the best fit for Alify given:

| Factor | Reasoning |
|---|---|
| **Existing stack** | The recommended infrastructure (ARCHITECTURE.md §9.3) already targets Azure App Service — Azure SQL is a natural fit |
| **Structured data** | User preferences, playback history, moderation results are relational by nature |
| **EF Core** | Entity Framework Core integrates seamlessly with .NET 10 and Azure SQL |
| **Compliance** | Azure SQL has SOC 2, ISO 27001, GDPR tooling built-in |
| **Cost** | Basic tier starts at ~$5/month; serverless option scales to zero between sessions |
| **Full-text search** | Needed for track/lyric search history queries |
| **JSON support** | Azure SQL supports JSON columns for flexible moderation result storage |

**Alternative considered:** Azure Cosmos DB — better for unstructured or high-write scenarios, but the data model is inherently relational (users → preferences → history → tracks).

### 9.3 Proposed Schema

```sql
-- Users (tied to Spotify user ID)
Users
  Id             UNIQUEIDENTIFIER PK
  SpotifyUserId  NVARCHAR(50)  UNIQUE NOT NULL
  DisplayName    NVARCHAR(200)
  Email          NVARCHAR(300)
  CreatedAt      DATETIME2
  LastSeenAt     DATETIME2
  Tier           NVARCHAR(20)  -- 'free' | 'pro'

-- Preferences per user
UserPreferences
  UserId         UNIQUEIDENTIFIER FK → Users.Id
  Key            NVARCHAR(100)     -- e.g., 'ContentShield.Sensitivity'
  Value          NVARCHAR(MAX)     -- JSON or plain value
  UpdatedAt      DATETIME2

-- Widget theme presets
ThemePresets
  Id             UNIQUEIDENTIFIER PK
  UserId         UNIQUEIDENTIFIER FK → Users.Id
  Name           NVARCHAR(100)
  ThemeJson      NVARCHAR(MAX)     -- Serialized theme params
  IsBuiltIn      BIT
  CreatedAt      DATETIME2

-- Playback history
PlaybackHistory
  Id             BIGINT IDENTITY PK
  UserId         UNIQUEIDENTIFIER FK → Users.Id
  TrackId        NVARCHAR(50)      -- Spotify track/episode ID
  TrackName      NVARCHAR(500)
  ArtistName     NVARCHAR(500)
  PlayedAt       DATETIME2
  DurationMs     INT
  WasSkipped     BIT
  SkipReason     NVARCHAR(100)     -- 'manual' | 'content_shield' | 'sleep_timer'

-- Moderation audit log
ModerationResults
  Id             BIGINT IDENTITY PK
  TrackId        NVARCHAR(50)
  LyricsHash     NVARCHAR(64)      -- SHA-256 for dedup
  ResultJson     NVARCHAR(MAX)     -- Full moderation response
  Provider       NVARCHAR(50)      -- 'Gemini' | 'OpenRouter' | 'Mistral'
  CachedAt       DATETIME2
  ExpiresAt      DATETIME2

-- Sleep timer events
SleepTimerEvents
  Id             BIGINT IDENTITY PK
  UserId         UNIQUEIDENTIFIER FK → Users.Id
  StartedAt      DATETIME2
  DurationMinutes INT
  FiredAt        DATETIME2 NULL    -- null if cancelled
  WasCancelled   BIT
```

### 9.4 EF Core Setup Plan

```
1. Add packages to Alify.Core:
   - Microsoft.EntityFrameworkCore.SqlServer
   - Microsoft.EntityFrameworkCore.Design
   - Microsoft.EntityFrameworkCore.Tools

2. Create AlifyDbContext in Alify.Core/Infrastructure/Data/

3. Register in both Program.cs files:
   builder.Services.AddDbContext<AlifyDbContext>(options =>
       options.UseSqlServer(builder.Configuration.GetConnectionString("AzureSql")));

4. Store connection string in Doppler (not appsettings.json)

5. Run initial migration:
   dotnet ef migrations add InitialCreate --project Alify.Core
   dotnet ef database update

6. Gradually migrate from IMemoryCache to DB-backed repositories
   starting with: UserPreferences → PlaybackHistory → ModerationResults
```

### 9.5 Connection String Security

Connection string goes into **Doppler** as `AzureSql__ConnectionString` — never in source control. For local dev, use .NET User Secrets.

---

## 10. API & Configuration Reference

### 10.1 Alify.Spotify API Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/` | Home / login prompt |
| `GET` | `/Dashboard` | Main dashboard with playback info |
| `GET` | `/SpotifyEvents` | SSE event stream page |
| `GET` | `/callback` | OAuth redirect handler |
| `GET` | `/api/playback/current` | Current playback info + sleep timer status |
| `POST` | `/api/playback/add-to-queue` | Add track/episode to queue |
| `PUT` | `/api/playback/repeat` | Set repeat mode (track/context/off) |
| `POST` | `/api/playback/sleep-timer/start` | Start sleep timer |
| `POST` | `/api/playback/sleep-timer/cancel` | Cancel sleep timer |
| `GET` | `/api/playback/sleep-timer/status` | Get sleep timer status |
| `GET` | `/api/episodes/shows` | Get user's followed podcasts |
| `GET` | `/api/episodes/shows/{id}/episodes` | Get episodes for a show |
| `GET` | `/api/episodes/new` | New/in-progress episode feed |
| `POST` | `/api/episodes/queue` | Add episode to queue |
| `POST` | `/api/spotify/test-lyrics-parsing` | Demo ArtistLyricService |
| `GET` | `/api/spotify/lyrics-for-artist/{artist}` | Line numbers for artist |

### 10.2 Alify.Lyrics API Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/LyricsView` | WhatsApp-style lyrics chat (demo) |
| `GET` | `/LyricsView?artist=X&title=Y` | Fetch + display song lyrics |
| `GET` | `/api/lyrics/search` | Search Genius API |

### 10.3 Feature Flags Reference

| Flag | Module | Prod Default | Dev Default | Controls |
|---|---|---|---|---|
| `EnableGeniusSearch` | Lyrics | OFF | ON | Genius API search endpoint |
| `EnableLyricsChat` | Lyrics | OFF | ON | WhatsApp chat lyrics view |
| `EnableVideoExport` | Lyrics | OFF | ON | MP4 export (not implemented) |
| `EnableSpotifyModeration` | Spotify | OFF | ON | AI moderation pipeline |
| `EnableMultipleLyricsSources` | Spotify | OFF | ON | lyrics.ovh + others fallback |
| `EnableSmartSkip` | Spotify | OFF | ON | Auto-skip flagged tracks |
| `EnableSpotifyIntegration` | Both | ON | ON | Core OAuth + playback |
| `EnableBasicLyrics` | Both | ON | ON | Non-Genius lyrics fetching |
| `EnableCaching` | Both | ON | ON | IMemoryCache usage |

### 10.4 Required Secrets (via Doppler)

| Secret Key | Used In | Purpose |
|---|---|---|
| `ApiKeys__Genius__Token` | Alify.Lyrics | Genius API bearer token |
| `ApiKeys__Gemini__ApiKey` | Alify.Lyrics | Google Gemini API key |
| `ApiKeys__OpenRouter__ApiKey` | Alify.Lyrics | OpenRouter API key |
| `ApiKeys__Mistral__ApiKey` | Alify.Lyrics | Mistral API key |
| `Spotify__ClientId` | Alify.Spotify | Spotify app client ID |
| `Spotify__ClientSecret` | Alify.Spotify | Spotify app client secret |
| `Spotify__RedirectUri` | Alify.Spotify | `http://127.0.0.1:7236/callback` (overridden by Aspire) |
| `DOPPLER_TOKEN` | Both (startup) | Doppler service token |
| `AzureSql__ConnectionString` | Future | Azure SQL connection string |

---

## 11. Infrastructure & DevOps

### 11.1 Observability Stack

- **Serilog** → structured console logs (enriched with `LogContext`)
- **OpenTelemetry** → traces and metrics exported via OTLP to Aspire dashboard
- **ASP.NET Core HTTP Logging** → combined request/response logs with field filtering
- **Source-generated `[LoggerMessage]`** → zero-allocation log paths for hot paths (EventId ranges 100–419)
- **Aspire Dashboard** → real-time logs, traces, resource health at `http://localhost:19043`

### 11.2 Configuration Priority Chain

```
1. Doppler         (remote, 1h cached, highest priority)
2. Environment variables
3. .NET User Secrets (dev only)
4. appsettings.{Environment}.json
5. appsettings.json (lowest priority)
```

### 11.3 Running Locally

**Option A — Aspire (recommended):**
```bash
# Set startup project to Alify.AppHost
dotnet run --project Alify.AppHost
# Aspire dashboard: http://localhost:19043
# Spotify app:      http://127.0.0.1:7236
# Lyrics app:       http://localhost:5143
```

**Option B — Individual projects:**
```bash
# Terminal 1
cd Alify.Spotify && dotnet run

# Terminal 2
cd Alify.Lyrics && dotnet run
```

**Playwright one-time setup:**
```bash
cd Alify.Lyrics\bin\Debug\net10.0
pwsh playwright.ps1 install chromium
```

**Secrets setup:**
```bash
# Store Doppler token
dotnet user-secrets set "DOPPLER_TOKEN" "dp.st.xxxx" --project Alify.Spotify
dotnet user-secrets set "DOPPLER_TOKEN" "dp.st.xxxx" --project Alify.Lyrics
```

### 11.4 Future Deployment (Azure)

| Resource | Azure Service | Tier |
|---|---|---|
| App (Spotify) | Azure App Service (Linux) | B2 or higher (Playwright needs RAM) |
| App (Lyrics) | Azure App Service (Linux) | B1 |
| Database | **Azure SQL** | Basic → Standard as load grows |
| Cache | Azure Redis Cache | Basic C0 → Standard C1 |
| Secrets | Doppler → Azure Key Vault (future) | — |
| Logging | Azure Application Insights | — |
| Static assets | Azure CDN | — |
| Container | Docker (`mcr.microsoft.com/dotnet/aspnet:10.0`) | + Playwright deps |

**Docker note:** Playwright requires additional system libraries in the container:
```dockerfile
RUN apt-get update && apt-get install -y \
    libnss3 libatk1.0-0 libatk-bridge2.0-0 \
    libcups2 libdrm2 libxkbcommon0 libxcomposite1 \
    libxdamage1 libxrandr2 libgbm1 libasound2
```

---

## 12. Security Model

### 12.1 Implemented Security Controls

| Control | Implementation |
|---|---|
| **OAuth 2.0 CSRF** | `state` parameter generated + validated in callback |
| **HSTS** | Enabled in production via `UseHsts()` |
| **Secure session cookie** | `HttpOnly`, `SameSite=Lax`, `IsEssential` |
| **Token expiry buffer** | 5-minute buffer prevents mid-request token expiry |
| **Secret isolation** | Doppler + User Secrets — no keys in source control |
| **Bearer auth** | All external API calls use Authorization headers |
| **User-scoped cache keys** | `{resource}_{userId}` pattern prevents data leakage between users |

### 12.2 Gaps (Must Fix Before Public Release)

| Gap | Risk | Fix |
|---|---|---|
| No Content Security Policy | XSS | Add `app.UseSecurityHeaders()` with CSP policy |
| No CORS policy | Cross-origin attacks | Configure `AllowedOrigins` for widget iframe usage |
| No rate limiting | Abuse / DDoS | Add `Microsoft.AspNetCore.RateLimiting` middleware |
| No input validation | Injection | Add `[Required]`, `[MaxLength]` attributes; model state checks |
| No anti-forgery tokens | CSRF on forms | Add `@Html.AntiForgeryToken()` to Razor forms |
| Widget URLs expose session context | Privacy | Implement short-lived widget tokens |

---

## 13. Performance Architecture

### 13.1 Three-Tier Caching Strategy

| Layer | Scope | Implementation | TTL |
|---|---|---|---|
| **Application cache** | Lyrics, moderation, auth tokens | `IMemoryCache` | 24h / 1h |
| **API deduplication cache** | Playback state, queue, lyrics | `SpotifyRequestCache` | 5s / 60s / 1h |
| **Domain cache** | User queue state | `QueueService` in-memory | 11min |

### 13.2 Applied Performance Patterns

- **Source-generated logging** — `[LoggerMessage]` attributes compile to zero-allocation log methods
- **Method injection** — services resolved on-demand, not held in memory for entire request lifecycle
- **Connection pooling** — `SocketsHttpHandler` with `PooledConnectionLifetime = 2min`
- **Playwright optimizations** — 35 browser args, resource blocking (images/CSS/fonts), DOMContentLoaded wait
- **Response compression** — GZIP/Brotli in production
- **Output caching** — applied on `GET /api/playback/current` (10s)
- **Async everywhere** — all I/O operations are `async`/`await`
- **`IReadOnlyList<T>`** — immutable collection exposure in `LyricMapping`
- **`SemaphoreSlim`** — concurrency control without locking overhead

### 13.3 Not Yet Applied

- `Span<T>` / `Memory<T>` for string manipulation in lyrics parsing
- Object pooling (HttpClient messages, JsonDocument, StringBuilder)
- Compiled EF Core queries (blocked by no database)
- CDN for static assets
- Redis distributed cache (in-process only currently)

---

## 14. Known Issues & Technical Debt

| Issue | Severity | Status | Location |
|---|---|---|---|
| No database — all state lost on restart | Critical | Open | Entire solution |
| AI moderation not wired into Spotify monitor | High | Open | `SpotifyPlaybackMonitorService` |
| No CORS configuration | High | Open | Both `Program.cs` |
| No rate limiting | High | Open | Both services |
| No Content Security Policy | High | Open | Both services |
| No unit tests | High | Open | `tests/` folder empty |
| `IPlaybackSource` not created | Medium | Open | Blocks widget phase + YouTube Music |
| `ContentShield` sensitivity levels not implemented | Medium | Open | No threshold model exists |
| `EnableVideoExport` flag — no service behind it | Medium | Open | `LyricService` / no `LyricsVideoService` |
| Blazor Server not added to Alify.Spotify | Medium | Open | Blocks entire widget suite |
| `Geniefy/` directory empty | Low | Open | Reserved, no code |
| No Swagger/OpenAPI | Low | Open | Controllers have XML docs but no generator |
| `AuxiliaryBackchannelService` suppressed | Low | Mitigated | AppHost.cs — `Ignore` behavior |
| `TickerQ` registered but not actively used | Low | Open | Listed in deps, no jobs defined |
| `Mscc.GenerativeAI` package vs direct HTTP calls | Low | Inconsistency | Lyrics uses direct HTTP; package registered but not used |

---

## 15. Development Setup Guide

### 15.1 Prerequisites

- .NET 10 SDK
- Visual Studio 2025 or VS Code with C# Dev Kit
- Doppler CLI (optional but recommended)
- Spotify Developer account (create app at `developer.spotify.com`)
- API keys: Genius, Gemini, OpenRouter, Mistral

### 15.2 First-Time Setup

```bash
# 1. Clone the repo
git clone https://github.com/intisor/Alify
cd Alify

# 2. Restore packages
dotnet restore

# 3. Install Playwright browsers (one-time)
cd Alify.Lyrics
dotnet build
pwsh bin\Debug\net10.0\playwright.ps1 install chromium
cd ..

# 4. Configure secrets (choose one method)

## Option A: Doppler
doppler login
doppler setup
dotnet user-secrets set "DOPPLER_TOKEN" "dp.st.xxxx" --project Alify.Spotify
dotnet user-secrets set "DOPPLER_TOKEN" "dp.st.xxxx" --project Alify.Lyrics

## Option B: Manual User Secrets
dotnet user-secrets set "ApiKeys:Genius:Token" "your-token" --project Alify.Lyrics
dotnet user-secrets set "ApiKeys:Gemini:ApiKey" "your-key" --project Alify.Lyrics
dotnet user-secrets set "Spotify:ClientId" "your-id" --project Alify.Spotify
dotnet user-secrets set "Spotify:ClientSecret" "your-secret" --project Alify.Spotify
dotnet user-secrets set "Spotify:RedirectUri" "http://127.0.0.1:7236/callback" --project Alify.Spotify

# 5. Register redirect URI in Spotify Developer Dashboard
# Add: http://127.0.0.1:7236/callback

# 6. Run via Aspire
dotnet run --project Alify.AppHost
```

### 15.3 Recommended Development Order (Next Steps)

Based on the current state, here's the recommended priority order for development:

1. **Database setup** — Azure SQL + EF Core + `AlifyDbContext` (unblocks everything)
2. **User persistence** — tie Spotify user ID to DB record on first login
3. **Playback history** — write to DB after each track plays/skips
4. **Wire AI moderation into Spotify monitor** — call the full chain from `SpotifyPlaybackMonitorService`
5. **Rate limiting middleware** — before any public-facing routes go live
6. **CSP headers** — security prerequisite for public release
7. **Unit tests** — cover `ArtistLyricService`, `QueueService`, `SleepTimerService`
8. **`IPlaybackSource` interface** — refactor `SpotifyService` to implement it
9. **Blazor Server widgets Phase 1** — widget routing, layout, SSE bridge
10. **Lyrics video export** — add SkiaSharp + FFMpegCore, implement `LyricsVideoService`

---

*This document consolidates the full state of the Alify codebase as of its current commit, cross-referenced against all six documentation files, the ARCHITECTURE.md deep-dive, README.md, and direct source code analysis of every project, service, controller, and configuration file in the solution.*
