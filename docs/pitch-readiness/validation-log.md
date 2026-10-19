# Klarrow — Validation Log

**Date:** 2026-10-18  
**Scope:** Run 4 engineering verification and XLSX export closeout  
**Status:** Internal engineering record; not independent validation, certification, compliance evidence, or production-readiness evidence.

## Environment and scope

- Repository: `Just4Skii/Klarrow`, branch `main`.
- Backend: .NET 10 / ASP.NET Core with EF Core and SQLite.
- Frontend: React, TypeScript, Vite.
- Model: local Ollama `llama3.2:3b`.
- Run 4 data: isolated SQLite root and seven authored fictional source documents.
- This is not a fresh-clone build performed by this log’s author. The results below are the recorded engineering checks from the Run 4 and export-closeout sessions.

## Recorded checks

| Check | Recorded result | Status / limitation |
|---|---|---|
| Full .NET test suite | 40 passed, 0 failed | Pass in the recorded local run |
| Targeted XLSX test | 1 passed, 0 failed | Separate targeted check |
| Frontend lint | 0 errors; 2 pre-existing fast-refresh warnings | Pass with warnings |
| Frontend production build | TypeScript and Vite build passed | Pass |
| Playwright suite | 25 passed, 0 failed | Passed before the XLSX-specific closeout |
| Local Llama smoke test | `success=true`; `llama3.2:3b`; one fact returned | Functional smoke test only |
| Run 4 extraction | Seven documents; 15 AI facts; 57.9 seconds wall-clock | Functional run, not accuracy evidence |
| Run 4 trace | `FL-1021A`, Bosveld Tuckshop Distributors, Highveld Retail Group; possible scope empty; 77 ms | One authored scenario |
| JSON export | Valid JSON; 32 top-level keys; 38 audit rows | Pass in recorded check |
| CSV export | UTF-8 BOM; 52 rows; invariant decimal `180.0` | Pass in recorded check |
| HTML export | Pilot-workspace provenance notice present | Pass in recorded check |
| XLSX export | Five worksheets; tables; frozen headers; wrapped cells; print setup | Pass in recorded closeout |
| Browser download / print check | XLSX download 30,664 bytes; 14 printed pages without clipping after row-height fix; HTML print 5 pages | Local browser validation, not an independent cross-platform Excel test |
| GitHub Actions | [Run 39145909433](https://github.com/Just4Skii/Klarrow/actions/runs/39145909433), on cleanup commit `b4b870003006f7e6e54c5ee8be4373b592534597` | Success: restore, build, 40/40 .NET tests, frontend install, lint and production build |

## Run 4 observed state

- Seven authored fictional documents; one local model run and one operator.
- 50 facts: 15 AI candidate facts and 35 deterministic-parser facts.
- AI grounding: 4 `quote_verified`, 3 `context_match_value_adjacent`, 8 `identifier_matched_quote_unverified`, 0 `needs_review_unverified_source`.
- Seven AI facts were reviewed; eight identifier-only AI facts were left unreviewed.
- Final fact review: 14 confirmed / 36 unreviewed.
- Eight relationship proposals: seven confirmed and one false-positive dispatch relationship rejected.
- The `RM-O873` / `RM-0873` alias was deferred; no alias was confirmed.
- Final Run 4 trace: `FL-1021A`, Bosveld Tuckshop Distributors and Highveld Retail Group (Pty) Ltd; possible scope empty; `FL-1034C` excluded from the confirmed trace.
- Mass balance remained insufficient: 320 kg received versus 180 kg recorded consumed and 6,600 downstream units; no conversion was invented.
- Three open findings: 36 unreviewed facts, an ambiguous identifier, and a quantity mismatch.

## What the results do not establish

The benchmark is one authored fictional scenario (`n=1`). No extraction accuracy, precision/recall, false-negative rate, manual-vs-tool time savings, real-document performance, customer demand, or willingness to pay was measured. The current scenario does not isolate a unique accuracy contribution from the Llama model against a deterministic-only baseline.

The separate browser E2E flow from the closeout used a fresh seven-document / 35-claim review scenario and returned five confirmed links, `FL-1021A`, two customers, and zero gaps. This is a distinct run and must not be merged with the Run 4 metrics above.

## Open risks

- The hosted CI run succeeded, but emitted dependency-vulnerability warnings for Microsoft.OpenApi 2.0.0, SQLitePCLRaw.lib.e_sqlite3 2.1.11, System.Security.Cryptography.Pkcs 9.0.0, and System.Security.Cryptography.Xml 9.0.0. It also emitted EF1002 warnings for interpolated SQL in schema initialization, unused-parameter compiler warnings, and two frontend fast-refresh warnings. The schema migration interpolations currently use code-owned, fixed identifier/definition lists rather than source-document values; this still deserves a deliberate security review, not a blanket claim of no risk. Re-audit and patch dependency advisories before any deployment; run regression tests after updates.
- API-key middleware is optional and leaves health, extraction-status and capabilities endpoints public when configured. No production security review has been completed.
- OCR for scanned images, RBAC/SSO, production deployment/monitoring, real customer/practitioner validation and independent real-document evaluation are not complete.
- The recorded hosted run verifies the cleanup commit only. Future commits should be judged from their own current Actions result; a green build is not a security audit or deployment approval.

## 2026-10-19 addendum — evaluation suite (deterministic vs Llama)

**Scope:** Tasks 1–2 of the submission plan on the current working tree. Three authored scenarios with
locked, manifest-hash-verified ground truth, executed by `scripts/evaluation/run-scenario.mjs` in both
extraction modes; consolidated run id `eval-dev-20261019-final` (3 scenarios × 2 modes = 6 runs; 13
boolean checks per run against the locked expectations).

| Run | Verdict | Notes |
|---|---|---|
| A run-4 baseline / deterministic | PASS 13/13 | 35 parser facts; 0 AI rows by design |
| A run-4 baseline / llama | PASS 13/13 | 50 facts (15 AI); claim recall 6/6 |
| B forward trace / deterministic | PASS 13/13 | 37 parser facts |
| B forward trace / llama | PASS 13/13 | 47 facts (10 AI); claim recall 3/5 |
| C fragmented evidence / deterministic | PASS 13/13 | 35 parser facts |
| C fragmented evidence / llama | FAIL 11/13 | 47 facts (12 AI); claim recall 1/4; 3 extra `Candidate` links, nothing auto-confirmed |

- .NET test suite at this revision: **43 passed, 0 failed** (the Run 4-era 40 tests plus two earlier
  additions and one new provider-parsing regression test).
- **Provider hardening disclosure:** the first llama executions (`eval-dev-20261019-llama-1`,
  `eval-dev-20261019-llama-2`) failed scenario B extraction with `Fact rawValue must be a string.` —
  the model returned a non-string `rawValue` and the provider rejected the whole response, aborting
  the run with zero AI facts. `LlamaProvider.TryParse` was hardened to skip malformed rows and coerce
  numeric/boolean `rawValue` to literal text; grounding, review and confirmation rules are unchanged,
  and the failure mode is now covered by a regression test. All post-fix results are the ones
  tabulated above; pre-fix run ids are retained for audit.
- Report and artefacts: `docs/evaluation/model-value-evaluation.md`;
  `results/evaluation/model-value-evaluation.json` / `.csv`; per-run metrics, raw run records and API
  logs under `results/evaluation/runs/eval-dev-20261019-final/`.
- **What this does not establish:** three authored scenarios, one small local model, no
  accuracy/precision/recall study, no time-savings measurement, no real-document evidence. Scenario C
  shows llama-mode candidate noise on fragmented evidence; it never enters confirmed scope without
  human review.

**Decision:** suitable for controlled prototype demonstration and competition submission, subject to rehearsing the exact submitted build. Not suitable for production or regulatory reliance.
