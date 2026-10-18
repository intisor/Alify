# 11 — Governance, Architecture Decisions and Risk Register

**Document ID:** GOV · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Proposed governance; no named appointments or approvals
**Accountable owner:** Product/technical lead roles, TBD

## 1. Governance appropriate to project size

This is a disciplined small-project process, not a claim that Alify has a corporate staff. One maintainer can cover multiple roles; document the actual assignment and seek independent review for security, permissions and linguistic assertions where practical. Review gates protect the product without requiring unnecessary bureaucracy.

| Activity | Accountable role | Responsible roles | Consulted / informed |
| --- | --- | --- | --- |
| Product scope and release claims | Product owner | Product/design | Engineering, permissions, evaluation, testers |
| Domain contracts and integrations | Technical lead | Engineering/data | Security, evaluation, quality |
| Credential incident and threat controls | Security owner | Engineering/security | Product, provider owner, release |
| Rights/platform/data processing assessment | Permissions/privacy owner | Permissions/privacy | Qualified legal advice as necessary; engineering/evaluation |
| Rubric, annotation and held-out study | Evaluation lead | Bilingual reviewers/evaluation | Product, permissions, quality |
| CI, verification and acceptance evidence | Quality owner | Engineering/quality | Security, design, release |
| Packaging, rollback and support | Release owner | Engineering/release | Quality, product, security |
| Spending approval and vendor choice | Product/budget owner | Provider/evaluation owner | Security/privacy, delivery |

Assign **one actual accountable person** per decision. Role names in this table are not approvals. Work changing processing rights, secrets, platform access or spending cannot be self-authorized by a model output or passing test.

## 2. Proposed ADR register

All ADRs below are **Proposed**, not formally accepted. Context/alternatives/consequences must be recorded in a decision record before implementation baseline approval.

| ADR | Decision proposed | Rationale / consequence | Revisit trigger |
| --- | --- | --- | --- |
| ADR-001 | Local-first single user; Windows x64 first | Avoid speculative hosting/tenancy/support costs; other OS support remains gated | Validated platform demand and credential/package test capacity |
| ADR-002 | Preserve .NET10/current projects | Reuse queue/SSE/parser/provider work; no language rewrite | Measured incompatibility/bottleneck, not novelty preference |
| ADR-003 | Separate official Spotify and permissioned semantic lanes | No permission inferred from modularity; preserve independent value | Scoped written permissions plus new reviewed data flow |
| ADR-004 | Analysis→policy→action contracts, no model ShouldSkip | Prevent model authority and preference contamination; clearer caches/tests | Evidence-backed change reviewed by security/product |
| ADR-005 | Immutable source ledger, four stance and three expression labels | Reproducible evidence and bounded rubric; narrator/figure subtypes metadata | Versioned rubric/schema migration with compatibility/evaluation plan |
| ADR-006 | SQLite and OS credential adapters for local release | No distributed infrastructure; SQLite holds data, not credentials | Measured concurrency/storage or platform limitations |
| ADR-007 | No automatic retry of uncertain skip | Remote operation non-idempotent; local dedup is not exactly-once | Provider supplies verified semantics eliminating uncertainty |
| ADR-008 | Gemini-only optional BYOK in new workflow | Consent/budget/version clarity; legacy failover not automatically reused | Explicit vendor/consent/risk/budget change review |
| ADR-009 | Paste/file first; no mandatory scraping/private hooks | Rights/security/reliability boundary | Verified provider permission and adapter qualification |
| ADR-010 | Bounded SSE writers and authoritative resync | Slow-client isolation; preserve current event names via migration | Measured alternative justified by use case |
| ADR-011 | Stable SDK/package policy and test-driven qualification | Preview/document drift and demo tests not release evidence | Approved servicing/security update with regressions |
| ADR-012 | Local telemetry only by default | Minimal identifying data/support burden | New opt-in data inventory, consent and privacy/security review |

Canonical wire vocabulary is in [DATA](05_DATA_AND_CONTRACTS.md). New enum names or timing/line semantics require a contract change, not uncoordinated implementation decisions.

## 3. Risk register

Likelihood/impact are qualitative planning judgments (Low/Medium/High), not quantified probabilities. All risks are Open; owners must record review date, residual risk and treatment evidence.

| Risk | Likelihood / impact | Mitigation / contingency | Owner role / gate |
| --- | --- | --- | --- |
| RISK-001 Secret material valid/exposed | Unknown / High | Private validation, revoke/rotate, artifact/history review; stop affected distribution | Security / G1 |
| RISK-002 Wrong or repeated skip | Medium / High | Fresh occurrence/device/auth guards, no blind resend, durable intent/unknown outcome; stop control | Engineering / G2 |
| RISK-003 State/token/SSE leaks across sessions/accounts | Medium / High | Protected local endpoints, scoped epoch caches/streams, broker disconnect tests | Security/engineering / G2 |
| RISK-004 Platform changes or disallowed intended use | Medium / High | Current-policy review, official-only lane, disable feature, preserve standalone mode; no workaround | Permissions/product / G0/G7 |
| RISK-005 Lyrics corpus lacks needed rights | High until acquired / High | Original fixtures and explicitly contributed material; reduce scope, no fallback scraping | Permissions / G3/G6 |
| RISK-006 Hallucinated cultural interpretation | Medium / High | Honest supported scope, independent bilingual annotation, evidence review/abstention; publish failures | Evaluation / G4/G6 |
| RISK-007 Keyword gate misses euphemism/context | High for broad claims / High | Baseline/triage only, no keyword absence=clean; held-out recall/false endorsement reporting | Evaluation / G6 |
| RISK-008 Benchmark leakage/overfitting | Medium / High | Group song/recording/artist splits, repeated-text dedup, frozen rubric/prompt, new version after tuning | Evaluation / G6 |
| RISK-009 Cost/privacy from external calls/failover | Medium / High | Explicit provider/source consent, rights/budget reservation, no implicit fallback/retry, usage evidence | Privacy/budget / G4 |
| RISK-010 Preview/dependency/startup mismatch | Medium / Medium | Stable SDK selection and clean build/DI harness; review advisories/compatibility | Engineering / G1 |
| RISK-011 Source evidence corrupted or renumbered | Medium / High | Immutable versions/ledger/newline/span validation; no silent legacy migration | Data/quality / G3 |
| RISK-012 Delete/restore resurrects content | Medium / High | Generation fence, descendant/backup cleanup, restoration reconciliation and limitations | Privacy/data / G4/G5 |
| RISK-013 Packaging/local session setup too complex | Medium / Medium | Windows-first clean-machine spike, explicit bootstrap/port design and help; developer preview if needed | Release/design / G5 |
| RISK-014 Reviewer/time availability limits progress | High until assigned / Medium | Start permissions/recruitment early, narrow Pidgin-English scope, defer unsupported language claims | Evaluation/delivery / G6 |
| RISK-015 Support and scope creep exceeds maintainer capacity | Medium / Medium | WIP limits, staged release, no speculative cloud/wrappers; bounded support policy | Product/release / G7 |
| RISK-016 Malicious local web/import/model input | Medium / High | Threat controls, encoding/CSRF/Host/validation/tool denial and limits | Security / G2/G3/G4 |
| RISK-017 No demonstrated semantic advantage | Unknown / Medium | Publish honest comparison; keep evidence/review tooling useful; change hypothesis not results | Evaluation/product / G6 |

## 4. Change control and review cadence

At each phase exit, review requirements, evidence, open audit issues/risks, budget, external policy and next slice. During active delivery maintain a short weekly or per-work-session review; no fixed team ceremony is imposed.

Changes to scope/schema/model/prompt/provider/permissions/retention/supported platform/SLO require: change ID; reason; affected PRD/contracts/tests; rights/privacy/cost impact; alternatives; responsible reviewer; migration/rollback; decision/date; updated release traceability. Version semantic experiments separately from application release.

Urgent security fixes may bypass ordinary feature scheduling but not credential confidentiality or required validation. Record follow-up review. Do not accept high/critical release risk silently; any exception needs owner, rationale, exact scope/expiry and containment. Missing platform/copyright permission, valid exposed secrets, or an uncontrolled playback mutation cannot be waived by a schedule decision.

## 5. Open decision log

| Decision | Needed before | Owner |
| --- | --- | --- |
| Actual owner assignments and initial Windows/language/topic scope | G0 | Product |
| Official Spotify use assessment and app/access model | G0/G2 | Permissions/engineering |
| Local session/bootstrap/listener ports, redirect and HTTP/HTTPS approach | G1/G2 | Security/engineering |
| Stable SDK pin and servicing/package restore policy | G1 | Engineering/release |
| SQLite library/schema ownership and shared-writer coordination | G3 | Technical/data |
| Credential adapter and unavailable-store behavior | G2 | Security/engineering |
| Rights/reviewer participation/consented model and budget | G3/G4/G6 | Permissions/evaluation/budget |
| Prospective semantic success thresholds and pilot size | Before held-out evaluation | Evaluation/product |
| Release authenticity/signing and support/security reporting | G5/G7 | Release/security |

Use [15](15_WORKING_TEMPLATES.md) for records. No blank owner/approval field is evidence of acceptance.
