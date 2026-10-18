# 15 — Working Templates

**Document ID:** TPL · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Proposed record formats. **Blank fields are not approval.** A template with no named owner, date and evidence is an intention, not a decision.

Companions: [Governance](11_GOVERNANCE_DECISIONS_AND_RISKS.md), [Backlog](10_DELIVERY_ROADMAP_AND_BACKLOG.md), [Traceability and release gates](14_TRACEABILITY_AND_RELEASE_GATES.md), [Security](07_SECURITY_PRIVACY_AND_COMPLIANCE.md).

## 1. How to use these templates

Copy a template into the working record location agreed with the technical lead, fill it in as work proceeds, and link the completed record from the relevant document. Keep records with the change they describe, not in a private notes file.

Four conventions apply to every template:

1. **Named person, not role.** "TBD" means the record is incomplete. A record with TBD approver fields does not satisfy a gate.
2. **Evidence over assertion.** Write the command, case ID, commit and observed result. "Tests pass" without a commit and a case list is not evidence.
3. **No secrets, no licensed content.** Never paste tokens, keys, cookies, full lyrics or reviewer identity into a record. Reference a redacted artifact instead.
4. **Record refusals.** If a check cannot be run, write why. "Not run" with a reason is honest; a silent omission reads as a pass.

Records marked *retained* below are the ones referenced by gates. Everything else is optional working detail.

## 2. Architecture decision record (ADR)

One per decision in [11](11_GOVERNANCE_DECISIONS_AND_RISKS.md) §2. Fill before the implementation baseline, not after the code exists.

```markdown
# ADR-NNN — <decision title>

Status: Proposed | Accepted | Superseded by ADR-NNN | Rejected
Date: YYYY-MM-DD
Decider: <name>            # one accountable person
Consulted: <names and roles>
Requirements affected: PRD-NNN, NFR-NNN
Gates affected: G0–G7

## Context
What forces are real, and what constraints (rights, platform policy, technical
limits, evidence gaps) bound the options? State facts, not preferences.

## Decision
The choice, in one paragraph, with the versioned names of any schema, enum,
provider, model or policy revision it fixes.

## Alternatives considered
| Option | Why rejected |
| --- | --- |

## Consequences
What becomes easier, harder, or newly constrained. What a future maintainer
will be tempted to do that this decision forbids, and how they would know
it is forbidden.

## Reversal conditions
The specific evidence that would justify revisiting: provider capability change,
written permission, measured failure, or a superseded decision.

## Evidence
Commit(s), benchmark or fixture references, rights/terms review record, prior ADRs.
```

## 3. Implementation ticket

Extends the ALI-NNN rows in [10](10_DELIVERY_ROADMAP_AND_BACKLOG.md). Raise in the tracker; keep the durable summary here.

```markdown
# ALI-NNN — <title>

Owner: <name>
Phase: P0–P5 | Priority: S/M/L
Requirements: PRD-NNN… | Controls: SEC-NNN | Gates: GN
Depends on: ALI-NNN | Blocks: ALI-NNN

## Problem
The observed behavior or gap, with the source path and line reference from the
audit. Not a solution description.

## Proposed change
Bounded write set. Name every project, file group and configuration surface
touched. Anything outside this list needs a new ticket.

## Explicit non-goals
What this ticket deliberately does not do, so a reviewer can reject scope creep
without reopening the decision.

## Acceptance
Given / When / Then cases for each requirement, including every negative and
failure case. A ticket with only happy paths is not ready.

## Verification
Exact commands, project paths, test project names, and the fixture IDs from
DATA-T01–09 or the QA matrix that must pass. No command may reference a path
that does not exist yet.

## Data, rights and cost impact
New stored data, retention, permission basis, deletion behavior, provider calls
and budget effect. "None" must be justified, not assumed.

## Risks and rollback
What could break, how it is detected, and how it is reverted. For schema changes:
migration, backup, downgrade behavior and deletion-ledger reconciliation.

## Done evidence
Commit range, review, test report link, documentation updates, remaining
limitations, and the release decision this ticket feeds.
```

## 4. Rights, terms and vendor review

*Retained.* Required by G0 for every external dependency, and by G3/G6 for material and reviewers.

```markdown
# RIGHTS-NNN — <source, provider or platform>

Reviewer: <name> | Role: permissions/privacy
Date of review: YYYY-MM-DD | Review again by: YYYY-MM-DD
Terms or policy version reviewed: <exact version/date>
URL: <link>

## Intended use
Describe the specific proposed data flow: what data, which direction, what
purpose, which users, which territory. A general description is insufficient.

## Rights actually granted
| Right | Granted? | Evidence | Expiry / territory / conditions |
| --- | --- | --- | --- |
| Obtain / read | | | |
| Local analysis | | | |
| Human reviewer access | | | |
| Retain stored evidence | | | |
| Publish excerpts or full text | | | |
| Send to an external AI provider | | | |
| Redistribute or train on | | | |

## Distinctions that must be explicit
Separate technical availability from permission. Note explicitly whether the
review covers ingestion for machine learning as distinct from training, and
whether the use is personal or business, child-directed, or public-playback.

## Findings and refusals
What the terms prohibit, ambiguously address, or leave unaddressed. A refusal
here is a valid and useful outcome.

## Result
Permitted scope | Prohibited scope | Conditions | Reviewer decision: <name>

## Change trigger
Any change to provider terms, model, scope, data direction or reviewer
population invalidates this review.
```

## 5. Experiment record

*Retained* for FEX-01–07 in [03](03_FEASIBILITY_AND_BUSINESS_CASE.md) §6 and any evaluation run under [09](09_SEMANTIC_EVALUATION_PROTOCOL.md).

```markdown
# FEX-NNN — <question>

Owner: <name> | Date: YYYY-MM-DD
Question: The single decision-relevant question. Not "explore X".
Timebox: <allocation> — a planning allowance, not a deadline
Decision it feeds: ADR-NNN / gate GN / risk RISK-NNN

## Preconditions
Rights: RIGHTS-NNN | Budget cap: <amount and currency> | Environment: <machine, OS, SDK, network state>

## Method
What was done, in enough detail that a different person could repeat it.
Record the corpus, split manifest or fixture IDs, provider/model versions,
prompts and settings before running anything.

## Result
Observed measurements, including failures and anything that did not work.
Report counts and uncertainty; do not summarize toward a preferred conclusion.

## Limitations
Sample size, environment, what was not tested, what could not be observed.

## Decision
Proceed / narrow scope / defer / abandon | Decider: <name> | Date
Follow-on record: <ADR or ticket>
```

## 6. Semantic evaluation run record

A specialized experiment record where output is a semantic claim rather than a system fact. Mirrors EVAL §11.

```markdown
# EVALRUN-NNN — <name>

Evaluation lead: <name> | Annotation lead: <name> | Rights record: RIGHTS-NNN
Date range: YYYY-MM-DD → YYYY-MM-DD

## Frozen configuration
Repository commit · .NET SDK · package versions · corpus digest · split manifest
version and group counts · rubric version · topic taxonomy version ·
normalizer/parser/schema versions · provider, model and revision (or alias plus
observation date) · prompt digests · translation settings · temperature,
top-p, token limits, seed support · validator version · cache settings.

## Reviewer panel
Pseudonyms, language/dialect competence, qualifications, calibration completed,
conflict-of-interest statement, rights attestation reference.

## Data
Unique song/recording groups, findings, eligible negatives, exclusions and why,
deduplication method, artist overlap with development set.

## Results
All measures required by EVAL §10, per stratum, with 95% intervals, method,
seed and resampling count. Report cache reuse. Report invalid-output and
abstention rates so validation does not hide failures behind lower coverage.

## Claims this run supports
State each claim and the exact evidence for it. State the claims it does not
support. A failed comparison is a publishable result.

## Cost
Requests, tokens, retries, actual and estimated spend, budget consumed.
```

## 7. Incident and runbook record

*Retained* for anything triggered by OPS-RB-001–009.

```markdown
# INC-NNN — <summary>

Detected: YYYY-MM-DD HH:MM TZ | Reporter: <name or "automated check">
Runbook invoked: OPS-RB-NNN
Classification: Security/privacy | Playback correctness | Data integrity |
Provider access | Semantic quality | Usability/package
Severity: <impact-based, with reasoning> | Status: Open | Owner: <name>

## What happened
Observed facts only. Do not include secrets or licensed text; reference redacted
artifacts.

## Immediate containment
What was stopped, disabled, revoked or rotated. Include whether any credential
was rotated and by whom, without the value.

## Impact assessment
Which data, users or actions were affected. Whether dispatched actions remain
`OutcomeUnknown`. Whether any external copy or export is outside local control.

## Root cause
Cause, or "under investigation" with the next diagnostic step named.

## Remediation
Fix, added test or fixture, and the gate or risk this closes.

## Closure
Retest result, remaining limitations, prevention, date closed, closed by: <name>
```

Credential exposure follows OPS-RB-001 immediately and stays in a private
channel. The public record may state that a rotation occurred; it must never
contain the value or enough detail to reconstruct it.

## 8. Risk review and acceptance

*Retained* per review cycle for each RISK-NNN in [11](11_GOVERNANCE_DECISIONS_AND_RISKS.md).

```markdown
# RISK-NNN review

Reviewer: <name> | Date: YYYY-MM-DD | Gate context: GN

Current likelihood / impact: <qualitative, with reasoning since last review>
Treatment status: Partially implemented | Implemented | Accepted | Avoided by scope
Evidence: <commits, fixtures, scans, rights records>

Residual risk: <what remains after treatment, stated honestly>
Uncertainty: <what is unknown and why it cannot be known yet>

Decision: Continue | Accept with conditions | Escalate | Remove (state why)
Accepted by: <name> | Expires: YYYY-MM-DD | Conditions: <exact scope>
```

A blanket acceptance covering every risk is not a review. Accepting risk that
concerns missing rights, valid exposed credentials or uncontrolled playback
mutation is not permitted by schedule pressure.

## 9. Release record

*Retained.* One per release attempt, completed with the checklist in [14](14_TRACEABILITY_AND_RELEASE_GATES.md) §5.

```markdown
# REL-NNN — <version>

Release owner: <name> | Date: YYYY-MM-DD | Target commit/tag: <id>
Audience: Developer preview | Private qualification | Public local release
Feature scope: <shipped features>
Explicitly excluded: <features not shipped, and how the product avoids implying them>
Supported environment: OS/RID, package variant, runtime, browser and screen-reader versions

## Gate results
| Gate | Result | Evidence | Approver | Date |
| --- | --- | --- | --- | --- |
| G0 | Not assessed | | | |
| G1 | Not assessed | | | |
| G2 | Not assessed | | | |
| G3 | Not assessed | | | |
| G4 | Not assessed | | | |
| G5 | Not assessed | | | |
| G6 | Not assessed | | | |
| G7 | Not assessed | | | |

Not-applicable gates must be marked Not applicable with the compensating reason.

## Verification environment
Machine, OS, SDK, package and dependency inventory, SBOM reference,
checksums, authenticity/signing status as actually verified.

## Known issues and limitations
Every known defect, degraded state and unsupported claim, in user-facing terms.

## Rollback and support
Rollback procedure reference, restore verification, support owner, security
reporting contact and channel. Do not invent an address or a response time.

## Decision
Ship | Hold | Roll back | Withdraw | Decided by: <name> | Date
```

## 10. Change record

*Retained* for any change to scope, schema, model, prompt, provider, permissions, retention, supported platform or target.

```markdown
# CHG-NNN — <title>

Requester: <name> | Date: YYYY-MM-DD
Type: Scope | Contract | Provider | Rights | Retention | Platform | Target

Reason: <what forces this change>
Affected: PRD-NNN, DATA-NNN, ARCH-NNN, SEC-NNN, ALI-NNN, GATE GN, RISK-NNN
Alternatives considered: <and why they were rejected>
Rights, privacy and cost impact: <including any new external data flow>
Migration and rollback: <or "none required", justified>
Verification required before merge: <tests, fixtures, scans>

Decision: Approved | Rejected | Deferred | Approved with conditions
Decider: <name> | Date: YYYY-MM-DD | Conditions/expiry: <exact scope>
Follow-up review date: YYYY-MM-DD
```

Emergency security fixes may proceed ahead of scheduling, using this record with
the timing noted, but never ahead of credential confidentiality, required
validation or a blocking rights finding.

## 11. Open questions the records cannot answer

These need a decision from a person, not a template. They are listed so they are
not mistaken for missing paperwork:

- Who holds each role, and who provides independent review of security, linguistic and rights questions?
- What is the actual disposition of the credential material found in launch settings?
- Which .NET 10 SDK and servicing policy become the supported baseline?
- Which languages, dialects and topics are claimed, and which are explicitly deferred?
- Which consent flow, model version and price basis apply to external analysis?
- What data does a user actually have the right to import, retain and analyze?
- What does the product claim about its interpretation quality, in words a user could hold it to?

Until these are answered by named people, the pack remains a plan. That is a
legitimate state for it to be in, but it should not be described as a
specification, an approval or a release.

## 12. Version history

| Version | Date | Change | Author role |
| --- | --- | --- | --- |
| 0.1 | 2026-10-09 | Initial record formats for tickets, ADRs, rights/vendor review, experiments, evaluation runs, incidents, risk reviews, releases and changes | Technical/product lead role (TBD) |