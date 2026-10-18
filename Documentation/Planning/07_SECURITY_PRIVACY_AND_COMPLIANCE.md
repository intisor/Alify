# 07 — Security, Privacy and Compliance Design

**Document ID:** SEC · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Draft threat/control specification; not legal advice, certification, policy clearance, or completed security test
**Accountable owners:** Security/privacy and permissions roles, TBD · **Approvals:** Not granted

Companions: [Audit](02_CURRENT_STATE_AUDIT.md), [Data](05_DATA_AND_CONTRACTS.md), [Operations](06_INFRASTRUCTURE_AND_OPERATIONS.md), [Validation](08_QUALITY_AND_VALIDATION.md).

## 1. Scope, assets and trust boundaries

Initial deployment: one user/OS account per local installation, two existing ASP.NET hosts, proposed local launcher/SQLite and OS credential adapter. Official Spotify lane uses metadata and authorized playback only. Standalone permissioned lyrics lane uses local rules and optional consented Gemini. External inference leaves the device; local-first is not an offline/privacy guarantee for enabled external features.

Protect OAuth tokens/state/verifiers; Gemini keys; local browser sessions; recording/source identity; permission/consent records; lyric text/evidence/reviewer annotations; budget and playback action journals; deletion records; release artifacts and update authenticity. Metadata and content hashes may also be identifying/correlatable.

Trust boundaries: browser↔local listener; host↔credential store; host↔database/files/backups; local import/provider/model output↔validator/rendering; host↔official Spotify/Gemini/permitted provider; maintainer↔CI/release distribution. Other websites, other local processes/users, providers and imported text are not implicitly trusted. Malware running as the same OS user is a residual threat; do not promise OS secret storage defeats full device compromise.

## 2. Threat and control register

| ID / threat | Scenario | Required control | Verification |
| --- | --- | --- | --- |
| SEC-001 Spoofing/CSRF/rebinding | Malicious page or forged Host reaches loopback mutations/stream | Exact loopback binding, Host allowlist, authenticated local bootstrap/session, same-origin checks and antiforgery for mutations. Reject unintended origin/host; CORS not authentication. | Cross-origin POST/unauthorized SSE/Host fixtures; no credentials in bootstrap query/history/referrer |
| SEC-002 OAuth theft/replay | Replayed callback, mismatched transaction, weak verifier, parallel login confusion | PKCE S256, cryptographically random state/verifier, bounded expiry, one-use transaction binding, exact registered redirect; no distributed secret | State/verifier/code denial/replay/missing/parallel tests, credential scan |
| SEC-003 Token race/retention | Refresh completes after logout or account switch | Single-flight broker, atomic rotated refresh storage, auth-generation fence, explicit disconnect clears all authorized state | Refresh/disconnect/account epoch races; restart/no-context expiry coverage |
| SEC-004 Information disclosure | Personal playback response/event sent to another session or reused account | Scoped caches/streams/output responses; explicit authorization each protected route; no wildcard sensitive access | Real middleware isolation tests and account-switch cache fixture |
| SEC-005 Tampered model authority | Lyrics say ignore instructions, leak key, skip track, fabricate evidence | Treat text/output as data; model gets no playback/file/network tools or secrets; schema allowlist and exact evidence/version validator | Injection/malformed/oversized/fabricated reference fixtures; denied model playback traffic |
| SEC-006 XSS/injection | Lyric, artist, rationale or review text renders executable content | Context-specific Razor/DOM HTML/attribute/URL encoding; no untrusted raw HTML; no inline unencoded JS; safe CSP where compatible | HTML/attribute/URL/script/bidi fixtures and UI security review |
| SEC-007 SSRF/path traversal | Imported URL/file/provider redirect accesses private system data | Initial paste/file import only, bounded size/type and safe file handling; future providers allowlisted destinations/redirects, no arbitrary fetch | Private-network/redirect/path/oversized fixtures; source adapter review |
| SEC-008 Non-idempotent action/race | Lost skip acknowledgement causes second skip or stale decision skips new track | Fresh account/device/occurrence preconditions, serialized dispatch, durable action intent, no automatic mutation retries; visible OutcomeUnknown | Fault simulations, SDK retry configuration review, concurrent manual/monitor commands |
| SEC-009 Resource exhaustion | Stalled stream, enormous import, expensive repeated model call | Input/concurrency/queue/cache/disk limits, cancellation/total deadlines, bounded writers, budget reservation, rate limiting | Slow/oversized/flood/disk-full fixtures; OPS resource targets |
| SEC-010 Source/rights tampering | Cached result hides source change/expiry; wrong clean/live recording | Immutable source versions and permission provenance; distinct recording/content hashes; rights/version-aware cache reuse | Changed source, mismatch, expiry/revocation and collision-simulation tests |
| SEC-011 Review repudiation/loss | Concurrent edits overwrite dissent or make recommendation appear reviewed | Append-only local review history, expected revision, source binding, clear unreviewed/disputed state | Conflict/supersession/integrity and policy-revision tests; no absolute nonrepudiation claim |
| SEC-012 Secret/log/package disclosure | Launch config, SDK error, DB export, CI artifact contains credential/content | OS store, redacted allowlisted diagnostics, artifact exclusions, private rotation process, secret scanning including history | Captured logs/artifacts/exports tests, credential remediation record |
| SEC-013 Supply-chain/update tampering | Unreviewed package/action, malicious binary or downgraded schema | Approved sources/versions and dependency licenses/advisories, minimal CI permissions, pinned/reviewed actions, SBOM, release authenticity/checksums, safe updates | Restore/package inventory, scan report, signature/checksum/rollback fixtures |
| SEC-014 Deletion resurrection | Late model result/backup restore recreates erased source | Deletion-generation fence, descendant deletion and controlled backup cleanup/reconciliation, cancel jobs | Delete during call, restart and restore fixtures; documented physical/remote limits |

Priority: SEC-001–008 and exposed-credential remediation are correctness/security release gates. Remaining controls are also required for their enabled workflows; all open risk acceptance must be scoped and recorded, not inferred from local-only deployment.

## 3. Authentication and secret lifecycle

- Public-client Spotify PKCE should distribute client ID only. Client ID is not a secret, but registration/use still has platform restrictions.
- Minimum scopes for enabled read/control capabilities; no playlist-write scope unless a separately approved feature needs it.
- Separate local application authentication from OAuth provider authorization. An eligible Spotify token does not authenticate every HTTP caller to the local app.
- Use a protected local bootstrap and HTTP-only session; select cookie Secure/SameSite settings coherently with the chosen loopback HTTP/HTTPS design. Do not claim a Secure cookie protects an HTTP-only setup. Design/negative-test cross-host session behavior explicitly.
- Store refresh token and BYOK key in an approved OS credential adapter. Access tokens ephemeral in memory. No plaintext fallback; unavailable store allows recoverable error or explicitly disclosed session-only mode.
- Concurrent refresh guarded; rotate refresh token atomically; logout/account change invalidates in-flight completions. No logs/UI/SSE contain secret values, verifier, codes or state.
- API keys set/replace/delete through protected mutation routes; UI returns masked availability, never the stored secret. Development user-secrets are not production encryption.
- Disconnect stops monitoring, invalidates pending work/cache/events, clears credentials and explains provider-side revocation if not available programmatically.

**Immediate audit issue:** credential material observed in Spotify launch settings (AUD-001). Owner must determine validity and revoke/rotate if real, inspect distribution/history, and remove through a safe separate change. This pack deliberately does not reproduce it or claim rotation.

## 4. Data purposes, retention and deletion

| Data class | Purpose | Proposed handling |
| --- | --- | --- |
| OAuth/Gemini credentials | Authorized integration/inference | OS store; no DB/backups/logs; explicit disconnect/remove |
| Playback metadata/snapshots | Personal live state and guarded action | Minimal in-memory retention; scope by auth epoch; durable action journal only if needed for safe reconciliation; no psychological profile |
| Permissioned source/version | Evidence-based analysis and review | Local protected SQLite/files under recorded purpose/expiry; preserve fidelity only where allowed |
| Findings/evidence/reviews | Auditable interpretation | Version-bound, minimal excerpts, append-only review; retention cannot exceed applicable rights |
| Consent/budget/action journal | Accountable processing/cost/action state | Minimal local records, explicit reason/status; avoid raw provider payloads; preserve only necessary audit history |
| Logs/diagnostics | Local error recovery | Content/identity/secret allowlist, rotation/age caps, user-reviewed export, no upload default |
| Backup/export | Recovery or deliberate user transfer | Same rights/security protections, limited copies, disclosure and deletion reconciliation |

DATA/OPS specify proposed limits. Confirm actual durations and purposes before implementation approval; permissions may require shorter retention. Normal source deletion removes dependent findings/evidence/reviews/cache and controlled copies. Erase-all includes credentials and installation state. Partial cleanup must be visible/retryable. OS snapshots, exported/unmanaged copies, SSD physical erasure and remote-provider retention are not silently promised away. State what remains and why; keep deletion ledger minimal and itself removable on erase-all according to the implemented policy.

## 5. Rights and platform gates

Spotify policy [Section III](https://developer.spotify.com/policy) includes restrictions on analysis of Spotify Content and ingestion into AI/ML, not just training. It also restricts child-targeted and business/public-playback uses. Interpret permitted official-lane behavior against current complete terms/use case before release; do not assume every metadata transformation or automated action is approved.

**Semantic Spotify bridge remains blocked** pending scoped written permission and a documented terms/rights assessment. Local code, independent library boundaries, a user's subscription/consent, API scopes, BYOK or third-party lyrics do not themselves create clearance. No private Spotify lyric endpoint, session extraction or Spicetify hook is part of this release.

For lyrics, distinguish rights to obtain/read, locally process, retain, let reviewers access, transform/translate, publish evidence/full text, redistribute and send to an external AI service. Public accessibility, an open-source API/database, a dataset paper or Genius search API access does not necessarily grant all those rights. User attestation is evidence of a statement, not counsel's legal determination.

Maintain permission records with source, licensor/authority, allowed purposes, territory/expiry/attribution, withdrawal procedure and review outcome. Reject/disable a source for uses not permitted. No CAPTCHA/proxy/client-hook workaround when a source is unavailable.

This document is not a final privacy notice/TOS or jurisdictional legal review. Determine operating/distribution jurisdictions and applicable data/copyright obligations with qualified review where needed before public beta.

## 6. Optional external inference and vendor assessment

Initial new adapter is Gemini only; legacy OpenRouter/Mistral failover is not automatically part of the consented workflow. Before transfer, display provider/model, source/context fields leaving device, purpose, estimated usage and known retention/training/region uncertainties. Verify user/source processing rights and consent, reserve budget transactionally and permit cancellation. Do not tell users processing is fully local when enabled.

Vendor review records current terms, model/version availability, pricing/quota, data-use/retention settings, locations/subprocessors as known, API security, deletion/support limitations and policy-change monitoring. Do not promise no training/zero retention/free credits without verified applicable settings/terms. Cloud-processing refusal or unavailable budget is an analysis Unavailable state, not permission to switch providers. Uncertain request outcome may still incur charges; no blind retry. No secrets or Spotify content/metadata in prompt input.

## 7. Incident response and readiness

Use OPS-RB-001–009. Classify incident, stop the affected feature/distribution, contain without destroying evidence, rotate compromised credentials, minimize/redact investigation artifacts, assess user/data impact and any legal notification duties, remediate, retest and record closure. No invented 24/7 support or legal notification timeline; applicable deadlines need jurisdiction/incident review.

Release evidence must include threat review, high/critical audit closures, negative auth/CSRF/evidence/action fixtures, redaction scans, dependency/secret scan, rights/provider review, consent/deletion test, and honest limitation copy. A passing scan alone is not security certification. Assign a responsible security contact and disclose a private reporting path that does not ask reporters to paste tokens/lyrics into public issues.
