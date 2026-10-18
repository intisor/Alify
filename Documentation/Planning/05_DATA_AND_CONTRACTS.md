# 05 — Data and contracts

**Status:** Draft for engineering review  
**Date:** 2026-10-09  
**Document ID:** DATA  
**Scope:** Proposed local data design and versioned contracts. No schema, interface, endpoint, or migration described as proposed is asserted to exist. No approval or legal clearance is claimed.

Companions: [Architecture and integration](04_ARCHITECTURE_AND_INTEGRATION.md), [Infrastructure and operations](06_INFRASTRUCTURE_AND_OPERATIONS.md). PRD identifiers use the agreed planning requirements; ARCH provides the complete requirement legend.

## 1. Actual baseline and compatibility constraints

Inspected current source, relative to the `Alify` workspace root:

- `Alify.Core/Models/LyricModels.cs`: mutable `LyricLine` with `LineNumber`, `Text`, `Artist`, `Section`, `IsAnnotation`; mutable `LyricMapping` rejects duplicate line numbers but has no immutable content version or provenance.
- `Alify.Core/Services/ArtistLyricService.cs`: splits CR/LF separately, trims text, counts blanks but omits empty mapped entries. Existing results must retain their legacy parser designation; they are not evidence for the new ledger.
- `Alify.Core/ApiKeys.cs`: `LyricsModerationResult` category booleans, `suitable_for_kids`, default confidence, optional context/time. It is not an evidence-backed stance analysis; absent fields/default values must not be treated as validated conclusions.
- `Alify.Lyrics/Features/Lyrics/Services/LyricService.cs`: artist/title lyric cache and process-dependent `GetHashCode()` moderation cache; result can be null after provider failures. No structured distinction among all proposed statuses exists in this path.
- `Alify.Spotify/SpotifyModels.cs`: `Track` embeds `FullTrack`, lyrics, mapping, and `IsFlagged`; `MusicQueue` is mutable and time-expiring; `SpotifyPlaybackInfo` holds current track/queue/remaining time.
- `Alify.Spotify/Features/Spotify/Services/SpotifyRequestCache.cs`: in-memory playback/queue keys, 50-second request cache, artist/title lyric lookup. `SpotifyService.cs` and `QueueService.cs` also cache track/queue objects and session/background tokens.
- Inspected projects have no SQLite package reference or persistent schema. SQLite below is a proposed new local dependency requiring implementation review.

**DATA-000 — Migration rule:** Preserve source and legacy endpoint paths with explicit adapters. Legacy booleans cannot be upgraded into stance, policy, evidence, or action success. Do not serialize the new model as `ShouldSkip`, and do not use legacy `IsFlagged` as the new analyzer output. A disabled or unavailable compatibility operation returns a documented error, not invented safe defaults. [PRD-007, PRD-008, PRD-009]

## 2. Identity, source versions, and resolution

### DATA-001 — Recording identity [PRD-013]

`RecordingIdentity` is a local immutable ID plus descriptive metadata: title, ordered credited artists, release/album, duration if known, version label (clean/explicit/live/remix/remaster), optional ISRC, optional provider recording identifiers, and identity provenance. Title/artist strings are search hints, not unique keys. ISRC and provider IDs are evidence, not a guarantee that all editions share lyrics. Store match candidates and reasons; ambiguous clean/explicit or live/studio matches require user confirmation or `Mismatch` rather than automatic reuse.

A standalone import may have a user-declared identity with unknown fields. It remains distinguishable from a provider-confirmed recording. Spotify identifiers stay in the official lane in initial scope; do not use them to join or transmit semantic results. Schema capability for provider identifiers does not authorize such a bridge.

`recordingId`, `sourceId`, `sourceVersionId`, `rawContentHash`, and `canonicalContentHash` are distinct. The same lyric text may belong to multiple recordings; two lyric editions for one recording may have different hashes. Deduplication must preserve identity/provenance links and deletion ownership.

### DATA-002 — Permissioned source and `LyricResolution` [PRD-006, PRD-012, PRD-013]

Proposed source record: `sourceId`, source kind (`UserPaste`, `UserFile`, future `PermissionedProvider`), provenance reference, permission basis/attestation reference, allowed purposes (local analysis, retention, export, external processing), attribution obligations, expiry/revocation if applicable, acquisition time, and associated recording match evidence. Attestation is a recorded user statement, not a legal finding; provider permission must be separately verified. Only collect the minimum rights evidence needed, avoid unrelated personal data, and never infer processing rights from technical availability.

Exact terminal resolution statuses:

| `LyricResolution` | Meaning | Allowed next step |
| --- | --- | --- |
| `Found` | Permissioned nonempty lyric content accepted for a confirmed or explicitly declared recording match. | Freeze source version; analyze within granted scope. |
| `NotFound` | Completed permitted lookup/import has no lyric source. | Show absence; allow another permissioned import. No safety inference. |
| `Instrumental` | Affirmative, attributable evidence confirms no sung lyrics for the recording. | Show provenance; no fabricated empty-text analysis. |
| `Mismatch` | Candidate does not match or match ambiguity is unresolved. | Resolve identity explicitly; do not analyze for the requested recording. |
| `ProviderUnavailable` | Permitted provider operation cannot complete (timeout, rate limit, authorization/service failure). | Explain availability; bounded read retry if appropriate, never label as `NotFound`. |

Permission/input validation failure is a typed request error before resolution, not a sixth resolution status. Cancellation is recorded on the job, not converted into `ProviderUnavailable`. `Instrumental` is never inferred merely from an empty response. Store diagnostic error codes without upstream response bodies or secrets.

### DATA-003 — Immutable source ledger and evidence [PRD-008, PRD-013]

1. Preserve original import bytes where permission permits, including encoding/newline information. Compute `rawContentHash = SHA-256(originalBytes)`; preserve text fidelity without HTML rendering or destructive trimming.
2. For canonical text, a versioned normalizer decodes approved encoding, handles BOM according to its declared rule, and changes CRLF/CR to LF without inserting extra lines. No whitespace trimming or Unicode normalization occurs unless separately versioned and mapped back to source. Compute `canonicalContentHash = SHA-256(UTF-8(canonicalText))` and record the normalizer version. Trailing newline handling must be fixed in tests, not left to platform `Split` defaults.
3. For plain text, assign each original logical source line a stable **one-based** `sourceLineId`, including blanks and section headers. Display filtering never compacts numbering. Ledger key is `(sourceVersionId, sourceLineId)`; a line number alone is not globally unique.
4. For LRC, number original logical source lines, retaining raw timestamp syntax. Store cleaned lyric text and a reversible mapping to raw text; store each timestamp separately in `LineTiming`. Multiple timestamps on one line point to the same source line. Unsupported timed formats are rejected until their source-mapping parser exists; timestamps are never line IDs.
5. Preserve `rawText`, canonical evidence text, `SourceLine.kind` (canonical enum exactly `Lyric`, `Blank`, `Section`, `Metadata`), artist/section as parsed annotations, and mapping metadata. Parsed performer/speaker attribution is uncertain interpretation unless explicitly supported; it is not automatically the singer's belief.
6. Source edits produce a new immutable version with `parentVersionId`; references to the old version remain valid. New policy/review cannot silently point at old evidence. Corrections to parsing create a new parser rendition/version, not mutation of the ledger.

Example: `[Verse]`, blank, `First lyric`, `[00:12.30]Second lyric` retain IDs 1, 2, 3, 4. Hiding blank/header lines still shows lyric IDs 3 and 4; the fourth line has separate timing `12300 ms`. CRLF and LF forms have different raw hashes but the same canonical text/hash under the specified normalizer. Recording identity is unaffected by either hash.

Evidence reference fields: `sourceVersionId`, `canonicalContentHash`, `parserVersion`, `sourceLineIdStart`, `sourceLineIdEnd`, exact `excerpt`, optional character spans, context line IDs, and a bounded rationale. Character offsets use Unicode scalar values in canonical evidence text, never a mix of UTF-16 indices and byte offsets. For multi-line evidence, join the referenced evidence-text lines with LF including intermediate blanks. Validate both endpoints, ascending range, excerpt/span equality, source version/hash, and context membership. Citation to a non-lyric/header line alone cannot substantiate a lyric stance claim.

Treat imported text and model output as untrusted data, never instructions. Strictly validate allowed fields, size, enums, references, and excerpts before persisting a usable analysis. Invalid output is quarantined only as bounded local diagnostic metadata, not promoted to an `Allow` decision; raw provider output retention is off by default. Prompt injection in lyrics grants no tools, network access, or command authority.

## 3. Interpretation, review, and policy contracts

### DATA-004 — `AnalysisResult` [PRD-007, PRD-008, PRD-011]

Exact terminal analysis statuses:

| `Analysis` | Meaning |
| --- | --- |
| `Completed` | Valid analysis for the declared scope with all required evidence and limitations; does not mean safe, exhaustive, or policy-approved. |
| `InsufficientContext` | Source/context is too incomplete or ambiguous to support the requested interpretation. |
| `UnsupportedLanguage` | Requested language is outside the shipped declared capability. |
| `Unavailable` | Analyzer cannot run (disabled provider, missing consent/key/budget, service failure, unavailable local capability). Includes reason code. |
| `InvalidOutput` | Analyzer returned malformed/schema-invalid data, unsupported references, fabricated excerpts, or failed integrity checks. |

Proposed fields: `analysisId`, source/recording references, status/reason code, detected/requested language and support declaration, engine kind (`LocalRules`, `Gemini`), parser/rules/prompt/schema/provider/model versions, execution time, findings, limitations, and optional usage summary. A non-completed result never masquerades as an empty clean analysis. Lifecycle (`Pending`, `Running`, `Finished`, `Cancelled`) is a separate job field; cancelled jobs have no fabricated terminal result.

Each finding has `findingId`, topic, `stance` (canonical enum exactly `Endorsed`, `Mentioned`, `Rejected`, `Ambiguous`), `expression` (canonical enum exactly `Literal`, `Figurative`, `Unknown`), bounded rationale, evidence references, uncertainty/limitations, and optional confidence explicitly labeled as an estimate. `Endorsed` denotes endorsement/glorification; `Mentioned` denotes mention/description without endorsement; `Rejected` denotes criticism/rejection; `Ambiguous` denotes unresolved stance. Optional `expressionSubtype` (for example, metaphor, hyperbole, or satire) and quoted narrator/character attribution are metadata, not additional enums or enum values. Topic, stance, expression, and profanity detection are different attributes. No claim that a character statement is the artist's personal belief; quoted speech alone does not establish stance. A claim requiring evidence cannot be emitted without valid citations. An `Ambiguous` stance or `Unknown` expression is valid when honestly supported; a completed result can still require policy abstention.

There is **no model `ShouldSkip`**, and no playback/action field in `AnalysisResult`. A provider model version alias is insufficient for guaranteed reproducibility: record the exact revision when available, otherwise record alias plus observation date and limit cache validity; warn that upstream behavior can change.

### DATA-005 — `ReviewRecord` and disagreement [PRD-010, PRD-008]

Append-only fields: `reviewId`, `analysisId`, optional `findingId`, `sourceVersionId`, installation-local reviewer identity (not a cloud account), `reviewRevision`, `expectedPriorRevision`, disposition (`Agree`, `Disagree`, `NeedsContext`, `Unresolved`), rationale, alternate evidence references, timestamp, and optional `supersedesReviewId`. Evidence uses the same integrity validation. Review requires an explicit save; viewing a result is not agreement.

Original findings remain visible. Revision conflict returns HTTP 409 and latest revision metadata; no last-writer-wins overwrite across tabs. Review correction creates another record. Policy refers to the exact effective review revision; subsequent reviews make prior recommendations stale rather than rewriting history. An unresolved disagreement defaults to `Abstain`; user preference cannot erase the disagreement label.

### DATA-006 — Independent `PolicyDecision` [PRD-004, PRD-009, PRD-010]

Exact decision values: **`Allow`, `RecommendSkip`, `Abstain`**.

Proposed fields: `decisionId`, `context` (`StandaloneLyrics` or `SpotifyExplicitMetadata`), input fingerprint, analysis ID/review revision when applicable, source version or official metadata snapshot ID as appropriate, preference revision, policy rules version, decision, reason codes, evidence links where permitted, evaluated time, and freshness/expiry. Policy is deterministic and side-effect free; version preferences and rules separately from analysis.

- `Allow` means no recommendation to skip under this policy and available inputs, not a certification of harmlessness.
- `RecommendSkip` is a recommendation, not a sent command or promise that Spotify skipped.
- `Abstain` covers unavailable/invalid analysis, unsupported language, unresolved identity/context/review, stale inputs, unknown explicit flag, and policy boundary failures. Default behavior is no automatic action and a visible explanation.
- `StandaloneLyrics` decisions remain standalone. `SpotifyExplicitMetadata` evaluates only official explicit metadata and user opt-in; it has no analysis/evidence foreign key. DB checks and contract tests enforce the context distinction.

A preference change recomputes policy, not analysis. Review supersession changes the effective policy input; old decisions remain historical. Future semantic Spotify decisions are blocked, not a third initial context.

### DATA-007 — Playback observation and action journal [PRD-003, PRD-004, PRD-012]

`PlaybackSnapshot`: local snapshot ID, account authorization generation, process/playback generation, device ID where available, official recording ID, minimal display metadata, explicit flag (`true`, `false`, or unknown), playback state/progress, observed time, age, queue observation, and capability/error codes. No lyric payload. Queue ordering is observational. Use monotonic elapsed time internally for deadlines; UTC timestamps for persisted diagnostics and presentation.

`PlaybackCommand`: `actionId` (locally unique), intended command, origin (`Manual`, `OptedInExplicitFilter`), independent explicit-policy decision ID if applicable, expected snapshot/device/recording/playback generation, requested time, and expiry. A local idempotency key suppresses duplicate dispatch **inside Alify only**; do not assume Spotify supports an equivalent remote idempotency key.

`PlaybackAction`: exact states **`Requested`, `Succeeded`, `Failed`, `OutcomeUnknown`, `CancelledStale`** plus `dispatchIntentAt`, acknowledgement time/code, observed effect/basis, reason code, and correlation ID. Persist request/dispatch intent before network send. Transitions: `Requested` → one of the other four; evidence-based reconciliation of `OutcomeUnknown` is appended as history and cannot invent causal certainty. Terminal journal data is not a mutable queue flag.

`Succeeded` records a definitive acknowledgement of command acceptance and its basis; effect confirmation is separate. Reports/UI display acceptance and observed effect separately and must not label `Succeeded` alone as an observed skip. `Failed` needs a definitive rejection/non-dispatch failure. Pre-dispatch account/device/recording/generation mismatch, expiry, disconnect, or cancellation is `CancelledStale`. Timeout/connection loss after possible send, ambiguous response, or crash with unresolved intent is `OutcomeUnknown`. Never automatically retry skip. Reconciliation may perform bounded reads; a later different song could reflect a manual/natural transition and does not prove Alify caused it. After reconnect/restart, stale commands are not replayed. Preserve unknown-action UI until acknowledged; a user can deliberately create a new command only after seeing a fresh snapshot and uncertainty warning.

## 4. Hashes, cache, consent, and budget

### DATA-008 — Cache key/version specification [PRD-013, PRD-009]

Use SHA-256 over a canonical, length-delimited encoding with named fields and explicit null markers; never concatenate ambiguous strings, use runtime `GetHashCode()`, or key correctness on title/artist alone. Store the unhashed version metadata locally for explanation (not in logs). Proposed formulas:

- Resolution key: hash(recording identity revision, provider/source identity, permission revision, resolver version, match-rules version). Permission expiry/revocation invalidates even an otherwise unexpired hit.
- Analysis key: hash(recording identity revision, source version, canonical content hash, normalizer version, **parser version, rules version, prompt version, provider and model version**, analysis schema version, language, analysis options). Local analysis explicitly marks prompt/provider/model as `NotApplicable`; no field is silently omitted.
- Policy key: hash(context, accepted analysis fingerprint or official metadata snapshot, effective review revision, **policy rules version**, preference revision). Policy is a separate cache/table, not included as a hidden decision in the model result.
- Playback read key: hash(authorization generation, official recording/device/playback generation, requested metadata operation). Never use these hits for a fresh command precondition check.

Proposed initial limits: 64 MiB bounded in-memory caches; successful resolution revalidated after 24 hours or earlier rights expiry; `NotFound` 15 minutes; `Mismatch` five minutes pending correction; provider errors at most 30 seconds; complete validated analyses reusable for 30 days under matching versions and permission, otherwise recompute explicitly; invalid output not reused as analysis; policy expires on any input change. Playback snapshots expire for display after five seconds and are visibly stale; fresh command checks use uncached observations with the ARCH two-second freshness limit. All limits are tuning proposals; measure before changing. Database history retention is distinct from cache TTL. Single-flight identical analysis jobs share work only within the same permission and consent context.

### DATA-009 — BYOK consent and budget ledger [PRD-011, PRD-012]

`ProviderConsent`: provider/model, disclosed fields and excerpt/full-text scope, purposes, permission basis, retention/region limitations as known, consent version, accepted time, revoked time, and source scope. Consent must explain that lyric text/context leave the device and are subject to provider processing; do not promise zero retention or training exclusion without verified terms. No Spotify content/metadata is included. Gemini-only initial optional adapter; legacy OpenRouter/Mistral fallback is not carried into the new workflow.

`BudgetSettings` and `ProviderUsage`: user-defined request/token limits and optional spending cap with currency/price schedule version; reservation ID, bounded input estimate, configured output maximum, worst-case estimate, actual/estimated usage, reconciliation status. No request if key/consent/price basis or required capacity is unavailable. Reserve transactionally before dispatch to prevent concurrent overspend; one provider request in flight initially. Unknown provider outcome keeps reservation until reconciliation or an explicit conservative user decision; do not immediately release and resend. Application limits are estimates, not a guaranteed external bill cap; direct users to provider-side quotas/limits where offered. Cancellation cannot guarantee a submitted request is unbilled or withdrawn. Never silently retry a chargeable uncertain request or switch providers.

Secrets are never stored in these tables: keep only opaque credential-store references. Revocation disables future dispatch, cancels undispatched work, and removes key references according to the user's choice. Already transmitted data requires provider-specific deletion guidance; local deletion cannot guarantee remote deletion.

## 5. Proposed HTTP and SSE contracts

### DATA-010 — Versioning and migration [PRD-002–PRD-012, PRD-014]

Actual endpoints/pages inspected:

| Actual path | Current behavior/source |
| --- | --- |
| `GET /api/auth/status`, `GET /api/auth/login`, `POST /api/auth/logout` | `AuthController.cs`: session status, login URL, session logout. |
| `GET /callback` | Razor alias in Spotify `Program.cs`; code/state handler in `callback.cshtml.cs`. |
| `GET /api/Playback/current` | `PlaybackController.cs`: session token check, 200/204/401; output-cache attribute. |
| `POST /api/spotify/start-monitor`, `POST /api/spotify/stop-monitor`, `GET /api/spotify/monitor-status` | `SpotifyController.cs`: monitoring control/status. |
| `GET /SpotifyEvents` | Razor alias and SSE handler; current `connected`, `playbackInfo`, `trackSkipped` event names in service. |
| `GET /LyricsView` | Lyrics Razor alias; artist/track resolver page, not a permissioned import API. |

New paths below are **planned examples**, not routes found in source. Lyrics endpoints belong to the Lyrics host; Spotify endpoints to the Spotify host. Browser clients do not share authentication merely because both hosts are local.

| Proposed `/api/v1` contract | Request/result |
| --- | --- |
| `POST /lyric-imports` | Permission scope, declared recording identity, bounded text/file and format → 201 immutable source version or typed validation error. |
| `POST /lyric-resolutions` | Identity plus permitted source reference → typed `LyricResolution`; asynchronous provider work may return 202 job ID. |
| `POST /analyses`, `GET /analyses/{id}` | Immutable source/options/consent reference → 202 job ID; result only after validation. |
| `GET /jobs/{id}`, `POST /jobs/{id}/cancel` | Separate lifecycle/status, bounded cancellation; never manufacture result status. |
| `POST /reviews` | Expected revision, disposition/rationale/evidence → appended review or 409 conflict. |
| `POST /policy-evaluations` | Context plus input/preference/review revisions → independent decision; blocked mixed contexts rejected. |
| `GET /spotify/playback`, `GET /spotify/queue` | Minimal official-lane observations with freshness/capability status; no lyrics. |
| `POST /spotify/playback-actions`, `GET /spotify/playback-actions/{id}` | Locally idempotent command → 202 journaled action / current action status. No success inferred from 202. |
| `POST /spotify/disconnect` | Stop/cancel, clear local auth/caches/SSE authorization → explicit deletion/unknown-action summary. |
| `POST /provider-consents`, `POST /provider-consents/{id}/revoke` | Versioned Gemini consent and disabling of future processing; secrets handled in a protected non-echoing credential flow. |
| `POST /local-deletions`, `GET /local-deletions/{id}` | User-confirmed scope → deletion progress/result including backup/export limitations. |
| `GET /events` | Authenticated SSE stream described below. |

Shared envelope: `schemaVersion`, local resource ID, UTC timestamp, correlation ID, typed payload. Errors use Problem Details with stable `code`, safe message, retryability of **reads** only, and correlation ID; no stack traces, credentials, provider bodies, or lyric text. Semantic absence is a typed result, not HTTP 500. Authentication/authorization failures use 401/403; revision conflict 409; invalid imports/schema 400/422; size limit 413; budget/rate/capacity failures have distinct codes. Proposed import cap 1 MiB and 10,000 logical lines; reject unsupported/binary content. Cancellation routes are mutations and require CSRF protection.

Keep existing paths and UI links during migration. New UI uses `/api/v1`; legacy adapters maintain fixture-tested shape where safe, document disabled blocked behavior, and never convert null/error/default bool into `Allow` or `Succeeded`. Authenticated personal/current playback responses are `Cache-Control: no-store`; explicitly remove/bypass output caching in planned adapters. Additive optional fields can remain v1; new required fields/changed meaning/statuses require v2. Publish schema fixtures in future tests, establish a compatibility window before any deprecation, and include schema/API versions in diagnostics. No endpoint is removed by this document.

### DATA-011 — Event contract [PRD-005, PRD-003, PRD-010]

Proposed names: `snapshot`, `analysisCompleted`, `reviewChanged`, `policyChanged`, `playbackActionChanged`, `reset`, and `heartbeat`. Each host emits only events from its authorized lane. Event envelope: `schemaVersion`, `eventId` (`epoch:sequence`), `resourceId`, resource revision, `occurredAt`, correlation ID, and bounded summary. Fetch detailed evidence through authenticated APIs; avoid full lyrics or credentials in events.

ARCH-016 specifies bounded channels, limits, replay window, and deadlines. Ordering is per stream/epoch, not a distributed total order. Client applies resource revisions and ignores duplicates; after reset fetch snapshots/journal since cursor. Persisted actions/reviews are authoritative even if delivery is dropped or stream closes. Legacy event names remain only in compatibility adapters; `trackSkipped` must not falsely claim success for `Requested`/`OutcomeUnknown`. Compatibility and privacy may require disabling that event with a documented migration note rather than inventing an executed outcome.

## 6. Local schema and lifecycle

### DATA-012 — Proposed SQLite schema [PRD-001, PRD-008–PRD-013]

Use one installation-local database with lane ownership checks, foreign keys enabled, transactional writes, serialized migration/writer coordination across the two hosts, and parameterized SQL. A small shared repository layer is not a network service. Database access strategy/package is an implementation choice; SQLite is not currently referenced. Avoid introducing EF/Redis as requirements.

| Proposed table | Key relationships and constraints |
| --- | --- |
| `Recording`, `RecordingRevision`, `RecordingIdentifier` | Stable local ID, immutable metadata revisions; identifier provenance/namespace; indexes for candidates, not title uniqueness. |
| `PermissionGrant`, `LyricSource`, `SourceVersion` | Grant scope/expiry, recording revision, parent version; raw/canonical hashes, format/encoding/normalizer; immutable accepted content. |
| `SourceLine`, `LineTiming`, `ParserRendition` | PK `(sourceVersionId, sourceLineId)` with ID >= 1; timings separate and >= 0; versioned parsed annotations/text mapping. |
| `ResolutionAttempt`, `MatchEvidence` | Exact status and source/provider/match references; validation distinguishes empty/mismatch/unavailability. |
| `Analysis`, `Finding`, `EvidenceReference` | Immutable result/version fingerprints; evidence foreign keys to the exact source version/line; status and integrity checks. |
| `Review` | Analysis/finding references, unique effective revision chain; optimistic concurrency and supersession, no model overwrite. |
| `PreferenceRevision`, `PolicyDecision` | Versioned rules/input fingerprint; context check excludes lyric inputs from Spotify explicit-only decisions. |
| `PlaybackAction`, `PlaybackActionEvent` | Unique local action ID, dispatch intent and append-only transitions; metadata only; snapshots normally ephemeral. |
| `ProviderConsent`, `BudgetReservation`, `ProviderUsage` | Consent revision/scope, atomic reservations and settlement, opaque credential references only. |
| `Job`, `DeletionOperation`, `SchemaMigration` | Lifecycle, cancellation/deletion checkpoints, monotonic schema version and migration checksum. |

Indexes: source/analysis fingerprints; `(sourceVersionId, sourceLineId)`; analysis ID/review revision; action ID/state/time; permission expiry; job lifecycle; consent ID/reservation state. Query projections retrieve summaries without loading all lyric/evidence text. Uniqueness prevents duplicate analysis insertion for an exact valid fingerprint, not reuse across incompatible rights. Use transactions for import acceptance, validated analysis/evidence insertion, review/policy revision updates, budget reservations, and dispatch-intent journaling. OPS defines backup/migration safety and disk-failure behavior.

### DATA-013 — Retention, deletion, and disconnect [PRD-012, PRD-001]

| Data | Proposed default lifecycle |
| --- | --- |
| Imported lyrics, permission/provenance, accepted analyses and reviews | Local until user deletes or applicable rights expire/revoke. Expiry disables processing immediately; delete restricted content/descendants according to recorded obligations, retaining only minimal non-content audit where justified and disclosed. |
| Derived policy/cache | Invalidate on source, identity, permission, rule, preference, review, or auth change; remove descendants on source deletion. |
| Spotify queue/current snapshots | Memory only, short TTL; clear on disconnect/account generation change/restart. |
| Action journal | 30 days locally by default, minimal identity/metadata; user can delete sooner. Pending unknown dispatch is not replayed after deletion. |
| Consent/usage ledger | 30-day usage history by default; current consent while enabled; retain unresolved budget reservation conservatively until settled or explicitly disposed. No lyric/provider response payloads in usage records. |
| Logs | Seven days, bounded size; opt-in diagnostic export; no content or secrets. |
| Backups | At most three local snapshots/30 days by default, subject to rights and deletion policy; no credentials. |

Source deletion cancels dependent work, prevents late-result persistence via a deletion generation/check, removes evidence/analysis/review/policy descendants transactionally, clears caches and source blobs, and checks WAL/journals/backups. Disconnect stops monitoring and undispatched commands, invalidates the auth generation, drops SSE authorization, deletes tokens from OS store and memory, and reports any already-dispatched `OutcomeUnknown` action; disconnect is not inherently deletion of unrelated standalone imports. Offer separate erase-all, provider-key deletion, and selected-source deletion flows with plain-language scope confirmation.

Backups containing deleted content must be deleted/rotated under the same operation or explicitly reported as pending; a restore must not silently resurrect previously deleted data. Maintain a minimal deletion ledger outside old backup snapshots for reconciliation on restore. User-created exports and provider-held content cannot be automatically recalled; explain this. Secure physical erasure cannot be guaranteed on SSDs, OS snapshots, or third-party backups. Do not call SQLite/ordinary file deletion cryptographic erasure. Use OS account permissions and recommend full-disk encryption; if stronger threat protection is required, review database encryption and key lifecycle separately.

## 7. Acceptance fixtures and unresolved implementation choices

| ID | Planned test/acceptance evidence | Requirements |
| --- | --- | --- |
| DATA-T01 | LF/CRLF/CR, trailing newline, blank/header, Unicode spans, LRC multi-timestamp fixtures; hidden lines never renumber. | PRD-008 |
| DATA-T02 | Different recordings same text and same recording changed text; clean/live/remaster ambiguity; content and recording hashes independent. | PRD-006, PRD-013 |
| DATA-T03 | Every resolution/analysis/decision/action status separately serialized and rendered; unknown enum rejected; cancellation kept on jobs. | PRD-003, PRD-006, PRD-007, PRD-009 |
| DATA-T04 | Fabricated excerpt/range/hash/parser version rejected; exact stance (`Endorsed`/`Mentioned`/`Rejected`/`Ambiguous`) and expression (`Literal`/`Figurative`/`Unknown`) serialization/contrast cases; subtype and narrator attribution remain metadata; no `ShouldSkip` field. | PRD-007, PRD-008, PRD-009 |
| DATA-T05 | Review conflict/disagreement persists; preference edits only invalidate policy; Spotify context cannot reference analysis. | PRD-009, PRD-010 |
| DATA-T06 | Version changes invalidate cache; permission expiry and consent revocation override cache; concurrent budget reservations cannot oversubscribe. | PRD-011, PRD-013 |
| DATA-T07 | Duplicate/stale/timeout/crash action tests; no skip retry; SSE reset recovers authoritative action history. | PRD-003, PRD-005 |
| DATA-T08 | Delete during analysis, locked credentials, WAL/backup restore, and disconnect during dispatch; no late persistence or automatic replay. | PRD-001, PRD-002, PRD-012 |
| DATA-T09 | Keyboard/screen-reader status/evidence/unknown-action flows and legacy adapter contract fixtures. | PRD-014 |

Implementation review must choose SQLite library/access pattern, source blob strategy (initial proposal: transactional DB blobs within the import cap), exact schema files, supported source formats/languages, provider price/version lookup, and API compatibility window. These are not completed deliverables. This task did not execute the application, providers, builds, or tests.
