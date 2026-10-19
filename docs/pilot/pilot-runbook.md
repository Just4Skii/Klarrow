# Klarrow Pilot Runbook - Operator and Administrator Guide

Scope: standing up and running a single-tenant Klarrow pilot instance from this repository. Prototype grade: this runbook describes current engineering behaviour, not a hardened deployment. See `docs/security/known-production-gaps.md` before hosting anything beyond a controlled pilot.

## 1. Prerequisites

| Requirement | Notes |
| --- | --- |
| .NET SDK 10 | `dotnet --version` must report 10.x. |
| Node.js and npm | Only needed to build or run the web portal (`src/frontend`). The repository's documented requirement is Node.js 24+. |
| Ollama (optional) | Local model host for `llama3.2:3b`. Without it, deterministic parsing still works and AI extraction is skipped or marked failed. |
| Host | Ubuntu 22.04 LTS or Windows Server 2022 per the pilot risk register. No Dockerfile is shipped in this tree; containerisation would be host-provided. |

## 2. Build and run

```powershell
# from the repository root
dotnet build
dotnet run --project src/RecallGraph.Api --urls http://127.0.0.1:5099
```

Web portal, in a second terminal:

```powershell
cd src/frontend
npm install
npm run dev
```

Open http://localhost:5173. On Windows, `start-klarrow.cmd` (or `powershell -NoProfile -File scripts\start-klarrow.ps1`) starts both services, waits for health, checks whether Ollama is available, and opens the browser. `-DryRun` previews without launching.

## 3. Pilot configuration (verified environment variables)

Set these before starting the API. Defaults are the values compiled into the code, not deployment recommendations.

| Variable | Purpose | Code default | Notes |
| --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | unset (Production) | `Development` enables the Debug logging from `appsettings.Development.json`; the evaluation runner uses it. |
| `ASPNETCORE_URLS` | Listen URLs | unset | Alternative to the `--urls` argument. |
| `Storage__DataRoot` | Root for data | `data` | Relative values are resolved against the API process content root at startup. Use an absolute path in a pilot. |
| `Storage__UploadsPath` | Stored uploads | `<DataRoot>/uploads` | A relative value is placed under the data root at startup. |
| `Storage__SeedEvidencePath` | Synthetic seed fixtures | `seed/evidence` | Only read when demo seeding runs. |
| `Storage__MaxUploadBytes` | Upload size cap | `10485760` (10 MB) | Larger uploads are rejected. |
| `Seed__DemoEnabled` | Synthetic demo workspace | `true` | Set `false` for a pilot: an empty pilot workspace is created instead. |
| `RecallGraph__ApiKey` | Optional single shared API key | unset (no key) | When set, application routes require the `X-Api-Key` header. `/api/health`, `/api/extraction/status` and `/api/capabilities` stay public. |
| `LLAMA_API_BASE_URL` | OpenAI-compatible model endpoint | empty (unconfigured) | For local Ollama use `http://127.0.0.1:11434/v1`. Alias: `LLAMA_ENDPOINT`. |
| `LLAMA_MODEL` | Model id | `meta-llama/Llama-3.1-70B-Instruct` (code fallback) | Always set this explicitly for Ollama, e.g. `llama3.2:3b`; the fallback value is not an Ollama model. |
| `LLAMA_API_KEY` | Bearer token for the endpoint | empty | Optional. Alias: `LLAMA_API_TOKEN`. |
| `LLAMA_TIMEOUT_SECONDS` | Per-request timeout | `30` | Values below 5 or non-numeric fall back to 30. The evaluation suite uses 180. |
| `EXTRACTION_MODE` | `deterministic_only` disables AI candidate extraction | unset | Deterministic parsing, review, trace and export are unchanged. |
| `LLAMA_EXTRACTION_DISABLED` | `true` or `1` also disables AI candidate extraction | unset | Same effect as `EXTRACTION_MODE=deterministic_only`. |

Pilot start example (PowerShell):

```powershell
$env:Seed__DemoEnabled="false"
$env:Storage__DataRoot="C:\Klarrow\data"
$env:RecallGraph__ApiKey="<random token, kept out of source control>"
$env:LLAMA_API_BASE_URL="http://127.0.0.1:11434/v1"
$env:LLAMA_MODEL="llama3.2:3b"
$env:LLAMA_TIMEOUT_SECONDS="180"
dotnet run --project src/RecallGraph.Api --urls http://127.0.0.1:5099
```

## 4. Data locations

| Item | Location |
| --- | --- |
| SQLite database | `<Storage__DataRoot>/recallgraph.db` |
| Uploaded source files | `<Storage__UploadsPath>` (defaults to `<DataRoot>/uploads`); stored under generated names with the original extension |
| Derived extracted text | `<Storage__UploadsPath>/derived/<document-id>.txt` |
| Seed fixtures (demo mode) | `<Storage__SeedEvidencePath>` |

Relative defaults resolve against the API process content root; confirm the resolved path by listing the directories after first start. For a pilot, set an absolute `Storage__DataRoot`.

## 5. Health and status endpoints

| Endpoint | What it reports |
| --- | --- |
| `GET /api/health` | Service status, UTC time, and `accessControl.mode` (`token` or `none`). Public. |
| `GET /api/extraction/status` | Provider mode (`configured`, `unconfigured`, `deterministic_only`), last successful run, and whether live AI facts exist. Public. |
| `GET /api/extraction/health` | Ollama reachability at the configured endpoint and the installed model list. |
| `GET /api/capabilities` | Feature/capability statuses. Public. |
| `GET /api/admin/healthcheck` | Minimal `status: ok` check. |

```powershell
Invoke-RestMethod http://127.0.0.1:5099/api/health
Invoke-RestMethod http://127.0.0.1:5099/api/extraction/health
```

## 6. Deterministic-only operation

```powershell
$env:EXTRACTION_MODE="deterministic_only"
$env:LLAMA_EXTRACTION_DISABLED="true"
```

Deterministic parsing happens at upload time and is unaffected. AI extraction runs are recorded as `completed-deterministic` with zero AI rows. To re-enable local model extraction, clear both variables (`Remove-Item Env:EXTRACTION_MODE`, `Remove-Item Env:LLAMA_EXTRACTION_DISABLED`) and restart.

Scripted evaluation on authored scenarios (isolated data root, no synthetic seeding, scripted review decisions; run from the repository root):

```powershell
node scripts/evaluation/run-scenario.mjs --scenario scenario_a_run4_baseline --mode deterministic_only
node scripts/evaluation/run-scenario.mjs --suite --modes deterministic_only,llama
```

The runner builds a Release API binary and expects the repository's Windows layout.

## 7. Backup and restore

1. Stop the API process (files are copied from a stopped instance so the SQLite file is not torn).
2. Copy the entire data root: `recallgraph.db` plus the uploads folder (including `derived/`).
3. Restore by stopping the API, replacing those files, and starting again.

On every startup the schema initializer runs `EnsureCreated` plus existence-guarded versioned `CREATE`/`ALTER` statements (current target schema version 6). It adds missing tables and columns; it does not drop data. Take a backup before any version change regardless.

## 8. Upgrade steps

1. Back up the data root.
2. Update the source tree, then `dotnet build` from the repository root.
3. Stop the old process and start the new one; migrations run automatically at startup.
4. Run `dotnet test` to confirm the tree's test suite (43/43 recorded at the documented revision). Check the repository's Actions tab for CI status.

## 9. Troubleshooting

| Symptom | What is happening | Action |
| --- | --- | --- |
| Ollama is not running but `LLAMA_API_BASE_URL` is configured | The extraction run is recorded as `failed` with the provider error. Documents were already parsed deterministically at upload; no facts are invented. | Start Ollama, confirm `llama3.2:3b` is available, check `/api/extraction/health`, then rerun extraction. |
| No model endpoint configured at all | Extraction runs are recorded as `completed-fixture` (fixture fallback); deterministic parsing continues normally. | Configure both `LLAMA_API_BASE_URL` and `LLAMA_MODEL` if live extraction is wanted. |
| Extraction run appears "stuck" or failed | Run statuses are `completed`, `completed-with-errors`, `completed-fixture`, `completed-deterministic` or `failed`. A configured-but-unreachable endpoint produces `failed`. | Check `/api/extraction/status` and the API console log; verify the endpoint and model id. |
| Same file uploaded twice | SHA-256 duplicate detection creates no new document; the response identifies the existing document and an audit event `document_duplicate_skipped` is written. | Expected behaviour; use the existing document. |
| Scanned image or photo upload | Stored and flagged as retained without text extraction; no facts are derived from it. | Attach a text-searchable version or transcribe manually. |
| `401 Unauthorized` | `RecallGraph__ApiKey` is set; the request lacks a valid `X-Api-Key` header. | Send the header; public endpoints are `/api/health`, `/api/extraction/status`, `/api/capabilities`. |
| Upload rejected | File exceeds `Storage__MaxUploadBytes` (10 MB default) or the extension is not allowlisted (PDF, CSV, TSV, XLSX, XLS, TXT, LOG, EML, PNG, JPG, JPEG, GIF, BMP). | Convert or split the file, or adjust the configured limit. |
| Port already in use | The listen URL is occupied. | Stop the other process or change `--urls` / `ASPNETCORE_URLS`. |

## 10. Operator checklist

Daily:

- Check `GET /api/health` returns `status: ok`.
- Check `/api/extraction/status` matches the intended mode (local model or deterministic-only) and review the last run result.
- Work the Review Desk: confirm, reject or explicitly leave unreviewed claims; never treat Candidate links as confirmed exposure.
- Review the Findings page for open gaps and quantity mismatches.

Weekly:

- Generate one export package (HTML and XLSX) and open it; confirm the pilot-workspace notice and audit trail are present.
- Spot-check the audit trail for the week's import, review, trace and export events.
- Back up the data root (database plus uploads).
- Re-read `docs/security/known-production-gaps.md` and the dependency advisories in `docs/pitch-readiness/validation-log.md`; apply updates only after re-running the test suite.

Training reference: `docs/RecallGraph_Pitch_Assets_Bundle/RecallGraph_5_Minute_Pitch_Script.md` (the older `docs/pitch-readiness/five-minute-demo-runbook.md` is marked superseded in the repository).
