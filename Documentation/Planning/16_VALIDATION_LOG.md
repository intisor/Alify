# 16 — Validation Log

**Document ID:** VAL · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Executed validation record for the planning pack. This is the first document in the pack containing **actual command output** rather than read-only inspection.
**Accountable owner:** Engineering/quality roles, TBD · **Approvals:** Not granted

Purpose: independently re-check the pack's load-bearing factual claims against the repository at a known commit, and record what is confirmed, what is wrong, and what the build actually proves. Companions: [Audit](02_CURRENT_STATE_AUDIT.md), [Traceability and gates](14_TRACEABILITY_AND_RELEASE_GATES.md).

## 1. Validation context

| Item | Value |
| --- | --- |
| Commit validated | `de7bbc331a9c0963c7f78956362499f84c62172e` (2026-02-04) |
| Working tree | Clean except untracked `Documentation/Planning/**` and the chat transcript |
| Platform | Windows, PowerShell |
| Installed SDK | `10.0.401` (only version present) |
| Commands run | `git ls-files`, `git log`, `dotnet build Alify.slnx --configuration Release`, targeted source reads, package/global.json inspection |
| Commands **not** run | No `dotnet test`, no app start, no live provider call, no Doppler call, no Playwright/Genius/Spotify traffic, no secret validity check, no dependency advisory scan, no packaging |

**What this document is not.** This is not G1, and no gate status changes. A successful build is a compile-time result. It does not prove DI resolution, startup, OAuth behavior, skip correctness, SSE isolation, deletion, or any PRD requirement. Those remain untested and no .NET test project exists.

**Secret handling.** `launchSettings.json` was read structurally (key names, lengths, comparison) to confirm the finding. No credential value was recorded, copied, or displayed in this document, and no attempt was made to validate it against the provider. Per OPS-RB-001 that remains a private owner action.

## 2. Build result

`dotnet build Alify.slnx --configuration Release` → **Build succeeded. 18 Warning(s), 0 Error(s)**, ~2m05s.

This resolves one pack assumption in the favorable direction and creates one new problem.

**Resolved:** the pack repeatedly declined to assert that the preview SDK pin works or fails. It works. `global.json` requests `10.0.100-preview.6.25358.103` with `rollForward: latestFeature`, and `10.0.401` satisfies that. All three projects compile to `net10.0` output. AUD-019's "does it build" half is answered.

**New problem:** the build leaves `bin/` and `obj/` artifacts in the tree containing compiled configuration. `launchSettings.json` is tracked in git (not ignored), and its environment variables are injected at build/run time by the SDK into the output configuration. Build output must therefore be treated as secret-bearing until ALI-001 removes the secret. This was not stated anywhere in the pack and belongs in the AUD-001 closure checklist.

Warning inventory (all pre-existing, none introduced by this validation):

| Count | Warning | Location |
| --- | --- | --- |
| 12 | `NU1510` redundant `Microsoft.Extensions.*` PackageReference | `Alify.Core.csproj` |
| 2 | `CS8625`/`CS8600` null-to-non-nullable | `ArtistLyricService.cs:38,52` |
| 4 | `CS8619`/`CS8601` nullability in dictionary types | `DopplerConfigurationProvider.cs:32,49,54` |

The `ArtistLyricService` nullability warnings are on the exact lines the audit calls out in AUD-013 (`mainArtist = null` default, `currentSection = null`). Minor, but they confirm the file is the loosely-typed legacy path the pack treats as evidence-ineligible.

## 3. Claim verification

Each row states the pack's claim, what inspection actually shows, and the verdict.

| # | Pack claim | Verified finding | Verdict |
| --- | --- | --- | --- |
| 1 | AUD-001: client-secret material in `Alify.Spotify/Properties/launchSettings.json` | Both `http` and `https` profiles carry `Spotify__ClientId` and `Spotify__ClientSecret`, each exactly 32 chars, **not** matching common placeholder patterns (`your_`, `xxx`, `<`, `CHANGE`). Identical value in both profiles. | **Confirmed, materially worse than described.** Shape is consistent with a real Spotify client ID/secret pair. Tracked in git across at least 2 commits. |
| 2 | AUD-009: secret-based auth code flow | `SpotifyService.StartAuth` builds `LoginRequest` with `ResponseType.Code` and no PKCE fields; `UpdateAuthAsync` calls `AuthorizationCodeTokenRequest` passing `ClientSecret!`. | **Confirmed exactly.** No `code_challenge`/`code_verifier` anywhere in the repo. |
| 3 | AUD-009: requests playlist-write scopes | `StartAuth` requests `PlaylistModifyPrivate` and `PlaylistModifyPublic` alongside read/playback scopes. | **Confirmed.** Both modify-playback and playlist-write requested, though no playlist feature exists. |
| 4 | AUD-003: skip returns the resulting queue head | `SkipFlaggedSongsAsync` calls `SkipNext()`, then `DequeueTrack()`, then `return queue.CurrentTrack` — the post-dequeue track. `SkipIfFlaggedAsync` then checks that returned track's `IsFlagged` to decide whether to notify. | **Confirmed and mechanism identified.** The event reports the wrong track, and the flagged-check is evaluated against the wrong object. |
| 5 | AUD-004: refresh bypassed for background tokens | `GetSpotifyClientAsync`: refresh runs only `if (accessTokenOverride == null && IsTokenExpired() && ...)`. `RefreshTokenAsync` writes to `Session` only, never to the `SpotifyAuthToken` cache entry set at `UpdateAuthAsync`. | **Confirmed.** Background monitor path never refreshes and never learns of rotation. |
| 6 | AUD-005: logout leaves background authorization | `ClearAuthentication()` removes four `Session` keys only. The `SpotifyAuthToken` cache entry (1h TTL) is untouched. | **Confirmed.** After logout the monitor can still hold a working token for up to an hour. |
| 7 | AUD-007: wildcard CORS, no auth on SSE | `SpotifyEvents.cshtml.cs:24` sets `Access-Control-Allow-Origin: *`. `OnGet` has no session/authorization check; `AddConnection` registers the raw `Response`. | **Confirmed.** Personal playback stream is open to any local or web origin. |
| 8 | AUD-006: sequential SSE fan-out, mutable observer list | `SendSseMessageAsync` iterates `_connections` and awaits `WriteAsync` per connection inline. `_observers` is a plain `List<>` with unsynchronized `Contains`/`Add`/`Remove`. | **Confirmed, plus a defect the pack missed:** write failure is logged but the connection is **not** removed, so a dead client is retried forever. |
| 9 | AUD-002: global cache keys | `_cache.Set("SpotifyUserId", ...)`, `"SpotifyAuthToken"`, and `queue_{userId}` with an 11-minute TTL. | **Confirmed.** `GetUserSpecificCacheKey` exists at line 407 and is **never called** — dead defensive code suggesting this was a known concern. |
| 10 | AUD-010: output cache on session response | `PlaybackController.cs:23` `[OutputCache(Duration = 10)]` on a handler reading `HttpContext.Session`. | **Confirmed as written.** Framework behavior still needs the measurement the pack correctly requires. |
| 11 | AUD-012: explicit-metadata-only flagging | `CreateTrackFromFullTrackAsync`: `var isFlagged = fullTrack.Explicit;`. No model or moderation call in the Spotify flag path. | **Confirmed.** |
| 12 | AUD-013: splits CR and LF separately | `ArtistLyricService.cs:46` `lyrics.Split(['\r', '\n'], StringSplitOptions.None)`; blanks increment `lineNumber` but are skipped before `mapping.AddLine`. `GetNumberedLyrics` has the identical defect at line 123. | **Confirmed.** CRLF input inflates line IDs by one per line; numbering and stored mapping disagree. |
| 13 | AUD-014: first Genius hit + Playwright scrape | `LyricService.GetLyricsAsync` takes `.FirstOrDefault()` from hits and scrapes it; returns `null` for missing key, non-success, or no hits — all indistinguishable. | **Confirmed.** |
| 14 | AUD-015: runtime `GetHashCode` + provider chain | `$"moderation_{lyrics.GetHashCode()}"`; provider loop Gemini → OpenRouter → Mistral with 3 retries each; prompt requests `violence`/`hate`/`profanity`/`sexual`/`suitable_for_kids` booleans. | **Confirmed.** Process-dependent cache key and cross-provider failover both present. |
| 15 | AUD-016: per-call browser, no-sandbox, CSP bypass | `PlaywrightLyricsScraper`: `Playwright.CreateAsync()` + `Chromium.LaunchAsync` **per scrape**, `--no-sandbox` in args, `BypassCSP = true`. | **Confirmed.** |
| 16 | AUD-016: browser closed only on success path | `await browser.CloseAsync()` is at line 79, **inside** `try`. The `catch` at 82 logs and returns `null` with no close. `using` covers `IPlaywright`, not `IBrowser`. | **Confirmed, and worse:** on the exception path the browser process is not closed at all. Leaks a Chromium process per failed scrape. |
| 17 | AUD-017: Doppler invoked in both hosts | Both `Program.cs` call `AddDoppler(builder.Configuration["DOPPLER_TOKEN"])` before `builder.Build()`. | **Confirmed.** Startup is network-dependent whenever the token is set. |
| 18 | AUD-018: Lyrics host lacks explicit `AddMemoryCache` | `Alify.Lyrics/Program.cs` has `AddOutputCache`, `AddHttpClient`, `AddControllers`, `AddHttpContextAccessor` — but **no** `AddMemoryCache`. `LyricService` requires `IMemoryCache` in its constructor. | **Confirmed, and the pack understated the risk.** See §4. |
| 19 | AUD-018: duplicate typed `LyricService` registration | `AddHttpClient<LyricService>(...)` with a handler config at line 59, then a bare `AddHttpClient<LyricService>()` at line 77. | **Confirmed.** Second registration overrides the first; the `SslOptions`/connection-pooling configuration is dead. |
| 20 | AUD-020: CI is demo-oriented, no .NET build | `playwright.yml`: `ubuntu-latest`, `setup-node@v4` with `lts/*`, `npm ci`, `npx playwright install --with-deps`, `npx playwright test`. No `dotnet` step, no test project, no `playwright.config`. | **Confirmed.** |
| 21 | AUD-019: README says .NET 9 | `README.md` states ".NET 9" in Key Technologies and Prerequisites while projects target `net10.0`. | **Confirmed.** |
| 22 | Ops claim: no SQLite package referenced | `Directory.Packages.props` has no SQLite/EF package. | **Confirmed.** SQLite remains genuinely greenfield. |
| 23 | AUD-001 remediation note: not ignored | `launchSettings.json` is **tracked** and `.gitignore` ignores `appsettings*.json` but not `launchSettings.json`. | **Gap in the pack.** The pack notes history review but does not state that the file is tracked and unignored, which is why the secret persists across commits. |

## 4. Two findings that change the plan

### 4.1 The Lyrics host may not resolve `IMemoryCache`

The pack was careful here — AUD-018 says "do not label proven startup failure" and lists it as a validation gap. That caution is correct but the evidence now tips further toward a real defect: `LyricService` has a **constructor** dependency on `IMemoryCache`, and the Lyrics host never registers the memory cache. `AddOutputCache` does not register it.

`IMemoryCache` is not resolved at startup; it resolves when `/LyricsView` constructs its page model. So the failure, if it occurs, is a **runtime request-time error on the main lyrics page**, not a startup crash. That distinction matters: a clean `dotnet run` would not reveal it, and the pack's G1 evidence (host starts, DI resolves) could pass while the feature is broken.

This must be settled by actually starting the host and resolving the page model — not by reading code. Until then it stays a validation gap, but ALI-004 should test the *page model resolution path*, not just host startup, or it will report a false pass.

### 4.2 The `no-sandbox` scraper leaks a browser process per failure

AUD-016 rates this Medium and treats it as "decision to exclude from package." The leak makes it more than a packaging question: `LyricService.GetLyricsAsync` calls the scraper for every uncached track, and the Spotify lane's `BuildTrackAsync` fetches lyrics for up to ten queued tracks. A run of failed scrapes leaves one Chromium process alive per attempt, untracked and unbounded, with `--no-sandbox` active. That is a resource-exhaustion path (SEC-009) reachable from normal operation, not just from scraping.

Either the scraper leaves the initial release (the pack's preferred path — ALI-010, AUD-009/ADR-009 all point that way), or it gets `try/finally` disposal before anyone runs it against a queue. The plan already prefers exclusion, so this reinforces the existing decision rather than changing it. It should not be treated as a Medium cleanup item.

## 5. Where the pack is wrong or incomplete

| Issue | Detail |
| --- | --- |
| Understated: AUD-016 | Scraper leaks a browser process per failed scrape and disables browser sandboxing. Reachable from the normal Spotify queue path. |
| Understated: AUD-018 | Pack frames it as "startup/DI may or may not work." The missing registration has a **constructor** dependency behind it and fails at request time on the main page, which G1's proposed evidence would likely miss. |
| Missed: dead SSE cleanup | Write failures are logged but the connection stays registered. The pack describes slow/failing clients stalling delivery; it does not note the unbounded retry against dead clients. |
| Missed: duplicate handler registration is a real bug | The second `AddHttpClient<LyricService>()` silently discards the first's SSL and pooling configuration. Listed only as a duplicate registration. |
| Missed: dead defensive code | `GetUserSpecificCacheKey` (session-scoped cache key) exists and is never called, while global keys are used throughout. Suggests an unresolved earlier attempt at session scoping. |
| Missed: secret is in tracked, unignored config | `launchSettings.json` is committed and not gitignored. Build output may embed it. Needs to be in the AUD-001 closure checklist. |
| Overstated risk framing, minor | "HTTPS profile still declares an HTTP redirect" (OPS-001) is **not** confirmed — `applicationUrl` for the https profile does not begin with `http://`. Either the finding or my check is wrong; re-verify before relying on it. |

## 6. Gate implications

No gate status changes. But G1 evidence must now include:

1. Actual `dotnet build` output recorded with SDK version (now available; still needs CI pinning per OPS-002).
2. **Request-time** DI resolution for both hosts' page models and typed clients — not just host startup.
3. `launchSettings.json` removed from tracked config and from build output, with the secret rotated or revoked first.
4. A decision on whether `IMemoryCache` registration is missing (fix) or transitively satisfied (no-op), evidenced by a real request.
5. Scraper excluded from the release build, or disposal fixed, before any queue-path test that could trigger it.

## 7. Re-validation trigger

This record is valid for commit `de7bbc3`. Re-run §3 against any change to `Alify.Spotify/Features/Spotify/Services/`, `Alify.Core/Services/ArtistLyricService.cs`, either `Program.cs`, or `Directory.Packages.props`. The build result in §2 does not generalize: it says the tree compiles on this machine on this date, nothing about any other machine, configuration, or runtime path.

## 8. Version history

| Version | Date | Change | Author role |
| --- | --- | --- | --- |
| 0.1 | 2026-10-09 | First executed validation: build succeeded, 23 claims checked, 2 findings escalated, 5 gaps recorded | Engineering/quality role (TBD) |