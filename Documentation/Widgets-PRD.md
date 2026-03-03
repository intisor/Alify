# Alify Widgets — Product Requirements Document (PRD)

**Product:** Alify Widgets
**Version:** 1.1
**Status:** Approved — Ready for Implementation
**Date:** February 2026

---

## 1. Executive Summary

Alify Widgets transforms Alify from a web portal users must actively open into an **always-present, embeddable intelligence layer** for Spotify. Users pin small, focused widgets in their OBS scene, desktop overlay, or browser — and Alify works silently in the background: displaying lyrics, enforcing content rules, managing sleep timers, and exporting content.

The widget delivery model removes the single biggest obstacle to adoption: the friction of opening a separate web application just to control music playback.

---

## 2. Background & Market Opportunity

### The Market Is Proven

"Now playing" Spotify widgets/overlays are actively purchased and downloaded by a large, engaged user base:

- **Streamers:** Viewers constantly ask "what song is this?" — hundreds of thousands of monthly views on widget setup tutorials. Tools like Songify, Nutty.gg, StreamSpell, and Amuse (6K Labs) have built real businesses here.
- **Desktop users:** Reddit is full of threads begging for lyrics overlays, always-on mini-players, and multitasking music views. Spotify's own mini-player arrived only in 2024 and is Premium-only with minimal functionality.
- **Family users:** No tool on the market offers real-time, AI-powered content filtering for music playback — they want set-and-forget protection that doesn't require parental monitoring.

### The Gap We Fill

Existing widgets solve surface problems (show song title, album art). They are:
- **Brittle** — JS polling with 5–15s delays, breaks when Spotify updates
- **Shallow** — no content awareness, no moderation, no lyrics sync
- **Aesthetics-only** — no intelligence behind them

Alify already has what none of them have: a **real-time server-side playback monitor**, **AI content moderation pipeline**, **independent lyrics sourcing**, and **episode/podcast support**. Widgets are the new surface for this existing engine.

---

## 3. Goals & Success Metrics

### Product Goals
1. Reduce time-to-value for new users from "open portal → navigate → configure" to "paste URL → done"
2. Make Alify the default "now playing" widget for streamers who care about content safety
3. Establish Alify as the only Spotify widget with real content intelligence

### Success Metrics

| Metric | 3-Month Target | 6-Month Target |
|--------|----------------|----------------|
| Widget unique installs / embeds | 500 | 5,000 |
| OBS browser source activations | 200 | 2,000 |
| Avg. widget session duration | > 60 min | > 90 min |
| ContentShield daily active users | 100 | 500 |
| Lyrics widget engagement rate | 40% of active users | 55% |
| Animated video exports | 50/week | 500/week |
| NPS (widget users) | > 40 | > 55 |

---

## 4. User Personas

### Persona 1: Marco — The Music Streamer 🎮
- **Age:** 22, streams on Twitch 4x/week, 800 followers
- **Goal:** Show viewers what's playing without needing a paid service. Wants lyrics on screen for singalong moments.
- **Pain:** Other widgets delay 5–10 seconds. Themes are ugly. No way to auto-skip explicit tracks when streaming family-friendly content.
- **Willingness to pay:** $0–$10/month if the tool is noticeably better
- **Setup tolerance:** Will spend 10–15 minutes if result is polished
- **Key quote:** *"I just want to paste a URL into OBS and have it look good."*

### Persona 2: Priya — The Focused Desktop Worker 🖥️
- **Age:** 29, works from home, second monitor available
- **Goal:** See lyrics and current track on secondary display without alt-tabbing to Spotify
- **Pain:** Spotify's mini-player is too small, no lyrics. Can't control sleep timer without opening the app.
- **Willingness to pay:** $0 (expects free tier with premium option)
- **Setup tolerance:** 5 minutes max
- **Key quote:** *"I just want it on my second screen and I don't want to think about it."*

### Persona 3: David & Sarah — The Parents 👨‍👩‍👧
- **Age:** 38/36, Premium Family plan, 3 kids aged 8–14
- **Goal:** Spotify plays in common areas — they want content automatically filtered without monitoring every song
- **Pain:** Spotify's explicit filter is binary and misses a lot. Podcasts have no filter. No per-artist or per-category controls.
- **Willingness to pay:** $5–$15/month for reliable protection
- **Setup tolerance:** 30 minutes for full setup, then forgets about it
- **Key quote:** *"I don't want to babysit the playlist, I want it handled."*

### Persona 4: Kai — The Self-Hoster 🔧
- **Age:** 31, homelab enthusiast, privacy-focused
- **Goal:** Run Alify locally, display widgets on home dashboard (e.g., Home Assistant, desktop)
- **Pain:** Every "now playing" widget sends data to a third-party cloud service
- **Willingness to pay:** $0 (will donate if project is open)
- **Setup tolerance:** High — reads documentation, configures Docker
- **Key quote:** *"I want my listening data to never leave my house."*

---

## 5. Jobs To Be Done

| Job | Current Solution | Alify Widgets Solution |
|-----|-----------------|----------------------|
| Show what's playing on stream | Songify ($3.99/mo), other paid tools | Free, self-hosted, OBS URL |
| Display synced lyrics while multitasking | None (Spotify app only, not embeddable) | `/w/lyrics` — karaoke widget |
| Skip explicit tracks automatically | Spotify's binary E-tag (misses a lot) | AI-analyzed content categories |
| Sleep timer that actually works | Spotify's native sleep timer (broken, buggy) | `/w/sleep-timer` — reliable, countdown ring |
| Share what you listened to | Screenshots, Receiptify | Animated lyrics video export |
| Control Spotify from a pinned overlay | Open Spotify app | `/w/controls` — play/pause/skip/device |
| See session listening stats in real time | Spotify Wrapped (annually) | `/w/stats` — live today/weekly counts |

---

## 6. Feature Specifications

---

### F1 — Widget Delivery System

**Summary:** Infrastructure that makes every widget embeddable anywhere.

**User Story:**
> As a streamer, I want to paste one URL into OBS and immediately see a working widget, so I don't have to configure anything complex.

**Acceptance Criteria:**
- [ ] Every widget is accessible at `/w/{name}` with no authentication required (session token passed via URL param or cookie)
- [ ] Widgets render with zero chrome: no navbar, no footer, transparent or solid background
- [ ] URL parameters control appearance: `?bg=000000&accent=1DB954&compact=true&font=Inter`
- [ ] Widget index at `/w` shows all available widgets with:
  - Live preview (5 seconds of simulated data)
  - Copy URL button
  - OBS setup instructions link
  - Theme customizer quick-launch
- [ ] Widgets handle "not authenticated" state gracefully (show connect prompt, not error)
- [ ] Widgets handle "nothing playing" state gracefully (show placeholder, not crash)
- [ ] All widgets work correctly in OBS CEF browser (Chromium-based, no audio access)

**Out of scope:** Authentication via widget URL (user must log in via main app first)

---

### F2 — Now Playing Widget

**Summary:** Core visual widget — the thing everyone sees first.

**User Story:**
> As a streamer, I want my viewers to see the current song's album art, title, and whether it's clean or flagged, so I can prove I'm running content-safe music.

**Acceptance Criteria:**
- [ ] Displays: album art (or podcast cover), track/episode name, artist/show name
- [ ] Smooth crossfade animation (300ms) on track change — no flicker
- [ ] Content badge: `✓ Clean` (green), `⚠ Flagged` (amber), `🔞 Explicit` (red), `🎙 Episode` (blue)
- [ ] If Spotify Canvas is available, displays the looping video clip instead of static image
- [ ] Compact mode (art + title only, < 120px height) and expanded mode (art + metadata + badge)
- [ ] Track change triggers a brief highlight animation so viewer notices the change
- [ ] No visible jump/reload on track change — widget is persistent

---

### F3 — Lyrics Widget (Synced / Karaoke)

**Summary:** Scrolling lyrics synced to playback position — the highest-demand streamer feature.

**User Story:**
> As a desktop user, I want to see the current line of lyrics highlighted and scrolling in real time on my second monitor, so I can read along without looking at Spotify.

**Acceptance Criteria:**
- [ ] Lyrics sourced from Alify's lyrics pipeline (independent of Spotify's Musixmatch)
- [ ] Current line is bold/highlighted; past lines fade (40% opacity); upcoming lines are normal
- [ ] Scrolls automatically so current line is always centered vertically
- [ ] Sync accuracy: current line displays within ±300ms of actual audio
- [ ] When no lyrics are available: shows "No lyrics available" message (not blank)
- [ ] For episodes: shows episode description in place of lyrics
- [ ] Artist attribution shown on featured sections (e.g., "[Post Malone]" inline before verse)
- [ ] Font size configurable via URL param (`?fontsize=lg`)
- [ ] "Export video" button visible — launches animated export flow

---

### F4 — Playback Controls Widget

**Summary:** Full playback control panel — pause, skip, like, device switcher.

**User Story:**
> As a desktop user, I want to control Spotify from a pinned overlay without alt-tabbing to the app, so I can manage music while in another app or game.

**Acceptance Criteria:**
- [ ] Buttons: Previous, Play/Pause (toggles with real state), Next, Like/Unlike (toggles heart state)
- [ ] Volume slider (where Spotify Connect supports volume control on the active device)
- [ ] Device switcher: shows all active Spotify Connect devices, click transfers playback
- [ ] All actions reflect in Spotify within 500ms
- [ ] Disabled state shown when no active playback device found
- [ ] Works correctly in MAUI WebView2 (desktop overlay target)
- [ ] Keyboard shortcuts supported when widget is focused: Space (play/pause), →/← (skip/previous)

---

### F5 — Sleep Timer Widget

**Summary:** Reliable client-side sleep timer — fixes Spotify's broken native one.

**User Story:**
> As a user, I want to set a sleep timer that actually pauses Spotify when it expires, so I can fall asleep without music playing all night.

**Acceptance Criteria:**
- [ ] Preset buttons: 15, 30, 45, 60, 90, 120 minutes + custom input
- [ ] Visual countdown: animated circular progress ring with time remaining
- [ ] Cancel button visible while timer is active
- [ ] When timer fires: Spotify pauses via API + SSE notification sent
- [ ] Timer state persists through browser refresh (stored server-side)
- [ ] Compact mode: shows only time remaining pill when active, full UI when clicked

---

### F6 — Content Shield Widget

**Summary:** Visibility and control interface for the AI content moderation system.

**User Story:**
> As a parent, I want to see that content filtering is active and working, and adjust sensitivity without opening a settings page.

**Acceptance Criteria:**
- [ ] Large on/off toggle (shield icon changes to indicate state)
- [ ] Sensitivity dropdown: `Family` / `Teen` / `Adult` — maps to AI moderation thresholds
- [ ] Counter: "X tracks filtered today" — resets at midnight
- [ ] Last filtered item shown: "Skipped: [track name] — violence"
- [ ] Podcast moderation toggle (separate from music — can enable independently)
- [ ] Explain-my-decision mode: tap last filtered item to see AI reasoning
- [ ] Shield state change applies immediately (no page reload)

---

### F7 — Theme Customizer

**Summary:** Per-widget visual theming with shareable presets.

**User Story:**
> As a streamer, I want my lyrics widget to match my stream brand colors and save that preset, so every time I use it, it looks right without re-configuring.

**Acceptance Criteria:**
- [ ] Customizable properties: background color, accent color, text color, corner radius, font family (Google Fonts), font size
- [ ] Live preview — changes apply instantly, no reload
- [ ] "Copy URL" button that encodes current theme as URL parameters
- [ ] Save named preset (stored server-side per user account, up to 10 presets)
- [ ] Preset library with 8 built-in themes: Spotify Dark, Midnight, Sakura, Retro, Minimal White, Deep Ocean, Neon, Warm Wood
- [ ] Export theme as shareable URL that others can import

---

### F8 — Animated Lyrics Video Export

**Summary:** Generate a shareable short video of the current song's lyrics — for TikTok, Reels, or clips.

**User Story:**
> As a listener, I want to export a short lyrics video for a song I love so I can post it on TikTok without any design work.

**Acceptance Criteria:**
- [ ] Triggered from lyrics widget — "Export Video" button
- [ ] Output: `.mp4` file, user choice of 16:9 (streaming/YouTube) or 9:16 (TikTok/Reels)
- [ ] Visual style: album art blurred as background, gradient overlay, lyrics centred, current line larger weight
- [ ] Max video length: full song or custom clip (start/end position) up to 90 seconds
- [ ] Generation completes in < 30 seconds for a 90-second clip
- [ ] Generated file downloadable via direct link — expires after 1 hour
- [ ] Watermark: "Made with Alify" (small, bottom corner — configurable off for premium users)
- [ ] No data sent to third-party services — generated entirely server-side

---

### F9 — History Widget

**Summary:** Recently played tracks list — live, updated per track change.

**Acceptance Criteria:**
- [ ] Shows last N tracks (user-configurable: 5 / 10 / 25 via URL param)
- [ ] Each row: album art thumbnail, track name, artist, duration played, content badge
- [ ] Skipped tracks show a skip icon (not hidden)
- [ ] Persists across browser refresh (server-side session history)
- [ ] Click on any track row: opens Spotify deep link for that track

---

### F10 — Session Stats Widget

**Summary:** Live listening analytics for today's session.

**Acceptance Criteria:**
- [ ] Displays: total listening time today, tracks played, tracks skipped, tracks flagged
- [ ] Optional sparkline: last 7-day listening minutes bar chart
- [ ] Resets at user's local midnight
- [ ] "Top artist today" derived from session history
- [ ] All data computed from Alify's own session tracking — no Spotify API call needed at render time

---

### F11 — Episode Feed Widget

**Summary:** New unplayed podcast episodes feed — restores feature Spotify removed.

**Acceptance Criteria:**
- [ ] Shows latest unplayed episodes from all followed shows, sorted by release date
- [ ] Each episode: cover art, episode name, show name, duration, release date
- [ ] In-progress episodes show partial progress bar (using resume position from API)
- [ ] "Add to Queue" button per episode
- [ ] Podcast moderation badge shown per episode (Pending / Safe / Flagged) based on description analysis
- [ ] Scrollable list, max 20 episodes shown

---

### F12 — Multi-Source Support (Phase 4)

**Summary:** Extend all widgets to work with YouTube Music in addition to Spotify.

**Acceptance Criteria:**
- [ ] Source selector toggle visible in each widget: `Spotify` / `YouTube Music`
- [ ] All widget functionality (controls, lyrics, content badge, sleep timer) works identically for both sources
- [ ] Switching source mid-session does not reload widget — seamless transition
- [ ] YouTube Music authenticated separately via its own OAuth flow

---

## 7. Non-Functional Requirements

| Category | Requirement |
|---|---|
| **Performance** | Widget first render < 500ms |
| **Lyrics accuracy** | Current line within ±300ms of playback position |
| **SSE latency** | Track change reflected in widget < 150ms |
| **Concurrency** | 10+ widgets running simultaneously per user without degradation |
| **Availability** | Widget gracefully degrades if backend unreachable (shows last known state) |
| **Security** | Widget URLs do not expose session tokens in server logs |
| **Privacy** | Lyrics video generation entirely server-side, no third-party upload |
| **Compatibility** | OBS CEF (Chrome 119+), Chrome, Edge, WebView2, Safari (PWA) |
| **Accessibility** | All widgets pass WCAG 2.1 AA. High-contrast mode via `?contrast=high`. Full `aria-label` coverage. Keyboard navigation on all interactive controls. |

---

## 8. Constraints & Risks

### Constraints
- Lyrics provider used internally must not be referenced publicly (legal sensitivity)
- Spotify Web API rate limits apply — monitor polling interval must respect limits
- Widget functionality limited to what Spotify's API exposes (no audio capture, no waveform)

### Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Spotify API changes break monitor | Medium | High | Abstract behind `IPlaybackSource`; monitor Spotify changelog |
| Lyrics sync is inaccurate for slow connections | Medium | Medium | Position estimated locally between SSE events |
| Video export is too slow for user patience | Low | Medium | Background job + polling for completion |
| Streaming users can't run Alify backend locally | Medium | High | Provide Docker image + cloud-hosted option |
| OBS browser source CEF quirks | Low | Low | Test against current OBS CEF version |
| Lyrics unavailable for ~15% of songs | High | Medium | Graceful fallback message + "Request lyrics" button that queues the track for community/human contribution |

---

## 9. Release Plan

| Phase | Milestone | Target |
|---|---|---|
| **Phase 1** | Widget infrastructure live (routing, SSE bridge, layout) | Week 2 |
| **Phase 2** | 5 MVP widgets live: NowPlaying, Lyrics, Controls, SleepTimer, Shield | Week 4 |
| **Phase 3** | Theme system, History, Stats, Canvas, Episode Feed | Week 6 |
| **Phase 4** | Animated video export + Widget index + share page | Week 8 |
| **Phase 5** | YouTube Music source + multi-source selector | Week 12 |

---

## 10. Product Decisions (formerly Open Questions)

### Hosting — Hybrid
- **Cloud default:** `widgets.alify.app/w/now-playing?user=xxx` — zero setup for streamers
- **Self-hosted:** Official Docker image: `docker run -p 8080:8080 alify/widgets` → widget URLs become `http://your-homelab:8080/w/...`
- Theme sharing works identically in both modes (URL-encoded params or server-stored presets)

### Monetisation — Free / Pro Split at Launch

| Tier | Price | Inclusions |
|------|-------|------------|
| **Free** | $0 | Watermarked video exports (max 3/day), 5 saved themes, all core widgets, 14-day history |
| **Pro** | $4.99/mo or $39/yr | Watermark-free exports (unlimited), unlimited theme presets, 90-day history, priority support, ad-free widget index |

This matches existing market pricing (Nutty.gg, Amuse) and feels fair for the value delivered.

### Song Request (F13) — Phase 6 "Chat Integration Pack"
- Out of v1.0 scope to protect MVP timeline
- Scoped as: Twitch / YouTube / Kick chat commands → Alify queue
- Will ship as a paid Pro add-on in Phase 6

### Mobile — PWA for v1.0
- Install-to-home-screen gives widget-like behaviour on Android and iOS already
- Native app (MAUI / React Native) reconsidered only if 6-month metrics show >30% mobile usage

### Analytics Data Retention
- **Free:** 14 days rolling
- **Pro:** 90 days rolling
- User can manually delete at any time

---

## 11. Frozen Feature Additions

### F13 — Widget Health Indicator
**Summary:** Every widget shows a persistent live-status dot so streamers immediately know if Alify is connected.

**Acceptance Criteria:**
- [ ] Tiny dot in the corner of every widget: 🟢 connected & playing, 🟡 connected & paused, 🔴 disconnected
- [ ] Hover/tap reveals tooltip: "Last update 3s ago — Spotify API healthy" or "Reconnecting..."
- [ ] Dot disappears entirely in `?compact=true` mode (space-sensitive layouts)
- [ ] Dot state updates via the same SSE channel — no additional polling
