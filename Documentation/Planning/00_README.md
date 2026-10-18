# Alify — Product and Engineering Planning Pack

**Version:** 0.1 · **Date:** 2026-10-09 · **Status:** Draft for review

**Approval:** Not approved for implementation baseline, external claims, legal clearance, or production release. Owner roles are proposed; named accountable owners must be assigned. This pack changes documentation only.

## Executive decision

Evolve the existing C# application into a **local-first, single-user music companion**, preserving the playback/queue/SSE foundation while adding independently useful, permissioned lyric interpretation. Analysis describes meaning and uncertainty; a separate policy evaluator recommends an action; a playback executor handles authorized commands and uncertain outcomes.

Initial delivery has two independent lanes:

1. Official Spotify OAuth, playback observations, opt-in explicit-metadata filtering, and a protected live dashboard.
2. Standalone permissioned lyric import, evidence-backed stance analysis, optional consented Gemini BYOK, reviewer disagreement, and standalone policy reports.

**Semantic analysis must not control Spotify playback or ingest Spotify Content under this release plan.** A future integration requires a documented, scoped rights assessment and written platform permissions/policy clearance. Separate projects, local execution, user consent, API access, or third-party lyric sourcing do not themselves authorize that integration.

No private Spotify APIs, session-cookie extraction, client hooks, mandatory scraping, child-safety guarantees, hosted multi-user service, Redis, or microservices are in the initial release. Preserve legacy code through controlled migration; preservation does not mean every existing behavior remains enabled.

## Document register and reading order

| Document | Purpose | Primary reviewer role |
| --- | --- | --- |
| [01 — PRD](01_PRODUCT_REQUIREMENTS.md) | Product scope, numbered requirements, acceptance criteria, outcomes | Product owner |
| [02 — Current-state audit](02_CURRENT_STATE_AUDIT.md) | Source-grounded findings and remediation priorities; not a penetration test | Engineering/security |
| [03 — Feasibility and business case](03_FEASIBILITY_AND_BUSINESS_CASE.md) | Technical, operational, rights, financial, and research go/no-go criteria | Product/engineering/permissions |
| [04 — Architecture and integration](04_ARCHITECTURE_AND_INTEGRATION.md) | Boundaries, ports, migration, playback/SSE behavior | Technical lead |
| [05 — Data and contracts](05_DATA_AND_CONTRACTS.md) | Canonical statuses, evidence identity, proposed schema, cache and lifecycle | Data/engineering |
| [06 — Infrastructure and operations](06_INFRASTRUCTURE_AND_OPERATIONS.md) | Local packaging, credentials, SQLite, CI, SLO proposals, runbooks, future hosting gates | Release/operations |
| [07 — Security, privacy, compliance](07_SECURITY_PRIVACY_AND_COMPLIANCE.md) | Threat model, controls, rights gates, retention, vendor review | Security/privacy/permissions |
| [08 — Quality and validation](08_QUALITY_AND_VALIDATION.md) | Requirement/test matrix, fault simulation, release qualification | Quality owner |
| [09 — Semantic evaluation protocol](09_SEMANTIC_EVALUATION_PROTOCOL.md) | Annotation rubric, baselines, held-out evaluation, honest reporting | Evaluation lead |
| [10 — Delivery roadmap and backlog](10_DELIVERY_ROADMAP_AND_BACKLOG.md) | Dependency-ordered work packages and implementable tickets | Delivery/engineering |
| [11 — Governance, decisions, risks](11_GOVERNANCE_DECISIONS_AND_RISKS.md) | RACI, draft ADRs, risk register, change control | Product/technical lead |
| [12 — UX and accessibility](12_UX_AND_ACCESSIBILITY.md) | Screens, flows, honest status copy, accessibility criteria | Design/accessibility |
| [13 — Distribution and support](13_DISTRIBUTION_AND_SUPPORT.md) | Release positioning, installation, support, portfolio publication | Release/product |
| [14 — Traceability and release gates](14_TRACEABILITY_AND_RELEASE_GATES.md) | Cross-document mappings, go/no-go checklist, readiness evidence | Release approver |
| [15 — Working templates](15_WORKING_TEMPLATES.md) | Tickets, ADRs, rights/vendor review, experiment, incident, release records | All contributors |

**Start here:** 01 → 02 → 03 → 10 → 14. Read architecture/data/security before writing code. Use 15 for durable evidence instead of informal conversation approvals.

## Baseline and evidence rules

- Inspected solution: `Alify.Core`, `Alify.Lyrics`, `Alify.Spotify`; all target `net10.0`. `README.md` still describes .NET 9; source/project configuration takes precedence.
- `global.json` requests a preview SDK. SDK `10.0.401` is installed on the inspected Windows machine; installation is not build/startup qualification.
- The existing Playwright workflow/tests exercise external demos, not Alify. No .NET test project appears in the inspected solution.
- Current playback flags derive from Spotify explicit metadata. Existing moderation clients are not an implemented stance engine.
- SQLite, OS credential adapters, neutral semantic contracts, reviewer persistence, protected event delivery, launcher, and production qualification described here are **proposed**, not implemented.
- No build, product test suite, benchmark, packaging test, live-provider call, or deployment was performed to create this pack. Read-only source/configuration inspection and SDK enumeration are not runtime certification.
- Source findings are a dated snapshot; update the audit after each implementation phase. Verify provider documentation again at design approval and release.
- Client-secret material was observed in Spotify launch settings. Values are deliberately omitted. If valid, rotate/revoke before sharing/distributing; documentation creation did not perform remediation.

## Source of truth and conflict resolution

1. 01 defines product requirements; 05 defines exact wire/domain vocabularies; 07 and permissions reviews constrain allowed data flows.
2. 04/06 implement those requirements in design; 08/09 define how to verify them; 14 records release evidence.
3. 10 schedules work and 11 tracks decisions/risks. A backlog ticket cannot waive a security or platform gate.
4. Existing `ARCHITECTURE.md`/README are historical implementation explanations, not approval for the new design.
5. Conflicts require a change record and reviewer decision before coding. Do not silently choose whichever document permits broader behavior.

## Open decisions before implementation baseline

Assign accountable owners; reconcile stable SDK pin/servicing; confirm Windows x64 as first release; choose local listener/session design and OAuth registration; prove credential storage and SQLite writer coordination; freeze supported language/rule scope and topic rubric; obtain appropriate material/reviewer permissions; select consented model and cost limits; agree release targets and evaluation size.

A solo maintainer may fulfill several roles. Independent review is still needed where practical for security, linguistic claims, and rights questions. No corporate team, contract, budget, certification, or approval is implied by these role names.
