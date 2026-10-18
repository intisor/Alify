# 10 — Delivery Roadmap and Implementation Backlog

**Document ID:** PLAN · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Proposed; no tickets implemented or dates committed
**Accountable owner:** Delivery/engineering roles, TBD

Companions: [PRD](01_PRODUCT_REQUIREMENTS.md), [Audit](02_CURRENT_STATE_AUDIT.md), [Release gates](14_TRACEABILITY_AND_RELEASE_GATES.md). Preserve source/projects; implementation PRs are separate from this documentation pack.

## 1. Delivery strategy and dependencies

| Phase | Deliverable | Exit gate | Dependency |
| --- | --- | --- | --- |
| P0 Baseline and security triage | Approved scope, credential disposition, stable toolchain, reproducible startup/test harness | G0/G1 | None; stop distribution of unremediated valid secrets |
| P1 Trustworthy playback core | Broker/PKCE, local endpoint protection, scoped fresh observations, guarded actions, bounded SSE | G2 | P0 |
| P2 Independent semantic foundation | Permissioned immutable source ledger, typed contracts, validation, local policy and baseline rules | G3 | P0; may run parallel to P1 with disjoint modules |
| P3 Optional model and review | Consented budgeted Gemini, evidence cards, durable review/corrections/deletion | G4 | P2 + security/storage controls |
| P4 Evaluation and packaging | Permissioned held-out study, clean-machine package, accessible UX, CI/security evidence | G5/G6 | P1–P3; reviewer/rights work starts earlier |
| P5 Limited adult pilot and release | Honest limited capability, support/runbooks and approval record | G7 | All applicable release gates |

No phase enables semantic Spotify playback. That remains a separately approved future project. Cloud hosting has no task in the initial backlog.

Timelines depend on audit results and reviewer/permission availability. Refine estimates after startup/credential/storage spikes. Relative effort below: S = bounded small change, M = multi-component change, L = needs decomposition. They are not story-point velocity or calendar promises. Maintain a small work-in-progress limit; code review/security tests must not become a last-week phase.

## 2. Ready-to-refine engineering backlog

| Ticket | Priority / effort / role | Work and acceptance | Depends on / trace |
| --- | --- | --- | --- |
| ALI-001 | P0/S Security + owner | Privately investigate credential material, rotate/revoke if valid, scan history/artifacts, remove distributed secret safely with owner approval. Record redacted disposition; no token pasted in public tickets. | AUD-001; SEC-012; PRD-002 |
| ALI-002 | P0/S Product/engineering | Approve PRD/scope and owner assignments, current Spotify-use assessment, forbidden flows; establish risk/ADR and evidence records. | PRD all; G0 |
| ALI-003 | P0/M Engineering | Reconcile stable .NET10 SDK pin/servicing/dependency policy; capture restore/build results and update inaccurate developer instructions. No unrelated dependency upgrades. | AUD-019; PRD-014 |
| ALI-004 | P0/M Quality/engineering | Add real .NET test projects to solution with centrally managed compatible test dependencies; host startup/DI validation for both environments and network-denied fixtures. Investigate session/cache registrations rather than guess. | ALI-003; AUD-018/020 |
| ALI-005 | P0/M Quality | Characterization tests for existing parser, queue boundaries, explicit metadata and SSE payloads; capture flagged→clean skip-attribution bug without live Spotify. | ALI-004; AUD-003/013 |
| ALI-006 | P1/L Engineering/security | Windows credential adapter and context-independent broker; serialized refresh/rotation, auth generation, coordinated disconnect, explicit unavailable-store state. No plaintext fallback. | ALI-001/004; AUD-004/005; PRD-002/012 |
| ALI-007 | P1/M Engineering/security | PKCE S256 transaction and least scopes with callback expiry/replay/denial/parallel tests; exact registered loopback redirect. | ALI-006; AUD-009 |
| ALI-008 | P1/L Engineering/security | Protect local bootstrap/session, Host/Origin, mutation antiforgery, SSE and personal-cache responses; prove no LAN/default unauthorized access. | ALI-004/006; AUD-007/008/010 |
| ALI-009 | P1/M Engineering | Scope playback caches by account/auth epoch; separate display snapshot caching from uncached action checks and monotonic freshness. | ALI-004/006; AUD-002; PRD-003/013 |
| ALI-010 | P1/M Engineering | Detach lyric enrichment from official Spotify observation/filter lane; retain legacy lyric code in standalone migration context only. UI identifies metadata-only behavior. | ALI-005/009; AUD-011/012 |
| ALI-011 | P1/L Engineering/quality | Add typed original-target action outcome/journal, serialized fresh preflight, occurrence/device/auth guard and no automatic skip retry; acceptance vs observed effect separate. Regression fixes existing skip-return/event error. | ALI-005/006/009; AUD-003; PRD-003/004 |
| ALI-012 | P1/M Engineering | Bounded per-client SSE writer, cancellation/cleanup, targeted initial snapshot, versioned compatible events and reconnect/resync; slow-client isolation. | ALI-008/009/011; AUD-006/007; PRD-005 |
| ALI-013 | P1/M Quality | Playback fault matrix: timeout/lost response, manual/monitor races, repeat/device/account changes, 429/401/no device, stop and process restart. No real playback or public provider needed. | ALI-011/012; QA section5/6 |
| ALI-014 | P2/M Engineering/data | Add neutral analysis/resolution/review/policy/action contracts per DATA; prove analyzer cannot depend on Spotify adapter or contain trusted ShouldSkip. Review Core's existing Spotify SDK dependency and isolate by namespaces/ports before considering project split. | ALI-002/004; PRD-007/008/009 |
| ALI-015 | P2/M Engineering/data | Immutable source ledger and CRLF-safe logical-line parser alongside legacy mapping; raw/canonical hashes, source versions, separate timings and encoding rules. | ALI-005/014; AUD-013; PRD-008/013 |
| ALI-016 | P2/L Engineering/data | SQLite adapter/migrations/writer coordination, permission/source records, bounded caches; durable action/budget integration where needed. Failure cannot destroy source or trigger unjournaled actions. | ALI-014/015; DATA/OPS; PRD-001/012/013 |
| ALI-017 | P2/M Engineering/security | Permissioned paste/file import and typed resolver with matching/version gate, size limits, no arbitrary URL/scraper fallback. Provider extension stays disabled until approved rights review. | ALI-015/016; PRD-006/013 |
| ALI-018 | P2/M Engineering/evaluation | Strict schema/exact evidence validator; separate deterministic policy evaluator with preference versions and Abstain defaults. Reject fabricated references/malicious fields. | ALI-014/015; PRD-008/009 |
| ALI-019 | P2/M Engineering/evaluation | Conservative heuristic baseline plus 10–15 original regression fixtures for supported scope; unknown idioms/languages abstain. No hardcoded fixture-title matching or generic clean verdict. | ALI-015/018; PRD-007; EVAL |
| ALI-020 | P3/L Engineering/security | Optional Gemini structured adapter, source/context consent and transactional budget reservation; bounded calls, invalid output/uncertain charges, model versioning, no provider fallback/tools. Validate available SDK/model rather than trust legacy comment. | ALI-006/016/018; PRD-011 |
| ALI-021 | P3/M Engineering/design | Evidence-card/source navigation and local analysis status UI; keyboard navigation, explicit uncertainty, no safety claim and no Spotify join. | ALI-017/018/019; PRD-007/008/014 |
| ALI-022 | P3/M Engineering/data | Append-only review repository/UI, expected revision conflict, alternate evidence and unresolved disagreement; independently recompute recommendations. | ALI-016/018/021; PRD-010 |
| ALI-023 | P3/L Engineering/security | Scoped disconnect/delete and late-write fences; source descendants/controlled backup deletion/restore reconciliation; provider/export/SSD limitations surfaced. | ALI-006/016/020/022; PRD-012 |
| ALI-024 | P3/M Engineering/security | Safe-code redacted diagnostics, bounded log rotation, explicit reviewed export; secret/content scan across logs/SSE/packages/error paths. | ALI-008/012/020; AUD-021; SEC-012 |
| ALI-025 | P4/L Evaluation/permissions | Obtain rights and bilingual reviewers, calibrate/freeze rubric, group/deduplicate train-dev-heldout split, compare baselines, report uncertainty/errors/coverage and actual usage. Not a prerequisite for claiming unsupported languages work. | ALI-019/020; PRD-007/010; EVAL |
| ALI-026 | P4/L Release/quality | Windows local launcher/package, coordinated migration/startup and ports, clean-machine install/update/rollback/uninstall; approved runtime servicing/signing/checksum/SBOM choices. | ALI-003/008/016/023/024; PRD-001/014 |
| ALI-027 | P4/M Quality/release | Real .NET+Alify fixture UI CI, dependency/secret scan and release evidence artifact policy; no live keys in fork PRs; bounded job runtimes. | ALI-004/013/019/023/026; QA/OPS |
| ALI-028 | P4/M Design/quality | Manual accessibility/usability tests, stale/unknown/error/consent/delete flows; verify proposed OPS targets on declared machine and collect fail cases. | ALI-012/021/022/026; PRD-014 |
| ALI-029 | P5/M Product/release | Adult limited pilot, release checklist/rights/current-terms sign-off, safe known-issues/help/security reporting and rollback/support ownership. | ALI-025/027/028; G7; DIST |

ALI-016 and ALI-011 require an integration design: action intent storage must be reliable before journaling-dependent dispatch is enabled. A fixture/in-memory action prototype may precede SQLite, but production gate G2 includes durable journal proof; no release occurs between prototype and persistence. Resolve this dependency during decomposition rather than claim the backlog order alone supplies durability.

## 3. Proposed file/module boundaries

Keep current projects and existing source intact except scoped implementation PRs. Candidate additions (not existing files):

- Core neutral analysis models/validators/policy under `Alify.Core/Features/Analysis/` and source ledger under a provider-neutral lyrics feature.
- Lyrics import/analysis/review adapters and pages under existing `Alify.Lyrics/Features/Lyrics/` and `Pages/Lyrics/`.
- Spotify credential broker/gateway/action types remain in its integration domain; no analyzer DI registration in the official lane.
- Tests proposed under `tests/Alify.Core.Tests/`, `tests/Alify.Integration.Tests/`, with application-specific Playwright specs separate from current demos. Exact names/test framework versions approved in ALI-004.
- Infrastructure implementation (SQLite/credential/launcher) location decided by ADR before new project scaffolding; no microservices implied.

Do not add placeholder language detectors, compile-time secret/semantic flags, or disabled demo hooks that silently become runtime functionality. New features need explicit supported capability and tested error paths.

## 4. Definition of Ready / Done

**Ready:** requirement and audit/control links; bounded write set; approved data flow; acceptance examples; negative/failure cases; dependency/API/version confirmed; test strategy; effort/dependency refinement; no unavailable secrets/rights assumed.

**Done:** focused code review; real passing scoped tests then relevant broader suites; unchanged unrelated user work; safe config/diagnostics; docs/traceability updated; performance measured if claimed; migrations/rollback/cleanup tested when applicable; risks/limitations and rights reviews recorded. Unsupported external environments are explicitly excluded. Passing handcrafted semantic fixtures is not a benchmark claim.

## 5. Next work session

Review scope/owners and AUD-001 privately, then execute ALI-003–005 to establish actual build/startup and characterization evidence. Approve canonical contracts before ALI-014–019. This is a practical progression, not another request to rewrite the project or deploy cloud infrastructure.

Record implementation decisions using [working templates](15_WORKING_TEMPLATES.md); check release progress in [14](14_TRACEABILITY_AND_RELEASE_GATES.md). No commits/branches/deployments are authorized by this document alone.
