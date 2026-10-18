# 14 — Traceability and Release Gates

**Document ID:** TRACE · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Draft traceability register and gate definitions. **No gate is assessed, no evidence is attached, no release is authorized by this document.**
**Accountable owner:** Release approver role, TBD · **Approvals:** Not granted

Companions: [PRD](01_PRODUCT_REQUIREMENTS.md), [Audit](02_CURRENT_STATE_AUDIT.md), [Architecture](04_ARCHITECTURE_AND_INTEGRATION.md), [Data](05_DATA_AND_CONTRACTS.md), [Operations](06_INFRASTRUCTURE_AND_OPERATIONS.md), [Security](07_SECURITY_PRIVACY_AND_COMPLIANCE.md), [Quality](08_QUALITY_AND_VALIDATION.md), [Evaluation](09_SEMANTIC_EVALUATION_PROTOCOL.md), [Backlog](10_DELIVERY_ROADMAP_AND_BACKLOG.md), [Governance](11_GOVERNANCE_DECISIONS_AND_RISKS.md), [UX](12_UX_AND_ACCESSIBILITY.md), [Distribution](13_DISTRIBUTION_AND_SUPPORT.md). Record outcomes using [15 — Working templates](15_WORKING_TEMPLATES.md).

## 1. Purpose and rules of use

This document is the single index that answers three questions: does every requirement have a design anchor, a verification method, and a gate; does every known audit finding have remediation and an owner; and what exactly must be true before a release decision can be made.

Four rules govern its use:

1. **A mapping is not coverage.** An empty cell means nobody has traced that requirement to a check. It is a defect in this pack, not a tacit pass.
2. **An anchor is not evidence.** A requirement pointing at `ARCH-016` says the design exists in a draft. Only a recorded run of a named case at a named commit satisfies a gate.
3. **A gate is not a schedule decision.** Missing platform permission, a valid exposed credential, or an uncontrolled playback mutation cannot be deferred to a later phase.
4. **Everything here is a proposal until a named person signs it.** Role names are not appointments. All approver fields below are TBD, and every gate result at this document's date is **Not assessed**.

The gate register in section 4 supersedes the partial list in [08](08_QUALITY_AND_VALIDATION.md) section 10. That table remains the operational summary; this document adds G6 and G7, which 10 and 11 already reference, and adds the requirement-level mapping and go/no-go detail.

## 2. Requirement traceability matrix

Column meanings: **Design** = architecture anchors; **Contract** = data/contract identifiers; **Operate** = infrastructure and runbook anchors; **Control** = security threats; **Verify** = quality matrix levels and acceptance fixtures; **Build** = backlog tickets; **Gate** = where the requirement must be satisfied. A dash means no anchor exists yet and the gap is recorded in section 6.

### 2.1 Functional requirements

| Requirement | Design | Contract | Operate | Control | Verify | Build | Gate |
| --- | --- | --- | --- | --- | --- | --- | --- |
| PRD-001 local single-user | ARCH-001, ARCH-008, ARCH-014 | DATA-012, DATA-013 | OPS-003, OPS-006, OPS-SLO-01 | SEC-001 | QA §3 (I/UI/S/R), DATA-T08 | ALI-016, ALI-023, ALI-026 | G1, G5 |
| PRD-002 Spotify OAuth PKCE | ARCH-005, ARCH-014, ARCH-015 | DATA-009, DATA-010 | OPS-002, OPS-004, OPS-RB-001/004/008 | SEC-002, SEC-003, SEC-012 | QA §3 (U/C/I/S/F), DATA-T08 | ALI-001, ALI-006, ALI-007 | G0, G2 |
| PRD-003 playback and queue | ARCH-012, ARCH-013 | DATA-007, DATA-008, DATA-011 | OPS-005, OPS-SLO-05, OPS-RB-002 | SEC-004, SEC-008 | QA §3, QA §5 matrix, DATA-T03, DATA-T07 | ALI-009, ALI-011, ALI-013 | G2 |
| PRD-004 explicit filtering | ARCH-005, ARCH-012, ARCH-013 | DATA-006, DATA-007 | OPS-005, OPS-RB-002/004 | SEC-008 | QA §3, QA §5 matrix | ALI-011, ALI-013 | G2 |
| PRD-005 protected event stream | ARCH-016 | DATA-011 | OPS-SLO-04, OPS-RB-007 | SEC-001, SEC-004 | QA §3, QA §6, DATA-T07 | ALI-012, ALI-013 | G2 |
| PRD-006 permissioned lyric import | ARCH-002, ARCH-010 | DATA-002 | OPS-005, OPS-SLO-02, OPS-RB-005 | SEC-007, SEC-010 | QA §3, DATA-T02 | ALI-017 | G3 |
| PRD-007 scoped interpretation | ARCH-003, ARCH-010, ARCH-011 | DATA-004 | — | SEC-005 | QA §3, DATA-T04, EVAL §3–§4 | ALI-019, ALI-021 | G3, G6 |
| PRD-008 verifiable evidence | ARCH-003, ARCH-010 | DATA-003, DATA-008 | OPS-SLO-03 | SEC-006, SEC-010 | QA §3, DATA-T01, DATA-T04 | ALI-015, ALI-018 | G3 |
| PRD-009 interpretation/policy/action separation | ARCH-004, ARCH-009 | DATA-000, DATA-006 | OPS-005 | SEC-005, SEC-009 | QA §3, DATA-T03, DATA-T04, DATA-T05 | ALI-014, ALI-018 | G2, G3 |
| PRD-010 review and disagreement | ARCH-011 | DATA-005 | — | SEC-011 | QA §3, DATA-T05, EVAL §5 | ALI-022 | G3, G4 |
| PRD-011 optional consented Gemini | ARCH-006 | DATA-009 | OPS-005, OPS-SLO-06, OPS-RB-005 | SEC-009, SEC-012 | QA §3, DATA-T06 | ALI-020 | G4 |
| PRD-012 disconnect and scoped deletion | ARCH-013, ARCH-015 | DATA-013 | OPS-006, OPS-SLO-07, OPS-RB-003/006 | SEC-003, SEC-014 | QA §3, DATA-T08 | ALI-023 | G4, G5 |
| PRD-013 identity and cache correctness | ARCH-010 | DATA-001, DATA-008 | OPS-006, OPS-SLO-08 | SEC-010 | QA §3, DATA-T02, DATA-T06 | ALI-009, ALI-015 | G3 |
| PRD-014 accessible recoverable release | ARCH-007, ARCH-016 | DATA-010 | OPS-003, OPS-008, OPS-SLO-09 | SEC-006 | QA §3, QA §7, DATA-T09, UX §7 | ALI-026, ALI-028 | G5, G7 |

### 2.2 Nonfunctional requirements

| Requirement | Design / contract | Operate | Control | Verify | Build | Gate |
| --- | --- | --- | --- | --- | --- | --- |
| NFR-001 secure local boundary | ARCH-014, ARCH-015 | OPS-004, OPS-RB-008 | SEC-001–004, SEC-012 | QA §4 security fixtures | ALI-007, ALI-008, ALI-024 | G2, G4 |
| NFR-002 bounded cancellable work | ARCH-016 | OPS-005, OPS-SLO-06 | SEC-009 | QA §6 fault laboratory | ALI-012, ALI-020 | G2, G4 |
| NFR-003 durable evidence and intent | DATA-007, DATA-012 | OPS-006, OPS-RB-006 | SEC-008, SEC-014 | QA §9, DATA-T07, DATA-T08 | ALI-011, ALI-016, ALI-023 | G2, G3 |
| NFR-004 maintainable modular code | ARCH-008, ARCH-009 | OPS-001 | SEC-013 | QA §3 contract rows | ALI-003, ALI-014, ALI-027 | G1, G2 |
| NFR-005 reproducible quality | — | OPS-002, OPS-008 | SEC-013 | QA §7, §8 | ALI-003, ALI-004, ALI-027 | G1, G5 |
| NFR-006 privacy and permission minimization | DATA-002, DATA-009, DATA-013 | OPS-007, OPS-SLO-07 | SEC-010, SEC-014 | QA §4, QA §9 | ALI-017, ALI-023, ALI-024 | G4 |
| NFR-007 accessibility | ARCH-007 | OPS-008 | SEC-006 | QA §7 targets, UX §7 | ALI-028 | G4, G5 |
| NFR-008 transparent capability and cost | ARCH-006, ARCH-011 | OPS-004, OPS-007 | SEC-009 | QA §7 cloud row, UX §6 | ALI-020, ALI-024 | G4, G7 |

### 2.3 Non-requirement that must stay mapped

The semantic Spotify bridge is a **prohibited** initial capability, not an unmapped requirement. It is tracked so that it cannot be introduced silently:

| Item | Status | Anchors |
| --- | --- | --- |
| Semantic result influencing Spotify playback or ingesting Spotify Content | **BLOCKED — no implementation, no permission, no approval** | ARCH-005, ARCH-009, ARCH-M5; DATA-006 context split; SEC-005, RISK-004; EVAL §12 gate 7; G0 |

Any pull request that makes this reachable must be treated as a rights and security incident under OPS-RB-009, not a feature.

## 3. Audit finding traceability

Audit identifiers are defined in [02](02_CURRENT_STATE_AUDIT.md). Every finding is Open until a linked pull request, test artifact, owner, date, and release decision are recorded. Severity is the auditor's provisional judgement and must be confirmed during implementation triage.

| Finding | Provisional severity | Remediation ticket | Control / gate | Closure evidence required |
| --- | --- | --- | --- | --- |
| AUD-001 client-secret material | Critical | ALI-001 | SEC-012, OPS-RB-001 / G1 | Redacted disposition record, provider rotation confirmation, working-tree/history/artifact scan, package exclusion check |
| AUD-002 global playback cache keys | High | ALI-009 | SEC-004 / G2 | Interleaved-session and account-switch fixtures; staleness fixtures |
| AUD-003 skip returns next queue head | High | ALI-011 | SEC-008 / G2 | Regression for flagged→clean, flagged→flagged, final queue item, rejected/timeout action |
| AUD-004 refresh bypass / token divergence | High | ALI-006 | SEC-003 / G2 | Refresh/rotation race, foreground/background concurrency, broker restart fixtures |
| AUD-005 logout leaves background authorization | High | ALI-006, ALI-023 | SEC-003 / G2 | Logout-during-pending-work fixtures; no authorized action after disconnect |
| AUD-006 unbounded sequential SSE writes | High | ALI-012 | SEC-009 / G2 | Stalled reader beside healthy readers, concurrent publishers, shutdown cleanup |
| AUD-007 unauthenticated wildcard-CORS event stream | High | ALI-008, ALI-012 | SEC-001, SEC-004 / G2 | Unauthorized/sibling-session denial, Host/Origin tests, disconnect revocation |
| AUD-008 missing endpoint CSRF/local-session protection | High | ALI-008 | SEC-001 / G2 | Malicious-origin, DNS-rebinding/Host, cross-site POST fixtures |
| AUD-009 secret-based OAuth with playlist-write scopes | High | ALI-007 | SEC-002 / G0, G2 | PKCE replay/expiry/parallel-transaction cases, least-scope review, no secret in package |
| AUD-010 output cache on session-token response | High | ALI-008 | SEC-004 / G2 | Middleware-accurate cache isolation, Cache-Control, unauthorized/204 behavior |
| AUD-011 lyric fetch blocks playback lane | Medium | ALI-010 | SEC-009 / G2 | Monitoring responsive with lyric/model adapters unavailable or denied |
| AUD-012 explicit-metadata-only flagging | High (design) | ALI-010 | SEC-005 / G2 | Boundary contract tests; no analyzer dependency or provider traffic in Spotify lane |
| AUD-013 CR/LF split and non-immutable mapping | High | ALI-005, ALI-015 | SEC-010 / G3 | LF/CRLF/CR, blanks, trailing newline, Unicode span identity, legacy-version reference tests |
| AUD-014 first-hit retrieval, empty-on-failure | High | ALI-017 | SEC-007, SEC-010 / G3 | Clean/live/remix mismatch, ambiguous candidates, 404 vs 429 vs timeout, instrumental provenance |
| AUD-015 runtime hash, provider failover chain | High | ALI-020 | SEC-005 / G4 | Version invalidation, malformed references, injection, no-consent/no-budget/no-fallback assertions |
| AUD-016 per-call browser, no-sandbox, CSP bypass | Medium | ALI-010 (defer) | SEC-006 / G5 | Decision to exclude from package; process-cleanup review only if separately retained |
| AUD-017 blocking Doppler on startup | Medium | ALI-003, ALI-026 | SEC-012 / G1 | Denied-network, missing-token, timeout startup; config precedence; bounded safe errors |
| AUD-018 unverified DI registrations | High (validation gap) | ALI-004 | NFR-004 / G1 | Integration host startup in both environments with scope/options validation |
| AUD-019 preview SDK / stale README | Medium | ALI-003 | OPS-002 / G1 | Recorded SDK resolution and clean restore/build/package on the approved version |
| AUD-020 no product test coverage | High (release gap) | ALI-004, ALI-027 | NFR-005 / G1, G5 | Versioned reports mapped to requirements, network-denied fixtures |
| AUD-021 identifying diagnostics | Medium | ALI-024 | SEC-012 / G4 | Captured log/export/artifact secret and content scan; delete controls |
| AUD-022 no source/evidence/rights ledger | High (design gap) | ALI-015, ALI-016, ALI-022 | SEC-010, SEC-011 / G3 | Migration, restore, deletion, revocation and source-version validation |

Findings AUD-001, AUD-018, AUD-019 and AUD-020 sit on the G1 path: they determine whether any later evidence can be produced at all.

### 3.1 Sequencing conflicts requiring a decision

Distinct from missing coverage: these are places where two correct-looking requirements cannot both be satisfied as scheduled.

| Conflict | Documents in tension | Consequence if unresolved | Resolution path |
| --- | --- | --- | --- |
| Durable dispatch intent required by G2, but the durable store arrives in P2/G3 while journaling-dependent dispatch sits in P1 | DATA-007, OPS-006, 08 §10, 10 §2 | G2 cannot pass, or playback ships without durable intent, which OPS-006 forbids | §4.1 Option A (recommended), needs ADR + change record |
| Shared-writer coordination for two hosts is decided at G3, after G1/G2 already depend on a durable store | DATA-012, OPS-006, 11 §5 | Write model chosen after the fact; migration and backup story unstable | Move decision to G1 as part of §4.1 |
| AUD-001 (Critical, secret material) sits on the G1 path, but nothing in the current plan names who investigates it | AUD-001, 10 §2 (ALI-001), RISK-001 | G1 stalls or is waived informally | ALI-001 executed privately; disposition recorded before any further distribution |
| Requirement rows for PRD-007/PRD-010 have no operational anchor | 01 §4/§5, 06 | Interpretation and review have no retention, load or recovery limits | Decisions recorded in DATA/OPS during P2 refinement |

## 4. Gate register G0–G7

G0–G5 correspond to the operational gates in [08](08_QUALITY_AND_VALIDATION.md) section 10. G6 and G7 are defined here because [10](10_DELIVERY_ROADMAP_AND_BACKLOG.md) and [11](11_GOVERNANCE_DECISIONS_AND_RISKS.md) already reference them.

| Gate | Purpose | Exit conditions | Approver roles (TBD) | Depends on |
| --- | --- | --- | --- | --- |
| **G0 — Scope and rights** | Establish that the intended data flows are permitted before any implementation depends on them | Approved feature boundaries and forbidden flows; named owners; rights ledger covering resolver acquisition, local analysis, reviewer access, retention, evidence excerpts, external processing and publication; current Spotify platform/terms review covering the official lane; explicit written statement that the semantic bridge is blocked; experiment budget approved | Permissions/privacy + product | None |
| **G1 — Build, startup and toolchain** | Produce a reproducible development and qualification base | Recorded stable .NET 10 SDK and dependency policy (OPS-002); both hosts restore, build and start in Development and Production; AUD-001 disposition closed; real .NET test projects exist in the solution with a network-denied fixture suite; no startup dependency on Doppler or a browser | Engineering + security | G0 |
| **G2 — Core playback correctness and local boundary** | Prove the official lane is safe and honest | PRD-001–005, PRD-009, PRD-013 tests pass; no duplicate or uncertain skip auto-retried; refresh/disconnect/account-epoch races covered; SSE isolation, resync and backpressure covered; PKCE and local-session negative fixtures pass; no open Critical or High defect | Quality + engineering + security | G1 |
| **G3 — Standalone semantic foundation** | Prove permissioned, evidence-bound, reviewable interpretation | Immutable source ledger and CRLF-safe line IDs; exact evidence validator rejects fabricated references; distinct resolution/analysis statuses; independent policy evaluator with `Abstain` default; analyzer has no playback capability and no `ShouldSkip`; review/disagreement persistence; protocol 09 integrity gate | Data/engineering + evaluation + permissions | G1; semantic portion may run in parallel with G2 on disjoint modules |
| **G4 — Privacy, security, consent and accessibility** | Prove data handling and user-facing honesty | Threat register reviewed with SEC-001–014 status; consent, budget reservation, deletion, export and redaction tests pass; secret/SSRF/XSS/CSRF/Host fixtures pass; scoped accessibility evidence with recorded manual screen-reader review; unresolved-risk ledger accepted with owners and expiry | Security/privacy + accessibility + quality | G2 for playback lane, G3 for semantic lane |
| **G5 — Lifecycle, packaging and release UX** | Prove the product can be installed, upgraded, recovered and removed safely | Clean-machine install, offline first start, upgrade, rollback, uninstall; migration and restore rehearsal with integrity checks; deletion cannot resurrect content; known-issue and limitation copy published; release authenticity and exclusion policy decided and verified; no scraping runtime in package | Release + quality | G2, G3, G4 |
| **G6 — Evaluation evidence** | Decide what can honestly be claimed about interpretation quality | Rights/feasibility, integrity, pilot and preregistration gates from EVAL §12 passed; blinded held-out comparison across the four systems of EVAL §8 with frozen protocol; stratified report with counts, confidence intervals, disagreement and abstention/coverage; contamination and overlap disclosed; no numerical semantic threshold approved retrospectively | Evaluation + product + permissions | G3, and rights/reviewer availability |
| **G7 — Release authorization** | Authorize a bounded public release of the supported scope | G0–G6 assessed for the features actually shipping; release artifacts per [13](13_DISTRIBUTION_AND_SUPPORT.md) §4 present; support and private security-reporting path named; rollback/support ownership assigned; published limitations match measured evidence; residual risks accepted in writing with scope and expiry | Product + release + security + permissions | All applicable prior gates |

### 4.1 Unresolved sequencing conflict: durable action intent vs gate order

This document exists to surface conflicts, and one is live enough to name. It was left open in [10](10_DELIVERY_ROADMAP_AND_BACKLOG.md) §2 with the instruction to "resolve this dependency during decomposition rather than claim the backlog order alone supplies durability."

The conflict, stated plainly:

| Says | Where | Effect |
| --- | --- | --- |
| Dispatch intent must be persisted **before** the network send; if journaling fails, do not send the command | DATA-007, OPS-006 | Playback safety depends on a durable store |
| G2 requires PRD-003/012 tests including uncertain and stale skip, and a durable action journal | 08 §10, this document §4 | The playback lane cannot pass G2 without that store |
| ALI-011 (journaling-dependent dispatch) is **P1**; ALI-016 (SQLite adapter, migrations, writer coordination) is **P2**, exiting at G3 | 10 §2 | Durable storage arrives a phase *after* the gate that needs it |
| The shared-writer coordination decision is scheduled at **G3** | 11 §5 | The two-host write model is decided after a host has already written to it |

Read together, the gate order is unsatisfiable as written: either G2 fails, or the playback lane ships dispatch without durable intent, which OPS-006 forbids outright. Both are release blockers. This is not a documentation tidiness issue.

**Proposed resolution (Option A — split durability, recommend).** Separate the minimum durable capability the playback lane needs from the full semantic schema:

1. **A minimal append-only action journal becomes a P1/G2 deliverable, owned by ALI-011.** It holds only what DATA-007 requires: action ID, intent, target expectations, dispatch time, state transitions, acknowledgement, reason code, correlation ID. It is not a database and must not grow into one.
2. **The DATA-012 SQLite schema stays P2/G3.** Sources, evidence, reviews, consent, budgets and their migrations are not prerequisites for playback safety, and coupling G2 to them would delay the fix for AUD-003, a live correctness defect.
3. **The storage substrate and cross-host writer coordination are decided once, at G1, not G3.** Both lanes must then write through one mechanism with one migration, backup and deletion story. Whichever substrate is chosen, it must satisfy DATA-013 retention and OPS-006 backup, restore, integrity and deletion behavior. SQLite is still a proposal; a journal that is not SQLite is acceptable only if it meets the same guarantees.
4. **Dispatch stays disabled until the journal is durable.** No playback command is issued when the intent write fails. An in-memory or fixture-backed prototype may precede the durable store for development, but per 10 §2 there is **no release between prototype and persistence**.

**Alternative (Option B) — reorder.** Move ALI-016 ahead of ALI-011 so P1 waits for the full schema. Simpler sequencing, no split store. Rejected as the default because it couples the playback lane to work it does not need and postpones a High-severity correctness fix; it remains acceptable if the writer-coordination decision is genuinely easier once the whole schema exists.

Conditions that must hold under either option, and are checked at G2:

- [ ] Crash-during-dispatch and restart fixtures show unresolved intent survives and is reported `OutcomeUnknown`, never replayed.
- [ ] Two-host or two-process concurrent write fixtures show no interleaved or lost journal records, and bounded lock/wait behavior with a visible failure state.
- [ ] The journal contains no lyric text, evidence or credentials, and honors the DATA-013 30-day default with user-earlier deletion.
- [ ] Deletion and disconnect remove or fence journal records as specified, and no late write recreates them.

**This section is a proposal, not a decision.** Adopting it changes the G1/G2/G3 schedule and the open-decision log in [11](11_GOVERNANCE_DECISIONS_AND_RISKS.md) §5, so it requires an ADR and a change record under [11](11_GOVERNANCE_DECISIONS_AND_RISKS.md) §4 before any of ALI-011, ALI-016 or the launcher is built. The conflict is recorded here rather than silently resolved by editing the other documents, because the pack's own rule is that conflicts require a reviewer decision, not a quiet choice of whichever reading is convenient.

Gate rules:

- Gates are assessed per **feature scope**, not for the whole product. A release may ship the playback lane without claiming semantic capability, provided G3/G6 are marked out of scope in the release record and no UI implies otherwise.
- A skipped or not-applicable gate must be stated explicitly in the release record with the compensating reason. Silence is not a skip.
- **G6 does not imply G7 for a claim the evidence does not support.** Passing an evaluation allows a bounded claim; it does not authorize a broad one.
- Automated checks, coverage percentages and passing scans are inputs. They never substitute for a required negative fixture, a rights record or a human judgement of linguistic or policy questions.

## 5. Go/no-go checklist

Recorded per release attempt. Every line is **Not assessed** at this document's date.

### 5.1 Preconditions for any release decision

- [ ] Named accountable person recorded for each approving role in section 4.
- [ ] Release scope enumerated as a feature list, with explicitly excluded features listed.
- [ ] Target OS, RID and package variants stated and previously qualified on clean machines.
- [ ] Current provider terms, policy versions and access rules reviewed and dated.
- [ ] Rights ledger reviewed for every source, including original fixtures.
- [ ] Consented model version and price basis rechecked, or the feature excluded.

### 5.2 Credentials and secrets

- [ ] AUD-001 disposition recorded; any real credential revoked or rotated with provider confirmation.
- [ ] Working tree, git history, build artifacts and packages scanned; findings closed.
- [ ] No secret, development certificate or launch profile in the shipped package; verified by inspection of the artifact, not by configuration intent.
- [ ] Credential-store adapter proven on a clean machine, including its unavailable/locked behavior.

### 5.3 Playback and network actions

- [ ] No automatic retry of a playback mutation anywhere in the stack, including SDK and HTTP retry handlers; verified by configuration inspection and fault fixtures.
- [ ] Action journal persists dispatch intent before send; outcomes use the exact `Requested`/`Succeeded`/`Failed`/`OutcomeUnknown`/`CancelledStale` vocabulary.
- [ ] UI never presents `Succeeded` alone as an observed skip, and never names the wrong track as skipped.
- [ ] Explicit-metadata filtering is opt-in, off after restart, and described as metadata filtering rather than interpretation.

### 5.4 Semantic lane and boundaries

- [ ] No analyzer, resolver or semantic policy is resolvable from the Spotify host; verified by a denied-network test.
- [ ] No model `ShouldSkip` or playback field exists in any contract or schema fixture.
- [ ] Evidence validation rejects fabricated excerpts, ranges and version references.
- [ ] `NotFound`, `Instrumental`, `Mismatch`, `ProviderUnavailable`, `InsufficientContext`, `UnsupportedLanguage` and `InvalidOutput` are separately rendered and never presented as a clean verdict.
- [ ] UI states that semantic findings do not control Spotify playback in this release.

### 5.5 Privacy, rights and deletion

- [ ] Consent is explicit, versioned and revocable; revocation prevents new processing and invalidates dependent caches.
- [ ] Budget reservation precedes external calls; concurrent reservations cannot oversubscribe; uncertain charges are not silently retried.
- [ ] Deletion removes descendants, caches and controlled backups; restore does not resurrect deleted sources; limits are disclosed.
- [ ] Diagnostics, exports, logs and CI artifacts scanned for credentials, personal identifiers and lyric content.

### 5.6 Claims and communication

- [ ] Every published claim traces to a recorded measurement, or is removed.
- [ ] Declared language and topic scope matches what was actually evaluated.
- [ ] Limitations, abstention behavior and stale/unknown states are visible in the product, not only in the repository.
- [ ] No safety, child-protection, universal-fluency, zero-cost or zero-latency claim appears in any artifact.

## 6. Known gaps in this register

Recorded rather than hidden. Each item needs an owner and a decision before the implementation baseline in [10](10_DELIVERY_ROADMAP_AND_BACKLOG.md) can be approved.

| Gap | Effect | Needed to close |
| --- | --- | --- |
| No requirement maps to a verified test, because no test project exists | Every "Verify" cell is aspirational | ALI-004 then real cases per requirement |
| PRD-007 and PRD-010 have no `OPS` anchor | Operational limits for interpretation and review are unstated | Decisions in DATA/OPS for job retention, review storage and dispute handling under load |
| NFR-005 has no contract anchor | Reproducibility policy lives only in OPS-002 | A stated package-source, lock and SBOM policy decision |
| AUD-001 disposition unknown | G1 cannot be assessed | Owner investigation, kept off shared channels |
| Semantic bridge has no gate of its own | It is blocked, so no exit criteria exist by design | A new gate, PRD change and threat model, only after written permission |
| Durable-journal and gate ordering conflict (§4.1) | G2 cannot pass as scheduled | ADR and change record adopting Option A, plus the G1 writer-coordination decision |
| No risk has a recorded residual rating | All RISK-001–017 remain Open | Owner review with dates and evidence, per [11](11_GOVERNANCE_DECISIONS_AND_RISKS.md) |
| Local boundary threat model untested for a second OS account on one machine | Single-user assumption is asserted, not demonstrated | Fixture using a second local account against the loopback listener |
| Provider rate-limit and quota behavior unmeasured | Polling design targets may be wrong | Authorized measurement under current quota modes, or conservative backoff with the limitation published |

## 7. Change control for this document

Any addition to a requirement, an audit finding, a gate condition or the blocked-bridge list is a change record under [11](11_GOVERNANCE_DECISIONS_AND_RISKS.md) §4, with the affected rows updated here in the same change. Removing a trace because a check failed, or narrowing a gate because a deadline is close, is not permitted by omission: it requires an explicit, dated, approved change record naming the accepted risk, its scope and its expiry.

Version history:

| Version | Date | Change | Author role |
| --- | --- | --- | --- |
| 0.1 | 2026-10-09 | Initial traceability matrix, G0–G7 register, go/no-go checklist and gap list | Release approver role (TBD) |

No build, test run, provider call, scan or deployment was performed to produce this document. It is an index of requirements written on a stated date, and it inherits the staleness of every document it references.