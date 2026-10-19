# Klarrow

AI-assisted evidence-reconciliation workbench for disconnected food traceability records.

> **Disclaimer:** Investigation aid only. Not a food-safety clearance, product release, mandatory withdrawal, regulatory compliance or legal decision system. The default workspace is preloaded with clearly-labelled synthetic demonstration data; a pilot installation should disable synthetic seeding (`Seed:DemoEnabled=false`) before importing authorized enterprise records.

---

## 1. What is Klarrow?

Klarrow is an evidence-reconciliation workbench designed for Food Safety & Quality Assurance (FSQA) professionals responding to contamination alerts, supplier inquiries, or regulatory/customer audits. It can support work shaped by US FSMA 204 or equivalent customer requirements, but it is not a compliance certification and does not assert that FSMA 204 applies in South Africa.

Unlike rigid ERP platforms (SAP, NetSuite) that assume all data lives in uniform tables, or vendor portals that require extensive supplier onboarding, Klarrow ingests raw, heterogeneous documents (PDFs, CSVs, Excel sheets, text logs, emails), extracts candidate facts with verbatim source grounding, surfaces near-match typo proposals, generates candidate trace relationships across supply chain tiers, and traverses multi-tier graphs while strictly separating **Confirmed Affected Scope** from **Possible Exposure Scope**.

---

## 2. Core Architecture & Pipeline

```
[Raw Evidence Files] (PDF, CSV, XLSX, EML, TXT, LOG)
         │
         ▼
[Deterministic & AI Parsers] ─── Verbatim Text Grounding (No Line-1 Fallbacks)
         │
         ▼
[Extracted Claims / Facts] ───── Human Review Desk (Confirm / Reject / Edit)
         │
         ├────────────────────── Invalidation Cascade (Rejection Demotes Links)
         ▼
[Relationship Engine] ────────── Extracts Multi-Tier Candidates (Raw ➔ Batch ➔ Finished ➔ Customer)
         │
         ▼
[Trace Engine Traversal] ─────── Dual-Path BFS Graph Traversal:
                                  ├── Confirmed Scope (Human-Confirmed Links)
                                  └── Possible Scope (Candidate & Re-Review Paths)
         │
         ▼
[Reconciliation Findings] ────── Gap Detection, Mass Balance Discrepancy & Caveats
         │
         ▼
[Auditable Exports] ────────────── XLSX, JSON, CSV & HTML with Provenance
```

---

## 3. Quick Start (Windows PowerShell)

### Prerequisites
- **.NET SDK 10** (`dotnet --version`)
- **Node.js 24+** & **npm**
- **Ollama** (optional, for local `llama3.2:3b` extraction)

### Backend API
```powershell
dotnet build
dotnet run --project src/RecallGraph.Api --urls http://127.0.0.1:5099
```

### Frontend (in a separate terminal)
```powershell
cd src/frontend
npm install
npm run dev
```
Open [http://localhost:5173](http://localhost:5173).

### One-click start (Windows)
Double-click **`start-klarrow.cmd`** in the repository root. It starts the API and the web portal in their own windows (skipping ones already running), waits until both are healthy, reports whether local Ollama is available for live AI extraction, and opens the browser. Service logs stay visible in the `Klarrow API` and `Klarrow Web` windows; close those windows to stop the system. On first use it also installs the frontend dependencies automatically. To preview what it would start without launching anything: `powershell -NoProfile -File scripts\start-klarrow.ps1 -DryRun`.

---

## 4. Modes & Security Guards

### Public Demo Deployment (Render blueprint)

`render.yaml` deploys this repository as a single Docker container (API + pre-built UI in `wwwroot/`) with:

| Guard | Mechanism |
| :--- | :--- |
| **No public uploads** | `Demo__PublicMode=true` -> `POST /api/documents/upload` returns 403; the UI also hides upload affordances (`VITE_PUBLIC_DEMO=1` build flag) |
| **Synthetic seed only** | `Seed:DemoEnabled=true` (default): the Savanna Bites workspace is pre-seeded and resettable |
| **Abuse control** | `RateLimit__Enabled=true`: per-IP fixed window (120 req/min default, 429 beyond) — off in local dev and the test harness |
| **Cross-origin UI** | `CORS__Origins__N` entries allow the UI to be hosted separately (e.g. GraffGrid Pages `/work/klarrow`) while the API lives here |
| **API key** | `RecallGraph__ApiKey` remains optional; public demo deployments leave it unset and rely on the limits above |

### Synthetic Demonstration vs. Pilot Workspace
- **Synthetic demonstration mode (default):** Pre-seeds the `Savanna Bites` workspace with labelled synthetic records (raw lot `RM-0427`, finished lots `FL-0514`, `FL-0515`, and lookalike `FL-0516`). The UI shows a persistent synthetic-data banner, the API reports the workspace mode and synthetic document count, and exports carry a synthetic-data provenance notice.
- **Pilot mode:** Run with `$env:Seed__DemoEnabled="false"` to initialize a clean, unpopulated workspace with no synthetic records. Pilot exports carry a pilot-workspace notice instead. Seeded synthetic records are always tagged wherever they appear, including when they are mixed into a pilot workspace.

### Single-Tenant API Key Guard
```powershell
$env:RecallGraph__ApiKey="your-secure-random-token"
dotnet run --project src/RecallGraph.Api --urls http://127.0.0.1:5099
```
When configured, application routes require the `X-Api-Key` header except `/api/health`, `/api/extraction/status`, and `/api/capabilities`. The frontend prompts for the key and stores it in session storage only. This optional guard is intended for a controlled local/demo setup; it is not a substitute for production authentication, authorization, TLS, or deployment hardening.

---

## 5. Supported Formats & Security Protections

- **Server Allowlist:** PDF (searchable), CSV, TSV, XLSX, XLS, TXT, LOG, EML, PNG, JPG, JPEG, GIF, BMP (Max 10 MB).
- **Duplicate Detection:** Ingested files are hashed with SHA-256; duplicate byte payloads are rejected to prevent record pollution.
- **Scanned Images:** Stored securely with `retained_not_ocr_processed` badges (not hallucinated).
- **CSV Formula Injection Defense:** Exported CSV packages sanitize trigger characters (`=, +, -, @, \t, \r, %`) by prefixing `'` to protect operator spreadsheets from command injection.

---

## 6. Verification & Automated Test Suite

Klarrow includes an integration test harness using temporary SQLite databases and isolated storage roots. A GitHub Actions workflow runs the .NET build/tests and frontend lint/build on pushes and pull requests. Check the repository’s Actions tab for the latest run status.

```powershell
# Run the 43 automated integration tests
dotnet test

# Build and lint frontend
cd src/frontend
npm run lint
npm run build
```

**Latest recorded test results (43/43 .NET tests; 25/25 Playwright tests):**
- Document upload -> relationship proposal generation -> operator confirmation -> confirmed trace scope.
- Strict isolation of Candidate links from Confirmed finished lot and customer exposure.
- Invalidation cascade demoting confirmed links when an underlying supporting fact is rejected or edited.
- Multi-investigation gap finding isolation preventing cross-case resolution.
- Dynamic suspect lot detection from active investigation metadata.
- Grounding taxonomy: value-in-quote, adjacent-line context, identifier-only and unlocated claims are kept distinct; no line-1 fallback.
- Rejecting a false-positive relationship removes an orphaned auto-registered entity while preserving audit history.
- Workspace open-gap count uses persisted findings, matching the Findings page.
- Near-match typo proposals (`O` vs `0`) requiring human review without auto-merging.
- CSV formula-trigger sanitization and HTML provenance report generation.
- Five-sheet XLSX workbook export, with worksheet tables, frozen headers, wrapped evidence cells and print layout checks.
- Malformed model-response rows are skipped or coerced without discarding the valid extracted facts (regression test).

The repository also contains a scenario evaluation suite (`scripts/evaluation/run-scenario.mjs`) that
runs three authored scenarios in deterministic and llama modes against locked, hash-verified ground
truth. Latest consolidated run `eval-dev-20261019-final`: deterministic mode passed all 13 checks on
all three scenarios; llama mode passed A and B and scored 11/13 on the fragmented-evidence scenario
where extra candidate links remained in `Candidate` state. See
[`docs/evaluation/model-value-evaluation.md`](docs/evaluation/model-value-evaluation.md) and
`results/evaluation/model-value-evaluation.json`.

---

## 7. Pitch Readiness & Customer Discovery Documentation

Detailed operational and technical reports are available in [`docs/pitch-readiness/`](docs/pitch-readiness/):
1. **[Claims and Evidence Register](docs/pitch-readiness/claims-and-evidence-register.md):** Factual matrix of every pitch claim, supporting code, evidence strength, and open hypotheses.
2. **[Five-Minute Demo Runbook](docs/pitch-readiness/five-minute-demo-runbook.md):** Minute-by-minute live presentation script with actions, screenshots, and contingency fallbacks.
3. **[Manual vs. Klarrow Benchmark Protocol](docs/pitch-readiness/manual-vs-recallgraph-benchmark-protocol.md):** Scientific study design measuring time-to-first-trace ($T_{first}$), scope recall ($R_{scope}$), and provenance grounding.
4. **[Customer Discovery Interview Guide](docs/pitch-readiness/customer-discovery-interview-guide.md):** 19 non-leading questions for VPs of FSQA and plant quality directors based on Mom Test / JTBD principles.
5. **[Pilot Readiness & Risk Register](docs/pitch-readiness/pilot-readiness-and-risk-register.md):** Risk matrix covering legal disclaimers, data integrity, model connectivity, and pilot deployment checklists.
6. **[Implementation Findings](docs/pitch-readiness/implementation-findings.md):** Architectural report documenting baseline fixes across schema version 6 and the relationship engine.
7. **[Implementation and Validation Report](docs/pitch-readiness/implementation-and-validation-report.md):** Current code-change, test and residual-risk report for the Run 4 cycle.
8. **[Run 4 Benchmark Report](docs/benchmark-run3.md):** Current single-scenario observed results; Run 3 is preserved as a historical appendix.
9. **[Competition Verification](docs/pitch-readiness/competition-verification.md):** Official event page evidence, deadline, eligibility and unpublished/blocked details.
10. **[Validation Log](docs/pitch-readiness/validation-log.md):** Commands and observed pass/fail counts for the current tree.
11. **[Model-Value Evaluation](docs/evaluation/model-value-evaluation.md):** Deterministic vs local-Llama results across three authored scenarios (run `eval-dev-20261019-final`), including the provider-hardening disclosure.

---

## 8. Honest Limitations & Boundaries

1. **Not Automated Safety Clearance:** Klarrow reconstructs what ingested records support; it does not determine whether food is safe or replace a certified food-safety sanitarian.
2. **Optical Character Recognition (OCR):** Degraded raster scans and handwritten logs are retained without OCR. Text-searchable PDFs, spreadsheets, and structured text files are fully parsed.
3. **Local LLM Performance:** AI extraction requires a running local Ollama instance with `llama3.2:3b`. When Ollama is unreachable, the system gracefully falls back to deterministic structured parsers. In llama mode a small 3B model can add extra candidate links on fragmented evidence (observed in the C scenario of the evaluation suite); candidates never enter confirmed scope without human review.
4. **Benchmark Scope:** The primary benchmark is one authored seven-document fictional scenario (Run 4), extended by a three-scenario evaluation suite comparing deterministic and llama extraction. Both use authored fictional documents, one small local model and one operator. None of this is an accuracy, time-savings, customer-demand or real-document study.
5. **Early Commercial Validation:** Klarrow is an engineering-verified prototype with an automated acceptance test suite; it has not completed formal multi-site commercial pilot contracts or independent customer validation.
