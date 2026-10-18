# 03 — Feasibility Study and Business Case

**Document ID:** FEAS · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Draft for review · **Accountable owner:** Product/engineering roles, TBD
**Approval:** No funding, partnership, hiring, rights clearance, or commercial viability is established.

## 1. Decision summary

**Conditional go:** local-first hardening of the existing playback companion and a separately permissioned lyric-analysis pilot. **No-go under this initial plan:** semantic Spotify playback integration, hosted multi-user rollout, private lyric extraction, child/venue product promises, or broad linguistic-performance claims.

The business objective is a maintainable open-source engineering artifact and a useful small research/product pilot, not a guaranteed subscription business. Career visibility is a possible benefit, not a hiring commitment. The differentiator must be measured quality and trustworthy engineering, not an assertion that Spotify has never studied lyrics.

## 2. Feasibility by dimension

| ID | Dimension | Assessment / evidence | Proof required before advancing |
| --- | --- | --- | --- |
| FEAS-001 | Existing platform | Three .NET10 projects, real OAuth/queue/monitor/SSE/parser/client code are reusable; audit shows open correctness/security gaps | Clean startup/build, fixture tests and prioritized audit closure |
| FEAS-002 | Local packaging | Windows inspected; browser-based local hosts feasible in principle; launcher/credential adapters/package not implemented | Clean-machine install, port/session design, stable runtime, stop/update/uninstall tests |
| FEAS-003 | Source/evidence storage | SQLite suitable candidate for bounded single-user data; coordinated writers, durability, migration and deletion need design proof | Temporary-DB concurrency/crash/restore/deletion fixtures and package compatibility |
| FEAS-004 | Semantic capability | Existing model SDK and parser reduce plumbing effort; heuristics cannot establish cultural fluency | Declared narrow scope, bilingual rubric review, original regression fixtures, blinded held-out comparison |
| FEAS-005 | Data availability/rights | Permissioned original/artist-contributed text can support a pilot; no licensed commercial corpus is established | Rights ledger covering acquisition, review, retention, excerpts, cloud and publication separately |
| FEAS-006 | Official Spotify lane | Existing public SDK flow; provider eligibility, permitted use, scopes and quota depend on current rules | Release-time official-doc review and authorized limited smoke test, not unapproved public scaling |
| FEAS-007 | Semantic Spotify bridge | Technical modularity does not provide legal/platform permission | Blocked until scoped written platform permission and rights review; new threat model and PRD |
| FEAS-008 | Security/privacy | Local execution reduces central hosting exposure but not token theft, malicious websites, prompt injection or leaks | Local threat/secret controls, negative fixtures, incident/deletion procedures |
| FEAS-009 | Operating cost | No maintainer-hosted runtime is required; users still incur electricity/device/network and optional provider costs | Measured local resource/provider usage, release/support budget, no free-tier assumption |
| FEAS-010 | Distribution/demand | Utility and research interest are hypotheses; broad consumer willingness to pay unvalidated | Small adult pilot, usability evidence, support burden, direct feedback; no fabricated conversion rates |
| FEAS-011 | Research novelty | Stance, multilingual lyrics and music recommendation already have prior work | Related-work review; specific claim and protocol; independently reviewed measured differences |

## 3. Options appraisal

| Option | Benefits | Costs/risks | Recommendation |
| --- | --- | --- | --- |
| Harden existing app only | Fastest trustworthy systems demonstration; keeps scope small | Less distinctive semantic contribution | Required foundation |
| Add standalone evidence/review engine | Builds on parser/moderation work; testable independent value | Rights, annotation, evaluation effort; uncertainty cannot disappear | Preferred next product slice |
| Directly wire Gemini output to Spotify skip | Appears quick to demo | Policy conflict, malformed output, context errors, race conditions, false safety | Reject under initial plan |
| Add Spicetify/cookie bridge | Potential access to internal lyric data | Private interface fragility, secret/security and platform/rights issues | Out of scope |
| Rewrite in Go/Rust or native wrapper | New packaging options | Discards useful .NET work; no proven benefit for current bottleneck | Defer absent measured need |
| Hosted SaaS/cloud microservices | Central updates and potential scale | New security/tenancy/licensing/quota/support costs, demand unproved | Defer pending separate business case |

## 4. Financial model — formulas, not vendor quotes

Maintain separate **maintainer**, **end-user**, and **research** budgets. Currency, tax/exchange rates, provider quotes and spending authority are TBD. Do not describe local/BYOK as zero total cost.

| Cost bucket | Sizing basis | Responsible role |
| --- | --- | --- |
| Engineering opportunity cost | Approved engineering days × actual daily opportunity cost | Product/delivery |
| Review and annotation | Items × independent reviewers × mean minutes/item ÷60 × hourly rate, plus adjudication/coordination | Evaluation/product |
| Optional inference | Uncached authorized jobs × input tokens/job × input price/1M + output tokens × output price/1M + billable uncertain attempts | User; evaluation owner for experiments |
| Rights/legal | Actual corpus/provider/legal quotes; may dominate costs or be unavailable | Permissions/product |
| Distribution | Measured artifact size/downloads, CI minutes, signing/notarization fees and update work | Release |
| Support/maintenance | Qualified issues × mean resolution time; servicing, dependency, policy monitoring | Maintainer |
| Local resources | Measured CPU/memory/disk/network on supported machines; device electricity | User |
| Future hosted service | Compute + storage + backups + egress + observability + support + provider/licensing/security | Separate approval; not current scope |

**Illustrative workload only:** 100 pilot texts × four analysis variants × two repeat runs = 800 analysis runs before retries. At an assumed 1,000 input/250 output tokens per run that is 800,000 input/200,000 output tokens; substitute measured sizes and current consented-provider rates before spending. Local keyword/rule baselines need not be billable provider runs, so actual external usage may be lower. This is not a price quote or approved experiment size.

**Illustrative annotation only:** 100 texts × two reviewers ×10 minutes = about33.3 reviewer-hours before adjudication. Difficulty/language/context can increase time materially. Start with calibration to measure actual effort.

Set both a local transactional request/token budget and any provider-side spending caps. Estimated token cost is not a hard billing guarantee; unclear provider responses may still be charged. No automatic charged-request retries/provider failover. Pause experiments when consent/budget/rights are missing.

## 5. Benefit model and success evidence

Near-term tangible benefits: reliable app behavior; minimized token/data leakage; reproducible integration/fault tests; source/evidence traceability; documented limitations; a polished small demo; technical writing and independent review.

Research benefits require empirical support. Compare keyword, translation-first, generic-prompt and proposed approaches on frozen held-out material; measure false endorsement, presence/stance precision/recall, evidence support, coverage/abstention, latency and cost. Preserve disagreement and report uncertainty/sample limitations.

Product benefits require usability evidence: whether adults can install, import, understand a card, correct it, stop playback monitoring and delete data without assistance. A small pilot informs design; it is not proof of market size or general retention.

Commercialization remains exploratory. Do not plan revenue around public venue playback, children, unlicensed lyric redistribution or unrestricted Spotify access. Any paid offering needs its own current platform/rights/tax/support/security review. Optional donations/sponsorship also require review against platform terms and the actual offering; they are not automatically exempt.

## 6. Feasibility experiments and exit criteria

| Experiment | Timebox proposal | Evidence / decision |
| --- | --- | --- |
| FEX-01 Toolchain/startup | 1–2 focused engineering days | Actual stable SDK restore/build/DI behavior and offline default; revise plan if fundamental host/dependency failure |
| FEX-02 Local auth/credentials | 2–4 days | PKCE transaction and Windows credential-store proof; no distributed secret, working denied-store flow |
| FEX-03 Action and stream fault harness | 3–5 days | Lost skip response, stale occurrence, slow SSE, disconnect races; no unsafe automatic resend |
| FEX-04 Immutable source/SQLite prototype | 2–4 days | CRLF-safe evidence/versioning, migration/concurrency/delete including late-job results |
| FEX-05 Rubric/annotation calibration | Effort determined with reviewers | Written permissions, expertise, workable topic/language rubric, measured annotation effort |
| FEX-06 Optional Gemini structured output | 1–3 engineering days after consent | Schema/evidence failures handled; measured token/latency costs; no platform data/model tools |
| FEX-07 Packaged usability pilot | After correctness gates | Clean-machine flow and adult feedback; support demand and failed-task evidence |

Timeboxes are rough planning allowances, not promises or sums for a delivery deadline. Learning, permissions, reviewer availability and security remediation may change scope. Estimates require backlog refinement after FEX-01.

## 7. Stop/pivot rules

- If rights are unavailable, keep original fixtures and standalone infrastructure; do not scrape to fill gaps.
- If bilingual reviewers are unavailable, reduce language claims/scope instead of accepting model guesses as truth.
- If the proposed engine does not improve targeted outcomes, publish the honest result and keep reliable evidence/review tooling; do not tune on held-out failures without a new evaluation version.
- If local packaging is difficult, ship a clearly labeled developer preview before claiming consumer readiness; secure defaults still apply.
- If Spotify access/policy changes, preserve independent lyric analysis and disable affected integration; no private endpoint workaround.
- If support/resource costs exceed approved capacity, limit platform support and release size; do not add cloud services automatically.

## 8. Sources and review obligations

Starting points, not legal clearance: [Spotify Developer Policy](https://developer.spotify.com/policy), [Developer Terms](https://developer.spotify.com/terms), [quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes), [PKCE](https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow), [Spotify lyrics/mood research](https://research.atspotify.com/publications/the-contribution-of-lyrics-and-acoustics-to-collaborative-understanding-of-mood), [ORIN-Lyrics research](https://proceedings.mlr.press/v319/folorunso26b.html).

Record actual access/review date, terms version, findings and permitted data flows in a vendor/rights review. Availability/publication of a dataset does not establish redistribution or AI-processing permission. Provider prices, quotas and model names must be rechecked before authorized experiments and release.
