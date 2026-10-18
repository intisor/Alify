# 08 — Quality and validation

**Status:** Draft  
**Date:** 2026-10-09  
**Owners:** Quality, engineering, security/privacy, accessibility, release, and permissions/policy roles — all TBD  
**Approvals:** Not granted. This document proposes validation; it is not execution evidence or permission to ship.

## 1. Scope and non-negotiable boundaries

Alify remains local-first for a local user, preserving the .NET 10 Spotify playback monitor, queue, explicit-metadata filtering, and server-sent events (SSE). Standalone lyric analysis must use a permissioned resolver or user-supplied material with verified rights. Gemini is optional, bring-your-own-key (BYOK), and requires explicit cloud-processing consent and a bounded budget. Local-first does not mean Spotify can operate offline; unavailable network-dependent features must be clearly distinguished from local analysis and review.

Semantic analysis must not control Spotify playback until the relevant lyric permissions and Spotify/platform policy review are complete and recorded. This gate cannot be waived by a passing model evaluation, OAuth playback scope, a supplied API key, or a user preference. Existing explicit-metadata filtering is a separate behavior to preserve and validate, not a general safety guarantee. No model-generated `ShouldSkip`, suitability boolean, stance, or confidence can authorize playback mutation. Policy evaluation remains independent and the semantic Spotify-control path remains blocked.

This scoped change writes documentation only. No source edits, builds, tests, packaging, live provider calls, or performance measurements were performed. All targets below are proposed and unverified. The companion [semantic evaluation protocol](09_SEMANTIC_EVALUATION_PROTOCOL.md) defines semantic validation separately from operational release quality.

## 2. Repository evidence and current gaps

Paths in this document are relative to the workspace root `Alify`.

| Inspected file(s) | Observed baseline | Validation implication |
| --- | --- | --- |
| `Alify.Core/Alify.Core.csproj`, `Alify.Spotify/Alify.Spotify.csproj`, `Alify.Lyrics/Alify.Lyrics.csproj`; `Alify.slnx` | Three projects target `net10.0`; Spotify and Lyrics use the Web SDK; Core references ASP.NET Core. The solution contains no test project. | Preserve .NET 10. Proposed .NET test projects and their commands do not exist yet. |
| `global.json` | SDK version `10.0.100-preview.6.25358.103`, `rollForward: latestFeature`. | Verify actual SDK selection and document a supported release SDK before packaging; do not infer it from the target framework. |
| `Directory.Packages.props` | Central package versions include SpotifyAPI.Web 7.2.1, SpotifyAPI.Web.Auth 7.1.1, Mscc.GenerativeAI 3.0.2, Microsoft.Playwright 1.44.0, and Microsoft.Extensions packages 10.0.2. | Test selected SDK/package compatibility, SDK serialization, DI, and browser installation. Node and .NET Playwright installations are not interchangeable. |
| `.github/workflows/playwright.yml`, `package.json`, `tests/example.spec.ts`, `tests-examples/demo-todo-app.spec.ts` | Workflow installs Node LTS, runs `npm ci`, installs browsers, invokes Playwright, and uploads a report. Inspected specs visit playwright.dev and its demo TodoMVC, not Alify. No .NET setup/build/test or Alify startup is declared. No Playwright config was found in the file search. | Current CI is demo-oriented, not evidence of product quality. Actual discovery/report contents have not been executed or verified. |
| `Alify.Spotify/Program.cs`, `Alify.Lyrics/Program.cs` | Razor Pages and controllers; singleton queue/SSE/monitor services in Spotify; scoped Spotify service; Doppler added in both hosts. Lyrics service depends on `IMemoryCache`, with no explicit `AddMemoryCache` call in the inspected Lyrics startup. | Exercise service resolution and options validation in both Development and Production; do not declare startup broken or healthy without running it. |
| `Alify.Spotify/Features/Spotify/Services/SpotifyService.cs`; `Pages/Authentication/callback.cshtml.cs` | OAuth stores state in session, removes it before token exchange, uses authorization code with client secret, and refreshes session tokens. Background token override bypasses refresh. | PKCE is a planned requirement, not the current implementation. Negative state, refresh races, and foreground/background expiry need coverage. |
| `Alify.Spotify/Features/Spotify/Services/SpotifyRequestCache.cs`, `QueueService.cs`, `SpotifyService.cs` | Request cache uses global `CurrentlyPlaying` and `Queue` keys; queue cache is keyed by user; global `SpotifyUserId` and `SpotifyAuthToken` also occur. | Account switches and concurrent sessions must prove isolation; single local-user intent does not make global identity state safe. |
| `Alify.Spotify/Features/Spotify/Controllers/PlaybackController.cs` | `/api/Playback/current` has output caching and session-based token checks. | Verify authenticated response cache scope and unauthorized/empty/error responses; do not assume framework defaults provide required isolation. |
| `Alify.Spotify/Features/Spotify/Services/QueueService.cs`; `SpotifyPlaybackMonitorService.cs`; `SpotifyModels.cs` | Semaphore-guarded skip calls `SkipNext`, then mutates queue; returned track is the resulting current track. Monitor interprets a flagged return as skipped. Queue models include empty/index/expiry behaviors. | Locking is not remote idempotence. Verify skipped-track identity, uncertain outcomes, stale snapshots, empty queues, and event semantics. |
| `Alify.Spotify/Features/Spotify/Services/SseService.cs`; `Pages/Spotify/SpotifyEvents.cshtml.cs`; `Pages/Dashboard/Dashboard.cshtml` | Sequential writes to stored responses; endpoint allows `*` origin; dashboard reloads on playback events, alerts on skips, and closes EventSource on error. | Reconnect, slow readers, access control, account scoping, focus retention, and duplicate notifications are unproven. |
| `Alify.Core/Services/ArtistLyricService.cs`; `Models/LyricModels.cs` | One-based numbering; blanks counted but omitted from mapping; annotations retained; splitting on both CR and LF individually can count CRLF as two separators. `Artist` is parsed metadata. | Define canonical logical-line IDs and test CRLF/LF/CR before trusting evidence. Do not infer narrator beliefs or artist identity from that property. |
| `Alify.Lyrics/Features/Lyrics/Services/LyricService.cs`; `Pages/Lyrics/LyricsView.cshtml.cs` | Standalone lyrics page has no Spotify dependency; retrieval selects the first Genius hit and scrapes it; moderation uses booleans, text hash caching, Gemini/OpenRouter/Mistral fallback. | Existing retrieval/moderation is not a permissioned semantic pipeline. Test identity resolution, rights, stable cache keys, and absence of unconsented cloud fallback. |
| `Alify.Core/Infrastructure/Doppler/DopplerConfigurationProvider.cs`; `run-all.ps1`; `run-all.sh`; `README.md` | Doppler is skipped when no token exists, otherwise loaded synchronously; launch scripts announce ports without explicitly passing those port settings or proving readiness. README says .NET 9. | Clean-machine startup must verify actual bindings and readiness, optional-secret behavior, and truthful .NET 10 instructions. |

These are inspection findings, not an exhaustive audit. New tests, permission controls, schemas, packaging, and migrations described below are proposed work.

## 3. PRD traceability and acceptance matrix

Test levels: **U** unit; **C** contract; **I** integration with controlled adapters; **UI** browser/manual accessibility; **S** security/privacy; **F** fault simulation; **R** release/clean-machine. Each row requires a recorded case ID, owner (TBD), expected result, observed result, environment, and artifact reference before acceptance.

| Requirement | Levels | Mandatory scenarios and acceptance evidence |
| --- | --- | --- |
| PRD-001 — local user | I, UI, S, R | Start without cloud keys or Doppler; identify local vs network-dependent features; bind intended loopback interfaces; reject unintended LAN exposure/cross-origin mutation; restart without corrupting preferences. No mandatory hosted account or lyric cloud request. |
| PRD-002 — OAuth PKCE | U, C, I, S, F | S256 verifier/challenge, secure transaction binding, exact approved redirect, minimal scopes; missing/mismatched/expired/replayed state, denial, missing code, duplicate callback and parallel logins; invalid verifier, code replay, token exchange timeout; atomic refresh rotation and logout-vs-refresh. Current secret-based flow does not satisfy this gate. |
| PRD-003 — playback and queue | U, C, I, F, UI | Playing/paused/stopped, no device, unavailable device, premium/scope restriction, episode/non-track, null item, empty queue, repeated track occurrences, shuffle/repeat, seek, device transfer, external skip, queue reorder and track boundary. Display observed queue, never claim ownership of remote order. |
| PRD-004 — explicit filter | U, I, F, UI | Explicit true/false/missing metadata, preference off/on, start/stop, consecutive explicit tracks, no-device/403/429; stale and uncertain skip cases in section 5. Reason says metadata filtering, not semantic endorsement or child safety. No semantic labels feed the filter. |
| PRD-005 — SSE | C, I, UI, S, F | Verify `connected`, `playbackInfo`, `trackSkipped`, UTF-8 framing, content type and flush; reconnect/full resync, sequence/epoch handling, slow reader/disconnect, bounded buffers, account isolation, cancellation and host shutdown. Replayed notifications must never replay control commands. |
| PRD-006 — permissioned lyric resolver | U, C, I, S, F | User import/authorized source, recorded permission scope, matching recording/version/language; competing first hits, remix/live/clean edition, partial lyrics, oversized text, timeout/429/403 and prohibited source. Distinct unavailable, not-found, instrumental, and identity-uncertain states. No scraping fallback solely because an endpoint is reachable. |
| PRD-007 — stance | U, C, semantic, UI | Endorsed/Mentioned/Rejected/Ambiguous and Literal/Figurative/Unknown; negation, quotes, irony, code-switching and changing sections/narrators. Missing text is not a negative label; uncertainty remains visible. Apply protocol 09 rather than universal NLP claims. |
| PRD-008 — evidence | U, C, semantic, UI, S | Exact one-based original-line references including blank/annotation positions; version binding, spans/context, translation alignment and missing/out-of-range IDs; no fabricated quote; minimally retained licensed evidence. Line IDs are not timestamps. |
| PRD-009 — policy separation | U, C, I, S | Model cannot call playback tools, override permissions, or set trusted `ShouldSkip`; malicious lyric instructions and policy fields are rejected. Independent policy remains disabled for semantic Spotify control pending approvals. Explicit metadata behavior remains independently testable. |
| PRD-010 — review disagreement | U, I, UI, semantic | Two original annotations retained, dissent/alternatives preserved after adjudication, correction provenance, permission withdrawal, unsupported confidence and abstention UI. Review changes do not silently mutate playback. |
| PRD-011 — BYOK consent/budget | U, C, I, S, F, UI | No key/no consent/revoked consent => no cloud call; invalid key, provider rejection, rate limit, timeout, retry and concurrent reservations; token/currency cap includes retry costs. Gemini only when enabled; no silent OpenRouter/Mistral fallback; estimates labeled, no raw key/log/excerpt disclosure. |
| PRD-012 — delete | U, I, S, F, R | Delete imports, evidence, reviews, caches and credentials with documented scope; cancel in-flight jobs, reject late writes, restart and verify absence; partial deletion failure surfaced. Explain third-party retention limits; do not claim deletion of already processed cloud copies. |
| PRD-013 — identity/cache | U, C, I, S, F | Local profile + Spotify account + auth epoch scoping for playback; lyric recording/source/version/language/content digest/rights scope and model/prompt/schema version for analysis. Same title/different recording, changed text, permission expiry, switched account, hash collision simulation and concurrent misses cannot serve foreign/stale evidence. |
| PRD-014 — release UX | I, UI, S, R | First-run .NET/runtime configuration, OAuth and consent steps, key protection, offline/degraded states, accessible error recovery, filter stop control, delete, upgrade/downgrade and uninstall. Test packaged hosts on clean machines with no developer secrets/browser caches. |

## 4. Layered test design

### Unit and deterministic contract coverage

Use virtual clocks and controlled cancellation for cache TTLs, token expiry, monitoring delays, budget reservations, job epochs, and backoff. Table-driven/property-style cases should cover queue invariants, empty/index boundaries, line normalization, duplicate IDs, schema bounds and permission checks. Assert decisions and reasons, not just absence of exceptions. Avoid real waiting and provider calls in unit tests.

Contract fixtures must cover Spotify success/204/401/403/429/5xx, missing and additional fields, nullable metadata, unsupported item types, token responses with/without rotated refresh tokens, and SSE JSON/framing. Pin recorded schema versions, redact credentials, and check recording permissions before capturing data. Gemini contract tests validate the chosen SDK request/response behavior with controlled responses, malformed JSON, invalid enums, excessive arrays, fabricated IDs and provider refusal. Reachable model names in existing code are not proof of availability or suitability.

### Integration and startup coverage

Use isolated host instances, temporary local storage and controlled HTTP adapters. Exercise the real middleware, session, DI, background lifecycle and Razor Page handlers without Spotify credentials. Test Development and Production with scope validation/options validation enabled in the future harness; resolve controllers, page models and typed clients, including Lyrics `IMemoryCache`, session storage and singleton/scoped service boundaries. Verify missing optional Gemini/Doppler configuration disables only its feature, while invalid required Spotify configuration produces actionable setup errors before an OAuth request.

Separate read acquisition from optional lyric retrieval: a slow lyric source must not freeze monitoring/queue/SSE health. Account transition tests interleave two sessions and background iterations, then logout/relogin under the same browser cookie. Assert no token, queue, identity, SSE payload or output-cached response crosses the auth epoch.

### UI and accessibility

Target WCAG 2.2 AA for supported release flows, subject to accessibility-owner review. Browser checks cover keyboard-only setup/login/start/stop/review/delete, focus order/visible focus, accessible names, contrast, zoom/reflow, reduced motion, language/direction, and status/error announcements. Manual screen-reader checks cover Windows NVDA and at least one other supported browser/reader combination; automated checks alone cannot certify conformance.

SSE refresh must not steal focus, reset a review draft, produce alert storms, or announce every playback poll. Present permission denial, budget exhaustion, review uncertainty and provider refusal in plain text with recovery actions. Labels must not equate Mentioned with unsafe or Rejected with universally safe. Test encoded lyric/provider/track text, bidirectional text and long strings in Razor and any dynamic DOM updates.

### Security and privacy

Test loopback host binding, allowed Host/Origin handling and DNS-rebinding assumptions; cross-site requests against logout/start/stop/delete/key configuration require appropriate protection even on localhost. Review cookie security, OAuth callback binding, least privilege, output cache behavior and SSE access; `Access-Control-Allow-Origin: *` is not authorization. Inject HTML/attribute/URL/script payloads into every untrusted field and validate context-specific encoding. Resolver URL controls must resist SSRF, redirects to private hosts, path traversal, malicious files and excessive input.

Exercise secret storage permissions, redaction, key rotation and uninstall; search captured logs/reports/traces/crash artifacts for tokens, keys, full lyrics and session identifiers. Separate rights to read, locally analyze, retain evidence, publish excerpts, and send to Gemini. Revocation prevents new processing and invalidates dependent caches. Keep telemetry local/minimal by default, with no unconsented content upload.

## 5. Playback action uncertainty and stale-action protocol

`SkipNext` is a non-idempotent remote action. A local semaphore prevents overlapping local calls only; it cannot make a lost-response retry safe or prevent an external device from changing playback.

Proposed action lifecycle: observed snapshot -> independently authorized explicit-metadata intent -> fresh preflight -> dispatched -> confirmed, rejected, or **outcome unknown** -> reconcile. Associate intent with account/auth epoch, device, recording and playback occurrence, snapshot age and filter revision. A track ID alone is insufficient when repeat is enabled. Spotify may not offer an atomic compare-and-skip operation; residual preflight-to-dispatch races must be disclosed, not claimed eliminated.

| Simulation | Required outcome |
| --- | --- |
| Track/device/account/preference changes during preflight | Drop stale intent; do not skip the new track; reacquire state and record cancellation reason. |
| Response lost after server applies skip; connection reset or timeout | Mark outcome unknown; do not automatically resend `SkipNext`; invalidate optimistic queue, observe remote playback, and show reconciliation state. A later identical track does not prove failure. |
| Request definitely rejected (401/403/429) | No local dequeue/confirmed skipped event; recover authentication or honor rate limit. Any later action requires new observation and authorization, not generic HTTP mutation retry. |
| Two monitor/manual callers or delayed callback | Serialize local intents by account/occurrence and reject duplicate/old epochs; test multiple completions and external device action. |
| Server response received but playback has not advanced | Do not infer exact applied occurrence solely from response; bounded observation window, then unknown/degraded state. |
| Empty queue, consecutive explicit items, repeat-one, stale cached progress | No index exception or unbounded skip loop; rate/burst bounds and visible stop control. Do not erase tracks on a failed or unknown action. |
| Notification after skip | Report the observed skipped occurrence only when supported, not the next queue head. Test current `QueueService` return behavior and monitor event attribution explicitly. |
| Semantic result arrives after track changed or contains `ShouldSkip: true` | No Spotify mutation regardless of confidence; semantic control gate remains blocked. |

Read-only requests can use bounded retries/backoff; playback mutations need operation-specific outcome handling. Any claimed exactly-once remote skip semantics require provider evidence, not local dedup tests.

## 6. Reconnect, backpressure and fault laboratory

Propose a per-account stream epoch/sequence plus an initial authoritative snapshot. On reconnect, either replay a bounded valid history or explicitly perform a full resync; old events cannot replace newer state. Without replay support, document snapshot-only recovery instead of promising `Last-Event-ID` replay. Reconnect uses bounded exponential backoff/jitter and stops on logout. Never turn an SSE event into a playback action.

Simulate a stalled reader alongside a healthy reader, concurrent publishers, duplicate/out-of-order events, malformed event data, network flaps, proxy buffering, interrupted flush, disconnect mid-write and host shutdown. Bound per-client buffers; coalesce replaceable playback snapshots but do not silently drop audit-critical outcome information. Disconnect/resync slow clients when limits are hit. A blocked response must not block every client or the playback monitor. Validate production compression does not hold SSE indefinitely.

Fault suites also cover DNS/TLS failures, provider 401/403/404/429 with Retry-After, 5xx, malformed JSON, browser missing/crash, permission revocation, disk full/read-only, corrupted state, clock shifts, cancelled waits, process kill during writes and budgets, and key removal mid-job. Verify bounded attempts and timeouts, responsive cancellation, no fallback outside consent, minimal error artifacts, and recovery without unsafe action or stale evidence. Live smoke checks, if later authorized, are separate, opt-in and costed; they do not replace controlled faults.

## 7. Proposed measurable targets

Targets require owner approval and baseline measurement on a recorded CPU/RAM/OS/browser, payload size and client count. They are not achieved values, zero-latency promises, or universal semantic accuracy guarantees.

| Area | Proposed pilot/release target | Measurement and limitation |
| --- | --- | --- |
| Critical invariants | All authored negative permission, stale-action, isolation, deletion and schema-validator cases pass; zero observed unauthorized mutations/cloud calls in those cases | Bounded suite evidence, not proof that all possible failures are absent. |
| Deterministic suites | 100% critical-branch assertions; proposed >=80% changed-code branch coverage | Exclude generated/vendor code with rationale; coverage cannot substitute for case quality. |
| Local response | p95 <=250 ms for cached/read-only local responses at 10 local browser clients | At least 1,000 requests after warm-up; disclose cold-start and error results separately; exclude provider waits. |
| Fresh observation to UI | p95 <=1 second after server acquisition under the controlled load | Separately measure provider poll/cache age; the configured 4-second active poll does not guarantee 4-second end-to-end freshness. |
| SSE recovery | Resync within 10 seconds of restored connectivity in >=95% of 100 controlled cycles | Auth-valid controlled network; no promise under outages/provider throttling. |
| SSE resources | Proposed <=100 queued messages per client; healthy-reader p95 delay <=1 second during one stalled reader | 10 clients/30-minute run; report RSS before/after and connection cleanup, not an assumed implementation. |
| Cloud work | Default no calls; enabled jobs bounded by configured deadline, token cap and spend reservation | Suggested 30-second attempt deadline, at most one retry within job budget; costs/unknown usage reconciled conservatively. Provider latency is not guaranteed. |
| Accessibility | No unresolved critical/serious automated findings; all specified manual critical flows usable | Scope and assistive-tech combinations reported; conformance approval is separate. |
| Shutdown/delete | Proposed local completion <=5 seconds for reference-size data, or explicit progress/error | Check in-flight cancellation, persistence and restart; external retention excluded and disclosed. |
| Clean-machine release | Every declared supported OS/package variant passes setup/start/upgrade/delete/uninstall smoke | Support matrix must be approved before it is marketed. |

Semantic precision, recall, false positives, evidence support and abstention/coverage use protocol 09. Any numerical semantic gate must be preregistered after the provisional pilot with sample-size/uncertainty justification; this document invents no improvement percentage.

## 8. CI evolution and future command examples

Current workflow is not a product release gate. Proposed CI stages: (1) pinned SDK/Node restore and dependency/license/secret checks; (2) .NET build, analyzers and deterministic tests; (3) controlled host/contract/fault tests; (4) Alify browser/accessibility tests against explicitly started loopback hosts; (5) clean-machine package and lifecycle checks; (6) separately permissioned semantic evaluation. Never supply production keys to pull requests. Live Gemini/Spotify checks require consent, reviewed permissions, bounded spend and an independently approved trigger. Reports must exclude protected lyrics and credentials.

**Future examples only — none executed.** These examples assume the shell is in the directory containing the workspace folder `Alify`; each .NET command names an existing project or solution explicitly. Confirm SDK selection first; no test-project command is supplied because no .NET test project exists yet.

```sh
dotnet restore Alify/Alify.slnx
dotnet build Alify/Alify.Core/Alify.Core.csproj --configuration Release --no-restore
dotnet build Alify/Alify.Spotify/Alify.Spotify.csproj --configuration Release --no-restore
dotnet build Alify/Alify.Lyrics/Alify.Lyrics.csproj --configuration Release --no-restore
dotnet run --project Alify/Alify.Spotify/Alify.Spotify.csproj --no-launch-profile -- --urls http://127.0.0.1:5001
dotnet run --project Alify/Alify.Lyrics/Alify.Lyrics.csproj --no-launch-profile -- --urls http://127.0.0.1:5000
dotnet publish Alify/Alify.Spotify/Alify.Spotify.csproj --configuration Release --output Alify/artifacts/spotify
dotnet publish Alify/Alify.Lyrics/Alify.Lyrics.csproj --configuration Release --output Alify/artifacts/lyrics
npm --prefix Alify ci
npm --prefix Alify exec -- playwright test Alify/tests/example.spec.ts
```

Run host examples separately and terminate them after future validation. Publish examples produce framework-dependent outputs by default, not approved installers or proven self-contained releases. The Node example exercises only the existing external demo spec; it must not be presented as an Alify UI test. Future test projects/configuration must be added and their exact paths documented before introducing product test commands. Launch scripts' success messages are not health checks.

## 9. Packaging, migration, rollback and deletion

Release validation must use a clean OS user/machine without Visual Studio, user-secrets, Doppler token, preinstalled Playwright browsers or developer HTTPS certificates. Record selected .NET 10 SDK for builds and runtime requirements for shipped artifacts; verify port collisions, loopback binding, signing/checksums where appropriate, firewall prompts, filesystem permissions and offline startup. If browser scraping is retained only after permission review, package/install its matching .NET Playwright runtime explicitly; Node CI browser installation proves nothing about the .NET package.

No persistence migration system was established by this inspection. Before adding one, define versioned local data, rights records and cache formats. Rehearse fresh install and upgrade from each supported predecessor with consent/key settings intact but semantic/cloud features never newly enabled by migration. Cover malformed/partial old data, insufficient disk, interrupted writes and resumable/atomic behavior. Preserve original line-ID/source version mappings or invalidate analysis rather than relabel old evidence.

Rollback gates: take a user-approved minimal backup with documented rights/secret protections; prove restore or supported downgrade; fail safely on newer incompatible schema; clear incompatible disposable caches; prohibit rollback from resurrecting deleted/revoked records. Record destructive migration warnings and recovery procedures before release. Do not claim transactional rollback before implementing and exercising it.

Deletion gates: distinguish logout, disconnect/revoke, delete-analysis, delete-all-local-data and uninstall. Cancel jobs, advance deletion/auth epochs, remove scoped memory/disk/output caches and protected keys, verify no late response recreates data, then restart and verify. Explain backups/exported reports and third-party retention separately; secure erasure of underlying media is not guaranteed by file deletion. Permission withdrawals must also propagate to fixtures and retained evidence.

## 10. Release gates and evidence ledger

| Gate | Required evidence | Approver role |
| --- | --- | --- |
| G0 — scope/rights | Approved feature boundaries; resolver use/retention/cloud/publication rights; Spotify/platform review for any semantic control | Permissions/policy + product, TBD |
| G1 — build/startup | Recorded .NET 10 SDK/package matrix; both hosts resolve DI/start correctly; missing optional services degrade safely | Engineering, TBD |
| G2 — core behavior | PRD-001–005, 009, 013 tests; uncertain/stale skip, refresh race, account and SSE isolation; no open critical defects | Quality + engineering, TBD |
| G3 — standalone semantics | Permissioned data, integrity validator, protocol 09 report and visible abstention/review limits; model has no control authority | Semantic evaluation + permissions, TBD |
| G4 — privacy/security/accessibility | Consent/budget/delete checks, secret/SSRF/XSS/CSRF review, scoped accessibility evidence and unresolved-risk ledger | Security/privacy + accessibility, TBD |
| G5 — lifecycle/release UX | Clean-machine packages, first-run docs, migration/rollback/delete/uninstall evidence and known-issue notes | Release + quality, TBD |

A standalone-analysis release does not unlock semantic Spotify control. That capability stays blocked until G0 specifically approves it and a separate independent-policy/action validation is completed; no approvals are granted here. Existing metadata filtering also needs reviewed scopes and ordinary core release gates.

For every gate retain commit identifier, exact commands/case IDs, dependency/model versions, environment, rights record references, results (pass/fail/not run), redacted artifacts, defect severity and risk acceptance. Roles and concrete names remain TBD. Critical unauthorized action/data disclosure/permission bypass or unknown skip automatically retried is a release blocker; flaky tests cannot be silently ignored. Any exception must identify scope, owner, expiration and approver, and cannot substitute for missing rights. At this draft's date, all proposed gates are **not assessed**, not passed.
