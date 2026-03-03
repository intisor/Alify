# Alify Widgets — Technical Architecture

## Vision

Alify evolves from a web portal users must open into a **suite of embeddable, intelligent widgets** that run silently in the background. Users pin what they need — a lyrics overlay for streaming, a content shield for their family, a sleep timer pill — and forget about it. Alify just works.

> "The only Spotify widget that also flags explicit content, controls your sleep timer, and exports lyrics videos."

---

## System Architecture Overview

```
┌──────────────────────────────────────────────────────┐
│                  Alify.Spotify Backend                │
│                                                      │
│  ┌──────────────────────────────────────────────┐    │
│  │           IPlaybackSource (NEW)               │    │
│  │  GetCurrentPlaybackAsync()                    │    │
│  │  GetQueueAsync()                              │    │
│  │  ControlPlaybackAsync(action)                 │    │
│  └──────┬─────────────────────┬─────────────────┘    │
│         │                     │                      │
│  ┌──────▼───────┐    ┌────────▼────────┐             │
│  │ SpotifySource│    │YouTubeMusicSource│  (Phase 4)  │
│  │ (existing    │    │(new impl.)       │             │
│  │ services)    │    │                  │             │
│  └──────┬───────┘    └─────────────────┘             │
│         │                                            │
│  ┌──────▼──────────────────────────────────────┐     │
│  │ PlaybackMonitor (background, source-agnostic)│     │
│  │ → Detects track changes                      │     │
│  │ → Runs content moderation                    │     │
│  │ → Auto-skips flagged content                 │     │
│  │ → Fires SSE events                           │     │
│  └──────┬──────────────────────────────────────┘     │
│         │ SSE                                        │
│  ┌──────▼──────────────────────────────────────┐     │
│  │         SseService (existing)                │     │
│  └──────┬──────────────────────────────────────┘     │
└─────────┼────────────────────────────────────────────┘
          │ SignalR (Blazor Server websocket)
          │
┌─────────▼────────────────────────────────────────────┐
│               Widget Components Layer                 │
│                                                      │
│   /w/now-playing      NowPlayingWidget.razor          │
│   /w/lyrics           LyricsWidget.razor              │
│   /w/controls         PlaybackControlsWidget.razor    │
│   /w/sleep-timer      SleepTimerWidget.razor          │
│   /w/shield           ContentShieldWidget.razor       │
│   /w/history          HistoryWidget.razor             │
│   /w/stats            SessionStatsWidget.razor        │
│   /w/episodes         EpisodeFeedWidget.razor         │
│   /w                  WidgetIndex.razor               │
└──────────────────────────────────────────────────────┘
          │
          │ Renders inside any of:
          ▼
  OBS Browser Source  │  MAUI WebView  │  PWA  │  iframe  │  Stream Deck
```

---

## Key Abstraction: IPlaybackSource

The single most important new piece of infrastructure. All monitors, widgets, and services program against this interface — adding YouTube Music in Phase 4 requires zero changes to widget code.

```csharp
namespace Alify.Core.Interfaces
{
    public interface IPlaybackSource
    {
        string SourceName { get; }           // "Spotify", "YouTube Music"
        bool IsAuthenticated();
        Task<SpotifyPlaybackInfo?> GetCurrentPlaybackAsync();
        Task<MusicQueue?> GetQueueAsync();
        Task ControlPlaybackAsync(PlaybackAction action); // Play/Pause/Next/Prev
        Task SetVolumeAsync(int percent);
        Task TransferPlaybackAsync(string deviceId);
        Task<IEnumerable<PlaybackDevice>> GetDevicesAsync();
    }
}
```

---

## Blazor Server Integration

### Why Blazor Server (not WASM)
- Components call existing .NET services **directly** — no REST API round-trip overhead
- Server holds SSE subscription — component just reacts with `StateHasChanged()`
- Thin client: works in any WebView including OBS's CEF browser
- No CORS issues (widget calls are same-server)

### Standard Widget Component Pattern

Every widget follows the same structure:

```razor
@inject IPlaybackSource PlaybackSource
@inject SseService Sse
@implements IDisposable

<div class="widget">
    <!-- widget markup using @_info -->
</div>

@code {
    private SpotifyPlaybackInfo? _info;

    protected override void OnInitialized() =>
        Sse.OnPlaybackUpdate += OnUpdate;

    void OnUpdate(SpotifyPlaybackInfo info) {
        _info = info;
        InvokeAsync(StateHasChanged);
    }

    public void Dispose() =>
        Sse.OnPlaybackUpdate -= OnUpdate;
}
```

### Widget Routing Setup

`Program.cs` addition:
```csharp
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();
```

Widget pages live at `Components/Widgets/*.razor` with `@page "/w/{name}"`.
A `_WidgetLayout.razor` provides zero-chrome layout (no navbar, no footer, transparent/dark background).

### Project Structure

```
Alify.Spotify/
├── Pages/                          ← existing full pages (unchanged)
│   └── Dashboard/Dashboard.cshtml
└── Components/                     ← NEW
    ├── _Imports.razor
    ├── App.razor
    ├── Layouts/
    │   └── _WidgetLayout.razor     ← zero-chrome layout
    └── Widgets/
        ├── NowPlayingWidget.razor
        ├── LyricsWidget.razor
        ├── PlaybackControlsWidget.razor
        ├── SleepTimerWidget.razor
        ├── ContentShieldWidget.razor
        ├── HistoryWidget.razor
        ├── SessionStatsWidget.razor
        ├── EpisodeFeedWidget.razor
        └── WidgetIndex.razor
```

---

## Theme System

One CSS file loaded in `_WidgetLayout.razor`. All widgets use CSS custom properties — no inline styles.

```css
:root {
    --bg:       var(--param-bg,      #121212);
    --accent:   var(--param-accent,  #1DB954);
    --text:     var(--param-text,    #FFFFFF);
    --radius:   var(--param-radius,  12px);
    --font:     var(--param-font,    'Inter');
}
```

URL params are captured by a Blazor `[Parameter]` on `_WidgetLayout.razor` and applied via JS interop on mount:

```csharp
// _WidgetLayout.razor
[Parameter] public string? Bg { get; set; }
[Parameter] public string? Accent { get; set; }

protected override async Task OnAfterRenderAsync(bool firstRender) {
    if (firstRender)
        await JS.InvokeVoidAsync("applyTheme", Bg, Accent);
}
```

No server-side state needed for URL-param themes. Named presets (saved by user) are stored server-side.

---

## Lyrics Sync Mechanism

```
Track Change Event
  → SSE fires → LyricsWidget.OnUpdate() called
  → Widget fetches timed lyrics: List<(int TimeMs, string Text, string Artist)>
  → Client-side timer (every 500ms):
        estimatedPosition = lastKnownPositionMs + (DateTime.UtcNow - lastEventTime).TotalMilliseconds
        currentLine = lines.LastOrDefault(l => l.TimeMs <= estimatedPosition)
  → Blazor re-renders: current line highlighted, others dimmed
  → CSS scroll-behavior: smooth scrolls current line to center
```

The estimated position between SSE events keeps accuracy within ±300ms even on slow connections.

---

## Content Moderation Pipeline (Extended to Podcasts)

```
Track/Episode begins playing
  │
  ├── If music track:
  │     → Fetch lyrics from lyrics pipeline
  │     → ModerateLyricsWithRetryAsync(lyrics)
  │
  └── If podcast episode:
        → Use episode description (already in FullEpisode object)
        → ModerateLyricsWithRetryAsync(description)

  → Result: { HasProfanity, HasViolence, HasSexualContent, HasDrugReferences, Confidence }
  → Compare against user's ContentShield sensitivity:
        Family = flag any category at medium confidence
        Teen   = flag violence/sexual at medium, profanity at high
        Adult  = flag sexual at high confidence only
  → If exceeds threshold:
        → SpotifyService.SkipAsync()
        → SSE event: "content_skipped" { trackName, categories, reason }
        → ContentShieldWidget updates counter + last-skipped display
```

---

## Animated Lyrics Video Export

```
User clicks "Export Video" in LyricsWidget
  → POST /api/lyrics/export
        { trackId, stylePreset, aspectRatio: "16:9"|"9:16", startSec, endSec }
  → LyricsVideoService (server-side):
      1. Load lyrics from cache (already fetched for current session)
      2. Download album art over HTTP → MemoryStream
      3. SkiaSharp rendering loop at 30fps:
           Frame N:
             - Draw album art scaled to fill, blur radius 20px
             - Gradient overlay (bottom 60%): rgba(0,0,0,0.7)
             - Calculate which lyric line is active at frame time
             - Draw past lines: 60% opacity, normal weight
             - Draw current line: 100% opacity, bold, 1.4× font size
             - Draw upcoming lines: 80% opacity, normal weight
      4. FFMpegCore: encode frame sequence → .mp4 (H.264, AAC silent)
  → File saved to /tmp/alify-exports/{guid}.mp4
  → Response: { downloadUrl: "/exports/{guid}", expiresIn: 3600 }
  → Background job: delete file after 1 hour
```

**Dependencies to add:**
- `SkiaSharp` — 2D graphics rendering
- `FFMpegCore` — video encoding (requires FFmpeg binary on server)

---

## Deployment Models

| Model | How | Who |
|-------|-----|-----|
| **OBS Browser Source** | Paste `/w/now-playing?accent=1DB954` — zero setup | Streamers |
| **MAUI Blazor Hybrid** | `.msix` installer, WebView2 pane pinned to corner | Desktop/gaming users |
| **PWA** | "Install app" from browser → taskbar/home screen shortcut | Mobile, quick access |
| **iframe embed** | `<iframe src="/w/lyrics">` in any web page | Devs, custom dashboards |
| **Stream Deck** | Browser action tile → widget URL | AV enthusiasts |
| **Self-hosted / Docker** | `docker run alify` → localhost widgets | Privacy-focused users |

---

## Phased Delivery Plan

### Phase 1 — Foundation (Week 1–2)
- `IPlaybackSource` interface + refactor SpotifyService to implement it
- Add Blazor Server to `Alify.Spotify.csproj`
- `_WidgetLayout.razor` (zero chrome, CSS variable system)
- Widget routing `/w/*` + SSE → Blazor bridge
- `/w` widget index page

### Phase 2 — MVP Widgets (Week 3–4)
- `NowPlayingWidget.razor`
- `LyricsWidget.razor` (synced, karaoke)
- `PlaybackControlsWidget.razor` (controls + device switcher)
- `SleepTimerWidget.razor`
- `ContentShieldWidget.razor`
- URL-param theme system

### Phase 3 — Streamer Features (Week 5–6)
- In-widget theme editor + preset save/load
- `HistoryWidget.razor`, `SessionStatsWidget.razor`
- Canvas video in NowPlaying, `EpisodeFeedWidget.razor`
- Podcast content moderation

### Phase 4 — Export & Sharing (Week 7–8)
- Animated lyrics video export (SkiaSharp + FFMpegCore)
- Widget share page + embed code generator
- OBS + Stream Deck setup guide at `/docs/streaming`

### Phase 5 — Multi-Source (Week 9–12)
- YouTube Music `IPlaybackSource` implementation
- Source selector in all widgets

---

## Non-Functional Requirements

| Requirement | Target |
|---|---|
| Widget first render | < 500ms |
| Lyrics sync accuracy | ±300ms of actual playback |
| SSE → widget latency | < 150ms |
| Concurrent widgets per user | 10+ without degradation |
| Theme FOUC | Zero (CSS variables set before first paint) |
| Offline/degraded state | Show last known state, never error screen |
| OBS compatibility | CEF Chrome 119+ |

---

## Out of Scope

- Playlist management (Spotify already does this)
- Playlist sorting / duplicate detection (utility, not companion-specific)
- Song request via Twitch chat (Phase 6+)
- Discord rich presence (Phase 6+)
- Rainmeter / Lively Wallpaper integration (community contribution)
