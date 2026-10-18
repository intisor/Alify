# 12 — User Experience and Accessibility Specification

**Document ID:** UX · **Version:** 0.1 · **Date:** 2026-10-09
**Status:** Proposed flows/wireframes; not implemented or accessibility-certified
**Accountable owners:** Product/design/accessibility roles, TBD

## 1. Experience principles

Preserve useful existing dashboard/lyrics layouts. Make state and evidence understandable without pretending to guarantee safety or cultural fluency. Keep playback and standalone analysis visibly separate. No onboarding prompt for browser cookies, private Spotify tokens, client secrets, or client modding.

First release: adult personal-use local installation on qualified Windows x64; local rules/import/review can operate offline. Spotify/Gemini need network and eligible configuration. No hosted account required. Optional inference is visibly external, not marketed as entirely on-device.

## 2. Information architecture

| Area | Content and key actions | Restrictions |
| --- | --- | --- |
| Home/setup | Installation health, offline capabilities, supported languages, optional integrations | No secret values/config dump |
| Playback | Current observation/device/queue, freshness, monitoring state, metadata filter, action history | No semantic cards or lyric/model join in official lane |
| Lyric workspace | Permissioned source/version/line view, analysis/evidence, optional local recommendation | Independent from Spotify playback |
| Review | Agreement/disagreement/context needs and revision history | Append-only, no overwrite of model source/results |
| Settings/privacy | Credentials status, provider consent/budgets, retention, disconnect/delete/export | Protected mutation endpoints; masked key availability only |
| Diagnostics/help | Safe error codes, versions, support instructions, approved export preview | No lyrics/tokens/account IDs uploaded by default |

## 3. First-run and recovery flow

1. Launcher checks data path, installation lock, ports, schema compatibility and credential adapter; shows actionable errors rather than opening a dead page.
2. Browser receives a protected local session through the approved bootstrap; no long-lived bootstrap token in ordinary URL history/referrer/logs.
3. Welcome explains independent lanes, scope/limitations and what works offline. Offer an original local fixture with clearly stated provenance.
4. Optional Spotify setup asks for required public client registration/configuration where applicable, verifies exact redirect and eligibility, then opens official OAuth in the system browser. No bundled client secret. Distribution/access arrangements remain an owner decision.
5. Optional Gemini setup stores a key through the OS adapter; provider terms, source-transfer consent and budget are separate screens/actions. Adding a key is not consent to send lyrics.
6. Monitoring/filter off initially. Restart keeps it off until explicit user action.

Recovery covers denied OAuth/mismatched callback, no device, missing permissions, port conflict, locked credential store, network loss, incompatible/corrupt DB and failed migration. Provide supported safe steps and local mode fallback; no disable-security/store-plaintext/private-endpoint workaround.

## 4. Playback screen specification

Suggested layout:

```text
Playback — official metadata only            [Disconnect]
Connection: Connected | Device: observed device
Snapshot observed: time | Fresh / Stale / Unavailable

Current track + required attribution/link/cover where applicable
Playback state and observed queue

Monitoring: Stopped                         [Start / Stop]
Explicit-metadata filter: Off                [Enable]
Explanation: Uses the provider's explicit flag, not lyric meaning.
Some audio can play before a change is detected or skip is accepted.

Action history
Target | Requested at | Acceptance status | Observed effect | Details
```

A large stop control remains available while requests are pending. Paused/no-device/unsupported item and unknown explicit flag are distinct. Queue order labeled observed, not controlled/promised. Stale snapshot visibly dated; disable actions requiring freshness until preconditions rechecked.

`Succeeded` UI text means provider accepted command. Only separately supported effect evidence permits observed playback wording. `OutcomeUnknown` says the request may have reached Spotify and will not be automatically retried; user reviews a fresh snapshot before a deliberate new action. Do not display the new queue head as the skipped target.

## 5. Standalone lyric workflow

### Import

User chooses paste or supported local file; supplies recording/version metadata and permission declaration. Explain separate analysis, storage, reviewer, export/publication and cloud purposes. Do not require private personal details or claim an attestation proves legal rights. Reject oversize/unsupported encoding/input with limits and recovery instructions.

Preview original logical-line numbers, blank/header handling, source completeness/identity and language hints. No automatic confident recording match from title alone. Source edits create a new version.

### Analysis

Choose declared local baseline scope or optional Gemini. Before external processing show exact source/context transfer scope and estimated usage; require valid rights/consent/budget. Permit cancel and display pending/running/finished/cancelled separately from terminal analysis status.

If only a section is sent, retain relevant contextual lines and label scope; do not declare the full song clean. Missing keyword matches are not certified absence of harmful themes.

### Evidence card

```text
Theme: selected topic     Stance: Mentioned
Expression: Figurative    Scope: Verse / annotated narrator
Reason: bounded explanation
Evidence: Source version, lines 12–14 [Open surrounding context]
Language capability: declared scope / unresolved
Review: Unreviewed | Supported review | Disputed
Limitations / alternative interpretation
[Agree] [Disagree] [Needs context] [Leave unresolved]
```

Exact canonical enums/data fields are in [DATA](05_DATA_AND_CONTRACTS.md). Display language labels without pretending the baseline detected unimplemented languages. Review statuses are human dispositions, not calibrated confidence probabilities.

Source view highlights evidence without hiding surrounding context. Preserve original script/diacritics and separate any permitted translation. Optional timestamps labeled with source/precision; line numbers do not imply synchronization.

### Independent policy report

Show policy name/revision, inputs, recommendation Allow/RecommendSkip/Abstain and explanation. Copy: **“This is a standalone recommendation. It does not control Spotify playback.”** Allow means no recommendation under selected policy/available data, not universally safe. Disputed/unsupported/stale input remains visible and defaults to abstention.

## 6. Required honest state copy

| State | Suggested copy / action |
| --- | --- |
| NotFound | No matching permissioned lyric source found. Import another permitted source. |
| ProviderUnavailable | The selected provider could not complete the request. No content verdict was produced. |
| Instrumental | This recording is identified as instrumental by the stated source. No lyric stance applies. |
| Mismatch | This text may belong to another recording/version. Confirm identity before analysis. |
| InsufficientContext | Available context cannot support a reliable interpretation. Add permitted context or review. |
| UnsupportedLanguage | This release does not claim reliable analysis for this language. |
| InvalidOutput | The response failed source/schema/evidence checks and was not accepted. |
| Budget/consent unavailable | External analysis was not sent. Review budget/consent or use supported local baseline. |
| Disputed finding | Reviewers disagree; see both interpretations. No single reading is presented as settled. |
| Unknown action | The request may have been applied. Alify will not automatically resend it. |
| Stale observation | Display is older than the configured freshness window; it may not match current playback. |
| Semantic bridge blocked | Semantic recommendations do not control Spotify in this release. |

Never label absence/error “clean,” “safe for kids,” “verified harmless,” or “AI understands Yoruba perfectly.” Do not repeat unsupported interpretation of a slang phrase as fact.

## 7. Accessibility acceptance

Proposed WCAG2.2 AA target for supported flows; automated checks do not certify conformance. Required recorded manual keyboard and screen-reader review:

- Accessible page headings, landmarks, labels, errors and consent forms.
- Visible focus, logical tab order, no keyboard trap; dialogs return focus.
- Live updates preserve focus, selection, review drafts and reading position.
- Coalesced polite status announcements; errors/action uncertainty appropriately announced; no poll-by-poll alert storm.
- Text labels accompany stance/status colors; sufficient contrast, zoom/reflow and reduced-motion support.
- Source/evidence navigation works without mouse; tables/line references have accessible semantics.
- Bidirectional scripts/Unicode handling, language attributes where known and faithful diacritics.
- Confirmation dialogs explain destructive/charged actions and remote limitations clearly.
- No timed consent pressure, inaccessible toast-only errors or notification spam.

Target Windows NVDA plus qualified supported-browser combinations first. Document actual test OS/browser/reader versions and deviations. Other platform claims require equivalent evidence.

## 8. Privacy, deletion, export and usability pilot

Privacy controls distinguish disconnect Spotify, remove Gemini key, revoke cloud consent, delete a source and descendants, and erase all local data. Preview scope/controlled backups; display pending/partial cleanup and remote/export/physical limits. No late asynchronous result may recreate a deleted item.

Export defaults to metadata/minimal evidence only where rights permit; warn before full-text export and show fields. Diagnostics export separate and content/secret-free by default.

Pilot task script: install/start offline; view original fixture; inspect a claim's lines; leave dissent; resolve a revision conflict; deny cloud consent; cancel a job; stop monitoring; interpret unknown skip; disconnect/delete; recover from port/provider error. Record assistance, errors and completion, not fabricated satisfaction/retention statistics. Use [QA](08_QUALITY_AND_VALIDATION.md) and [release gates](14_TRACEABILITY_AND_RELEASE_GATES.md).
