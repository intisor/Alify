# Alify — Decoupled Music Playback & Semantic Lyric Ledger

**Version:** 2.0 · **Target Framework:** .NET 10 (`net10.0`) · **OS:** Windows / Cross-platform

Alify is a local-first application designed with strict separation of concerns into two independent architectural lanes:
1. **Official Spotify Observation & Filter Lane (`Alify.Spotify`)**: Observes currently playing tracks, manages playback queues, filters explicit tracks with user opt-in, and broadcasts events over bounded Server-Sent Events (SSE). Uses client-side PKCE OAuth (RFC 7636) with zero stored client secrets.
2. **Independent Semantic & Lyric Foundation Lane (`Alify.Lyrics` & `Alify.Core`)**: Manages permissioned lyric imports, canonical line normalization, an immutable SQLite ledger, strict evidence validation, deterministic policy recommendations, and append-only reviewer audits.

> [!IMPORTANT]
> **Strict Decoupling Invariant:** The semantic analysis engine never automates or issues Spotify playback commands, and the Spotify observation lane never ingests or requires third-party lyrics.

---

## Architecture Overview

```
                      ┌────────────────────────────────────────┐
                      │          Local User Interface          │
                      │   (http://127.0.0.1:5000 & 5001)       │
                      └───────────────┬────────────────────────┘
                                      │
              ┌───────────────────────┴───────────────────────┐
              │                                               │
   [Spotify Observation Lane]                     [Semantic & Lyric Lane]
   - Local PKCE OAuth (RFC 7636)                  - Permissioned Import (Paste/File)
   - Read-only playback observation               - Immutable SQLite Ledger (WAL mode)
   - Opt-in explicit metadata filter              - CRLF Normalizer & SHA-256 Hashing
   - Bounded SSE stream (DropOldest)              - Strict Evidence Validator (Anti-hallucination)
   - Playback fault & skip attribution            - Offline Heuristics & Consented BYOK Gemini
   - No lyrics dependency                         - Deterministic Policy Evaluator (Allow/Skip/Abstain)
                                                  - Append-Only Review Audits
```

---

## Core Capabilities

### 1. Trustworthy Spotify Core (`Alify.Spotify`)
- **Local PKCE Authentication:** Uses RFC 7636 SHA-256 code challenge generation (`PkceUtil.cs`) with least-privilege scopes. Eliminates hardcoded client secrets.
- **Accurate Skip Attribution:** Typed `SkipActionResult` preserves the original target track identity during playback advancement, eliminating post-dequeue attribution errors (`AUD-003`).
- **Bounded SSE Broadcasts:** High-concurrency Server-Sent Events backed by `System.Threading.Channels` with bounded queues (`DropOldest` backpressure), preventing memory leaks on slow or dropped connections.
- **Playback Fault Matrix:** Graceful handling of network timeouts (`OutcomeUnknown`), rejected calls 401/403/429 (`Failed`), and empty queues without index exceptions.

### 2. Independent Semantic Foundation (`Alify.Core`)
- **Permissioned Import:** Validates rights attestations, strips UTF BOMs, enforces 64KB/128KB payload bounds, and parses section tags (`[Verse]`, `[Chorus]`, `[Bridge]`).
- **Immutable Source Ledger:** Persists records in SQLite (`alify_ledger.db`) using foreign key cascades, WAL mode, transaction isolation, and serialized write locks.
- **Exact Evidence Verification:** `EvidenceValidator` verifies that citations match exact 1-based line bounds and canonical text from the ledger, rejecting fabricated model excerpts.
- **Deterministic Policy:** `DeterministicPolicyEvaluator` computes side-effect-free recommendations (`Allow`, `RecommendSkip`, `Abstain`) against user preferences and reviewer audits.
- **Consented BYOK Gemini Adapter:** Optional LLM integration requiring explicit user disclosure, transactional daily budget reservations, and error mapping to `Unavailable` or `InvalidOutput`.
- **Append-Only Review Audits:** Enables users and auditors to dispute or confirm stance findings (`Agree`, `Disagree`, `NeedsContext`, `Unresolved`) with version-conflict protection.
- **Safe Diagnostics:** `SafeDiagnosticsService` strips Bearer tokens, Gemini API keys, and client secrets from diagnostic reports.

---

## Quick Start & Running Locally

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/) (`10.0.100` or newer)

### Run Both Services
Execute the coordinated PowerShell launcher (which validates port availability and binds explicit loopback addresses):

```powershell
.\run-all.ps1
```

Or on Linux / macOS:
```bash
chmod +x run-all.sh
./run-all.sh
```

- **Alify Lyrics & Semantic UI:** `http://127.0.0.1:5000`
- **Alify Spotify Dashboard:** `http://127.0.0.1:5001`

---

## Automated Test Suites

The solution features automated unit and fault matrix test suites with 0 compilation warnings and 0 errors:

```powershell
dotnet test Alify.sln
```

- `tests/Alify.Core.Tests`: Covers RFC 7636 PKCE vectors, canonical normalizer, heuristic stance fixtures, evidence validator, SQLite ledger transactions, policy evaluator, Gemini structured adapter, and safe diagnostics redaction.
- `tests/Alify.Spotify.Tests`: Covers PKCE token exchange, skip attribution outcomes, bounded SSE channels, and playback fault matrix scenarios.