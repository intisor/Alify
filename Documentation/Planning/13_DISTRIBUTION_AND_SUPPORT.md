# 13 — Distribution, Launch and Support Plan

**Document ID:** DIST · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Draft; no release, revenue, partnership or hiring claim approved
**Accountable owners:** Product/release/support roles, TBD

## 1. Positioning

Proposed truthful description:

> Alify is a local-first .NET music companion with official Spotify playback observation and optional explicit-metadata filtering, plus a separate permissioned lyric workspace for source-grounded interpretation and review. Optional external AI processing requires a user key, permission and consent. Semantic findings do not control Spotify in this release.

Do not position as guaranteed censorship/parental protection, a business venue player, Spotify-endorsed integration, a replacement for Spotify, or a universally fluent cultural interpreter. The code/research can be useful even if no commercial demand or model advantage is established.

## 2. Distribution model and cost responsibility

Open-source repository and an initial qualified Windows x64 local package are proposed. Confirm repository/dependency/fixture licenses before choosing distribution terms; do not assume existing LICENSE.txt covers third-party lyric data, annotations or provider SDK redistribution. Optional public demo uses original/permissioned fixtures only; no public hosted user accounts or real tokens.

Maintainer pays only approved development/research/distribution/support costs, not a mandatory runtime backend. Users pay their own eligible provider subscriptions/device/network and optional inference usage. BYOK/free tiers are not zero-cost guarantees. Signing, CI, download hosting, legal advice and annotations may cost the maintainer; track them separately.

Spotify registration/client-ID access model must be reviewed against current provider policy. Do not ship a shared client secret, evade user quotas by proliferating registrations, or promise 30-second onboarding until a clean-machine study demonstrates it. Initial app eligibility and allowlist constraints must be stated accurately at release time.

## 3. Release stages

| Stage | Audience / purpose | Required controls |
| --- | --- | --- |
| Developer preview | Maintainer/collaborators, fake providers and original fixtures | Clearly labeled non-production; secret remediation, safe local defaults and no unsupported external-data flow |
| Private qualification | Small adult group permitted by provider rules, explicit opt-in | Packaging/security/rights/consent and account eligibility checked; no uncontrolled commands; support/rollback available |
| Public local release | Qualified OS/capabilities only | All applicable gates in14, published limits, install/uninstall/privacy/security/usage docs, licenses/SBOM/checksums and approved authenticity strategy |
| Future expansion | Additional OS/languages/providers or hosting | Separate scope/rights/threat/evaluation/cost review and qualification; no inherited approval |

No predetermined number of public Spotify users or extended quota approval is assumed. A limited local release may remain developer-oriented while eligibility/distribution questions are unresolved.

## 4. Required release artifacts

- Versioned source/tag reference and reproducible build environment/dependency inventory.
- Windows package plus authenticity/checksum verification instructions; exact signed/unsigned status disclosed.
- Runtime/OS/browser requirements, data/credential paths, registered redirect/onboarding guidance.
- Offline help and first-run original fixture permissions.
- User guide distinguishing metadata filtering, standalone analysis, policy recommendation and action outcome.
- Known issues, declared language/topic/model capabilities, benchmark/report scope and error examples.
- Privacy/data-flow statement, provider-consent explanation, permission/export limitations and deletion/disconnect guide reviewed against implementation.
- Upgrade/schema compatibility, backup/rollback and uninstall preserve-versus-delete instructions.
- Third-party licenses/SBOM and permitted publication/fixture notices.
- Support and private security-reporting process; safe diagnostics export template.

Release packages exclude developer launch settings/secrets/local databases/logs, development certificates and test artifacts. No mandatory Node/Chromium scraping runtime in initial product package. Help must not instruct cookie/private-token extraction.

## 5. Support policy — proposed, capacity bounded

Maintainer support is best-effort until explicit staffed service commitments exist. Do not promise 24/7 support, an SLA or response times not backed by capacity. Publish actual supported OS/version combinations and provider policy dependencies.

Public issue form asks for app/OS/version, safe error code, reproduction using original fixture/fake provider, expected/actual behavior and sanitized diagnostics. Explicitly prohibit tokens, cookies, keys, raw personal lyric text, account identifiers and private files. Private security reports use an owner-approved contact channel; until assigned, do not invent an email address.

Triage classifications:

| Class | Examples | Response procedure |
| --- | --- | --- |
| Security/privacy | Exposed token, unauthorized local access, cross-account data | Private containment/incident process; stop affected release/feature; rotate if needed; no public secret reproduction |
| Playback correctness | Wrong target, duplicate skip, unknown action mishandled | Disable automatic filter, safe journal/fixture reproduction, no repeated live skip experiments |
| Data integrity | Source/evidence mismatch, corrupt DB, resurrected deleted data | Stop writers, preserve protected local recovery state, tested restore/deletion reconciliation |
| Provider access | 401/403/429/no device/terms change | Eligibility/cooldown/reauth guidance, no private API workaround |
| Semantic quality | Unsupported idiom, disputed stance, missing context | Preserve dissent, confirm language/permission scope, add development fixture without contaminating held-out set |
| Usability/package | Port conflict, locked store, setup/accessibility | Offline recovery guide, scoped fix and clean-machine qualification |

Use OPS runbooks; never remotely collect the full user's DB/process memory by default. Report remote retention/external dispatch uncertainties honestly.

## 6. Portfolio and research publication

Publish a reproducible technical report showing existing systems work and actual improvements. Separate:

1. Source-level architecture explanation.
2. Verified operational tests and measured environment/latency/resource results.
3. Semantic protocol, permissioned dataset summary, baselines, model versions, sample limits and failure cases.
4. Open risks/current unsupported uses.

A short demo should show lost-response/slow-SSE recovery with controlled adapters and a source-grounded card with reviewer dissent. Only demonstrate real provider calls under permitted access, rights and consent. Do not publish full lyric datasets/excerpts/translation/reviewer identity without separate permission.

No claim of outperforming translation-first or frontier models until the frozen comparison supports that exact claim. No “top1%” ranking or promise of Spotify employment. Potential outreach asks for feedback on a concrete engineering/evaluation contribution and links reproducible evidence; do not imply endorsement or confidential partnership.

## 7. Pilot learning and expansion criteria

Measure setup errors, task completion/assistance, clarity of uncertainty/consent, safe disconnect/delete behavior, bugs/support effort and per-feature usefulness. Capture responses with appropriate consent; no listening-based psychological profiles. A few testers cannot establish market size/conversion.

Expand only when current scope is reliable and maintainable, rights/access permit the feature, demand is directly observed and cost/support capacity is approved. Hosted SaaS, monetization, additional OS/languages or semantic Spotify integration each require new decision records. A product may remain an excellent open-source reference without becoming a subscription business.
