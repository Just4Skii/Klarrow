# Final Submission Verification (2026-10-19)

Pre-submission quality gate for the Klarrow Pitchathon submission pack. Every item below was executed against the current working tree; nothing in this document is inferred from earlier sessions unless stated.

- **Commit verified:** `14858ea50d43b924698ebf8401a2edb5518dc81a` — "Fix pitch deck and one-pager PDFs (restore binary content)"
- **Verification window:** 2026-10-19, approx. 05:55–08:05 SAST (UTC+02:00), following the 2026-10-18 evening build-and-export session
- **Working tree:** 61 git-status entries — 59 staged pre-submission changes plus 2 intentionally untracked run-output directories (`data/evaluation/runs/`, `results/evaluation/runs/`). No commits were created during this gate.
- **Environment:** Windows, .NET SDK 10.0.202 (runtime 10.0.6), Node v24.15.0, npm 11.12.1, Playwright with MS Edge, Ollama at `http://127.0.0.1:11434` serving `llama3.2:3b`.
- **Scope rule applied:** no features added, no benchmark numbers changed, no deck content or claims changed. Run 4 artefacts were treated as read-only.

## Gate results

| # | Check | Result | Evidence |
|---|-------|--------|----------|
| 1 | `dotnet restore` | **PASS** | Completes; NU1902/NU1903 advisories only (see Warnings) |
| 2 | `dotnet build -c Release` | **PASS** | 0 errors, 36 warnings (advisories plus pre-existing code warnings) |
| 3 | `dotnet test` | **PASS** | 43 passed / 0 failed / 0 skipped |
| 4 | `npm ci` | **PASS** | 33 packages installed (~25 s) |
| 5 | `npm run lint` | **PASS** | exit 0; 2 pre-existing fast-refresh warnings (`primitives.tsx:52,159`) |
| 6 | `npm run build` | **PASS** | `tsc -b && vite build` complete |
| 7 | Playwright test count | **PASS** | `npx playwright test --list` → 25 tests in 3 files (21 core + 1 count-consistency + 3 screenshots) |
| 8 | Playwright suite | **PASS** | 25/25 passed in 54.1 s, 1 worker, MS Edge, live API on port 5099 |
| 9 | Live Llama smoke | **PASS** | `POST /api/extraction/smoke` → `success=true`, `model=llama3.2:3b`, 13,119 ms |
| 10 | Trace + export paths | **PASS** | Trace run 10 nodes / 8 edges; JSON, CSV, HTML and XLSX all valid (details below) |
| 11 | Scenario A/B/C consistency | **PASS** | 20/20 document hashes and sizes match the locked manifests; ground truth parses |
| 12 | Deterministic and Llama result artefacts | **PASS** | `eval-dev-20261019-final` has all 3 scenarios × 2 modes with parseable metrics and raw runs |
| 13 | Deck / one-pager current and consistent | **PASS** | Patched text present in both PPTX files; PDFs 10/1 pages and byte-identical to the recorded post-patch exports; 11 PNGs at 1920×1080 |
| 14 | Product name and claim sweep | **PASS** | No user-facing "RecallGraph" as a product name; no unqualified forbidden claims |

## 1. Backend build and tests

Commands run from the repository root `D:\code randoms\Recall`:

```
dotnet restore
dotnet build -c Release
dotnet test
```

- `dotnet restore` and the Release build complete with 0 errors. The 36 warnings are transitive package advisories plus pre-existing `CS9113`/`EF1002` code warnings; none were introduced by the 2026-10-19 changes.
- `dotnet test`: **43 passed / 0 failed / 0 skipped**. This includes the 40 Run 4-era tests plus the three 2026-10-19 additions (two earlier task additions and the Llama provider-parsing regression test).

## 2. Frontend build, lint and browser suite

Commands run from `src\frontend`:

```
npm ci
npm run lint
npm run build
npx playwright test --list
npx playwright test --reporter=line
```

- `npm ci` installed 33 packages; `npm run lint` exited 0 with only the two known `react(only-export-components)` warnings; `npm run build` completed.
- `--list` reports **25 tests in 3 files**, confirming the suite count after the Task 4 additions (no assumption carried over from earlier runs).
- Full run: **25/25 passed (54.1 s)** against a disposable API instance on `http://127.0.0.1:5099` (Release binary, temporary data root, demo seeding enabled). Screenshot specs wrote only to the ignored `src/frontend/shots/` directory.

## 3. Live API, Llama smoke, trace and exports

The API was started from the Release build with a temporary data root (`Storage__DataRoot`, `Storage__UploadsPath` under the user temp directory) and the demo seed path pointed at the repository's `seed/evidence`. Demo seeding was enabled; Llama configured via `LLAMA_API_BASE_URL=http://127.0.0.1:11434/v1`, `LLAMA_MODEL=llama3.2:3b`, `LLAMA_TIMEOUT_SECONDS=180`.

| Step | Result |
|------|--------|
| `GET /api/health` | 200 `{"status":"ok"}` |
| `POST /api/admin/seed {"reset":true}` | 200 — 24 documents, 137 facts, 13 links |
| `GET /api/workspace/home` | 200 — "Savanna Bites (Pty) Ltd - synthetic demonstration workspace" |
| `POST /api/extraction/smoke` | 200 — `success=true`, `llama3.2:3b`, **13,119 ms** |
| `POST /api/trace-runs` (`raw-lot` / `RM-0427`, `both`) | 200 — runId `ddbac6c5-2aaf-48b8-a8bf-54202cf7908b`, 10 nodes, 8 edges |

Export verification (`GET /api/trace-runs/{id}/export?format=…`):

| Format | Size | Validation |
|--------|------|------------|
| JSON | 17,560 B | parses; 32 top-level keys |
| CSV | 8,673 B | UTF-8 BOM present; 24 non-empty lines |
| HTML | 7,971 B | pilot/usage notice present; closes with `</html>` |
| XLSX | 34,606 B | valid ZIP (22 entries) containing `xl/workbook.xml`, 5 worksheets and 4 tables |

Evidence retained at `C:\Users\mphoj\AppData\Local\Temp\opencode\qg-exports\` (files plus `_summary.json`), together with the API stdout/stderr logs from the verification instance. These are temporary verification artefacts, not submission content.

## 4. Evaluation suite integrity

- **Scenario inputs:** Scenario A has 7 documents, B has 7, C has 6. All 20 files match the SHA-256 and byte size recorded in their `manifest.json`; all `ground-truth.json` files parse and each scenario keeps its README. Manifests are locked at 2026-10-17 (A) and 2026-10-19 (B, C).
- **Consolidated results:** `results/evaluation/model-value-evaluation.json` (run `eval-dev-20261019-final`, generated 2026-10-18T19:01:43Z) parses with 3 entries and 6 verdicts; the CSV has the matching 6 data rows.
- **Run directories:** `results/evaluation/runs/eval-dev-20261019-final/` contains all three scenarios × two modes (`deterministic_only`, `llama`), each with `metrics.json`, `raw/raw-run.json`, `health.json` and API logs. All parse; `manifestIntegrity` is all-OK in every one.
- **Recorded verdicts (unchanged):** deterministic mode 13/13 on all three scenarios; llama mode 13/13 on A and B, 11/13 on C with only extra `Candidate`-state links and nothing auto-confirmed. Ten historical run directories (deterministic `a`–`f`, `llama-1`–`3`) remain available for audit.
- The bulky raw run directories stay untracked by design; scenario inputs, the runner script and the consolidated JSON/CSV are staged.

## 5. Deck and one-pager

- **PPTX content:** pitch deck has 10 slides; slide 7 contains "three-scenario evaluation suite" and "13/13", and its speaker notes contain "never auto-confirmed" plus the guardrail "Do not present the suite as accuracy evidence". The one-pager has 1 slide containing "a three-scenario evaluation suite passes all locked checks in deterministic mode" and "The Run 4 demo is one authored scenario". Both files say "Klarrow" and contain zero "RecallGraph" in visible text.
- **PDFs:** deck 10 pages / 628,150 B; one-pager 1 page / 290,292 B. Byte sizes equal the recorded post-patch export values and both PDFs are newer than the patched PPTX files (PPTX 2026-10-18 22:54 SAST; PDFs 22:58 SAST).
- **PNGs:** `rendered/slide-01..10.png` and `rendered/one-pager.png` all exist, are valid PNGs and are 1920×1080.
- **Tooling note:** automated PDF text extraction was unavailable in this environment (`pypdf` absent, fonts subset-encoded, no `pdftotext`). PDF/PPTX consistency therefore rests on the identical page counts, byte sizes matching the recorded post-patch export, the later PDF timestamps, the verified PPTX source text, and the Task 5 visual check of slide 7 and the one-pager render.

## 6. Naming and claims sweep

**Product name.** 45 user-facing Markdown files, the deck/one-pager text and the frontend source were scanned. The 33 "RecallGraph" occurrences are all internal identifiers — solution/namespace/project paths (`RecallGraph.slnx`, `src/RecallGraph.*`), configuration keys (`RecallGraph__ApiKey`, `RecallGraph:ApiKey`), a browser storage key, and historical asset-bundle folder names. None uses "RecallGraph" as the product name in prose, and the frontend source contains zero occurrences. The health endpoint's internal `service` field is not rendered by the UI (it shows only the access-control notice). User-facing material consistently says **Klarrow**.

**Forbidden claims.** Nine phrases were searched across submission docs, deck/one-pager text, the repository README and the pitch/pilot/evaluation/security docs: 91 hits were reviewed individually. Every hit is a negative or limitation statement ("not an accuracy study", "no accuracy… claim", "not production-ready", "not compliant"), a do-not-claim guardrail list, or the South African regulatory phrase "accurate labelling". There are **zero** occurrences of "validated by customers", "pilot booked", "guarantees" or "AI-only discovery", and no unqualified accuracy, time-savings, production, compliance or AI-superiority claim. Deck and one-pager both carry explicit "NOT ESTABLISHED" / "remain unmeasured" language.

Full sweep output: `C:\Users\mphoj\AppData\Local\Temp\opencode\qg-artefact-verification.json`.

## 7. Run 4 immutability

All 52 files (8.8 MB) under `D:\code randoms\Recall-local\run4` were SHA-256 hashed before and after the gate. `Compare-Object` reports **no differences**: every hash is identical. No verification step wrote to the Run 4 directory; the live API and browser suite used temporary data roots, and exports were written to a temp directory.

Snapshots: `C:\Users\mphoj\AppData\Local\Temp\opencode\run4-snapshot-before.txt` and `run4-snapshot-after.txt`.

## 8. Warnings, observations and residual risk

- **NuGet advisories (pre-existing, documented):** high-severity advisories for transitive `Microsoft.OpenApi` 2.0.0 and `SQLitePCLRaw.lib_e_sqlite3` 2.1.11; moderate/high advisories for `System.Security.Cryptography.Pkcs`/`.Xml` 9.0.0. These are already tracked in the validation report and known-production-gaps documentation; the gate did not change dependencies.
- **Pre-existing warnings:** 2 frontend fast-refresh warnings and the `CS9113`/`EF1002` backend warnings remain; none block the submission.
- **Transient runtime incident (environmental, not reproduced):** during the first two live-check attempts of this gate, the API process was terminated by the .NET runtime with exit code `0x80131506` ("internal error in the .NET Runtime"), at 06:36 and 07:04 SAST; the managed stacks ended inside `AdminController.Seed` and `WorkspaceController.Home`. The same Release binary, runtime and data flows then completed **five consecutive clean runs** (isolation sequence, PowerShell seed/reset sequence, full node live-check replay, Llama-enabled full export verification, and the complete 25-test Playwright run). No code was changed. Windows Defender security-intelligence updates had installed/downloaded at 05:05/05:59 the same morning; the crash signature points at the runtime rather than Klarrow logic. **Residual risk:** if the API exits unexpectedly during the live pitch, relaunch `scripts/start-klarrow.ps1` — seeded demo data persists in the data root. Watch the API window during the demo.
- **PDF tooling limitation:** see section 5.
- **Known product limitations (unchanged):** Run 4 is one authored fictional scenario (n=1); no measured accuracy, time savings, customer demand or pilot evidence; Llama mode adds candidate noise on fragmented evidence (11/13 on scenario C, nothing auto-confirmed); no OCR, SSO/multi-user access control, or production hardening.

## Confirmations

- **Run 4 artefacts unmodified:** confirmed — 52/52 hashes identical before and after.
- **No fabricated claims:** confirmed — all claims are qualified, evidence-linked or explicitly negative; no customer, pilot, revenue, accuracy, time-saving, production-readiness or compliance claim exists.
- **Submission pack ready:** confirmed — deck (10 slides / 10-page PDF / 10 renders), one-pager (1 slide / 1-page PDF / render), supporting docs, 43/43 backend tests, 25/25 browser tests, live Llama smoke and all four export formats verified at the recorded commit.

## Command log

| Working directory | Command |
|-------------------|---------|
| repo root | `dotnet restore` |
| repo root | `dotnet build -c Release` |
| repo root | `dotnet test` |
| `src\frontend` | `npm ci` |
| `src\frontend` | `npm run lint` |
| `src\frontend` | `npm run build` |
| `src\frontend` | `npx playwright test --list` (25 tests in 3 files) |
| `src\frontend` | `npx playwright test --reporter=line` (25 passed, 54.1 s) |
| repo root | `src\RecallGraph.Api\bin\Release\net10.0\RecallGraph.Api.exe` with `ASPNETCORE_URLS=http://127.0.0.1:5099`, `ASPNETCORE_ENVIRONMENT=Development`, temporary `Storage__*` paths, `Seed__DemoEnabled=true`, `LLAMA_API_BASE_URL=http://127.0.0.1:11434/v1`, `LLAMA_MODEL=llama3.2:3b`, `LLAMA_TIMEOUT_SECONDS=180` |
| temp | `node qg-live-checks.mjs` (seed, workspace, smoke, trace, four exports) |
| temp | `python qg-artefact-verification.py` (scenario hashes, results, deck/one-pager, name and claims sweeps) |
| temp | `python -c "import zipfile …"` (XLSX structure) |
| repo root | PowerShell `Get-FileHash` sweep of `D:\code randoms\Recall-local\run4` before/after, `Compare-Object` |
| repo root | `git status --porcelain` (61 entries), `git rev-parse HEAD` |
