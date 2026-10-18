# 01 — Product Requirements Document

**Document ID:** PRD · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Draft for product, engineering, security, and permissions review
**Accountable owner:** Product role, TBD · **Approvals:** Not granted

Companions: [Audit](02_CURRENT_STATE_AUDIT.md), [Architecture](04_ARCHITECTURE_AND_INTEGRATION.md), [Data contracts](05_DATA_AND_CONTRACTS.md), [Release gates](14_TRACEABILITY_AND_RELEASE_GATES.md).

## 1. Problem and product thesis

Existing explicit labels and category booleans do not explain whether lyrics mention, endorse, reject, quote, or figuratively invoke a theme. Listeners and reviewers also need transparent limitations: missing lyrics, unresolved idioms, imperfect recording matches, and uncertain playback actions.

Alify will preserve its working integration concepts while making behavior observable, dependable, and inspectable. The distinctive hypothesis is that section/narrator context, source-bound evidence, and honest abstention can improve a narrowly evaluated lyric-understanding workflow. Improvement over generic models is **a hypothesis**, not a current result.

## 2. Personas and jobs to be done

| Persona | Job | Boundary |
| --- | --- | --- |
| Adult personal listener with eligible Spotify access | Observe playback and optionally request explicit-metadata skipping | No guarantee of silent transitions or child-safe listening |
| Curious adult listener/reviewer | Understand a permissioned lyric passage and inspect its evidence | Interpretations are not artist-belief or psychological profiles |
| Bilingual collaborator/researcher | Annotate ambiguity and compare approaches reproducibly | Rights, competence, and publication scope recorded |
| Developer/maintainer | Reproduce failures and demonstrate reliable .NET integration | No live credentials required for normal automated tests |

Target first OS: Windows x64, proposed because it is the inspected environment. Other platforms remain future candidates until packaged, credential-store, and accessibility tests pass.

## 3. Goals and non-goals

**Goals:** local first; one user per installation; preserved code/project structure; independent playback and analysis lanes; source-grounded findings; accountable review; optional transparent cloud processing; bounded cost/work; no unsafe retry of playback mutations; reproducible product and research evidence.

**Non-goals for v0.1:** hosted/multi-tenant SaaS; public venue playback; child-targeted filtering; direct Spotify lyric extraction; private APIs/Spicetify/session-cookie workflows; audio censorship or DSP; guaranteed zero-audio exposure; universal language understanding; model training; psychological diagnosis; catalog-wide recommendation; native desktop wrapper; semantic Spotify auto-skip.

## 4. Functional requirements

All requirements below are release-blocking unless explicitly scoped out through an approved PRD change. Detailed exact statuses are in DATA; avoid interpreting absence/error as a clean verdict.

| ID | Requirement | Acceptance criteria |
| --- | --- | --- |
| PRD-001 | Local single-user installation | Offline launch permits original/permissioned import, supported local rules and review without Spotify, Gemini, Doppler, or hosted account. Bind approved loopback listeners; enforce single active installation ownership; clearly label network-dependent features. |
| PRD-002 | Secure official Spotify OAuth | Public-client PKCE S256 with transaction-bound expiring single-use state/verifier, exact registered redirect, minimum scopes, secure local token broker, coordinated refresh and rotation. No client secret distributed. Missing configuration/capability produces actionable setup state. |
| PRD-003 | Correct playback and queue observations | Display account/device state, paused/no-device/unsupported-item cases, observation time and staleness. Queue is an observation, not a guaranteed future order. Commands target current authorization/device/playback generation and serialize dispatch; timeouts after possible send become `OutcomeUnknown`, never blind retries. |
| PRD-004 | Optional explicit-metadata filtering | Off by default; user explicitly starts/stops. Only official explicit metadata drives this lane. Unknown metadata/capability/stale observation abstains. Event reports original target/action outcome, not the new queue head. Restart/disconnect does not silently resume. |
| PRD-005 | Protected live event stream | Same-origin authenticated subscriptions; bounded independent client queues, cancellation, reconnect and authoritative resync; no account/epoch leakage; slow readers do not stall monitoring or other clients. Commands are never replayed from events. |
| PRD-006 | Permissioned lyric import/resolution | User file/paste first; record provenance, declared recording identity, allowed retention/analysis/review/export/cloud purposes. Reject unsupported/oversized inputs. Provider use requires a documented rights/terms assessment. Preserve Found/NotFound/Instrumental/Mismatch/ProviderUnavailable distinctions. |
| PRD-007 | Scoped thematic interpretation | Findings separate topic, stance Endorsed/Mentioned/Rejected/Ambiguous, expression Literal/Figurative/Unknown, section and source-supported narrator. Declare supported scope/languages; no keyword-only clean verdict. Unsupported/insufficient/failed analysis is explicit. |
| PRD-008 | Verifiable evidence | Immutable source version and stable one-based logical-line IDs, retaining blanks/headers; exact quote/range/hash validation before results are usable. Timings are separate. Invalid references/output are rejected; valid citation does not imply semantic correctness. |
| PRD-009 | Interpretation/policy/action separation | Analyzer contains no trusted ShouldSkip or playback tools. Side-effect-free policy returns Allow/RecommendSkip/Abstain against a versioned preference set. Standalone reports cannot drive Spotify. Permission/policy-blocked bridge is absent, not merely an undisclosed toggle. |
| PRD-010 | Review and disagreement history | Append-only reviewer records, rationale and alternate evidence; originals/disagreement retained; concurrent revisions detected; UI distinguishes unreviewed/reviewed/disputed. New reviews invalidate affected policy recommendations, not source evidence. |
| PRD-011 | Optional consented Gemini BYOK | Disabled by default. Show provider/model/data scope and known retention limitations; require processing rights and explicit consent before transfer; secret stored via approved credential adapter. Budget reservation/concurrency/cancellation and uncertain-charge behavior tested. No unconsented provider failover. |
| PRD-012 | Disconnect and scoped deletion | Stop new work; invalidate generations; cancel pending local jobs; clear selected tokens/caches/source descendants/reviews/controlled backups; reject late writes. Report partial failures, exports/remote retention/physical-erasure limits; provider-side revocation instructions available. |
| PRD-013 | Recording identity and cache correctness | Preserve clean/live/remix/version markers; title/artist hash is not unique recording identity. Freeze source/content/parser/model/prompt/rules versions. Account/authorization epochs scope playback caches; permission revocation/changes invalidate reuse. Concurrent identical work deduplicated within the same consent context. |
| PRD-014 | Accessible, recoverable release experience | Packaged clean-machine install/start/stop/upgrade/rollback/uninstall instructions; no developer secrets or scraping runtime mandatory. Keyboard and screen-reader flows for setup, consent, review, stop, deletion; honest errors and stale/unknown states; qualifications and limits visible. |

## 5. Nonfunctional requirements

| ID | Requirement | Verification |
| --- | --- | --- |
| NFR-001 | Secure local boundary and least privilege | Threat controls and negative fixtures in SEC/QA; not LAN-exposed by default; no secrets/content in ordinary logs or artifacts |
| NFR-002 | Bounded work and responsive cancellation | Cancellation/deadlines for locks, providers, streams and storage; operation-specific retries; measure proposed OPS targets |
| NFR-003 | Durable evidence and action intent | Transactional source/review/budget/action records; tested migration, crash recovery and restore; no replay of uncertain actions |
| NFR-004 | Maintainable modular code | Preserve existing projects; neutral contracts, SDK adapters, explicit error/status types, DI/startup validation, no speculative distributed components |
| NFR-005 | Reproducible quality | Approved stable .NET 10 toolchain, centrally reviewed dependencies, offline fixture tests in CI, signed/checksummed distribution policy recorded |
| NFR-006 | Privacy and permission minimization | Local diagnostics only by default; retention purposes/expiry; independent export/cloud permissions; deletion tests and honest limitations |
| NFR-007 | Accessibility | Proposed WCAG 2.2 AA target for release journeys, automated plus recorded manual review; no conformity certification before evidence |
| NFR-008 | Transparent capability/cost | No universal safety/fluency/zero-cost/zero-latency claim; estimated vs actual provider usage differentiated; unavailable state visible |

Latency/memory/stream/cancellation targets in OPS-SLO-01–09 are **proposed engineering acceptance goals**, not contractual guarantees. Semantic accuracy targets are set prospectively in EVAL after pilot/calibration and before held-out evaluation, never chosen retrospectively to pass.

## 6. User journeys and success outcomes

**Playback:** install → local setup → official login → view fresh state → explicitly enable metadata filter → observe accepted/failed/unknown action → stop/disconnect. No lyric/model dependency in this flow.

**Analysis:** permissioned import → inspect source/line structure → choose supported local rules or consented Gemini → inspect evidence/uncertainty → append review → view independent policy recommendation → delete/export within permitted scope.

**Release outcome measures:** setup completion and recovery usability in a recorded small pilot; all security/correctness gates evidenced; safe degradation and no duplicate local dispatch in fault suite; measured OPS targets. Do not infer population retention/conversion from a few testers.

**Research outcomes:** held-out stance/presence metrics, evidence-support review, false endorsement rate, abstention-versus-coverage, per-language breakdown and uncertainty. A small regression suite demonstrates behavior, not superiority.

## 7. Dependencies, launch gates, and scope controls

Dependencies: eligible Spotify developer/account access; release-time public API/policy review; permissioned material and bilingual reviewers; supported SDK/packages; OS secret storage; approved optional model terms/costs. Local standalone mode must survive loss of external access.

Semantic Spotify control is a separate future change requiring scoped written permissions and a new data-flow/threat/release assessment. No product deadline or user opt-in can override that gate.

Baseline approval requires named product/engineering/security/permissions owners, selected language/topic scope, agreed technical targets and budget, and review of source audit blockers. Release additionally requires QA/security/packaging/evaluation evidence, versioned limitations and support procedures. Use [14](14_TRACEABILITY_AND_RELEASE_GATES.md) to record actual decisions.
