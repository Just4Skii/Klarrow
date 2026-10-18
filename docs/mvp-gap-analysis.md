# MVP conversion gap analysis

Baseline verified on 2026-10-14: `dotnet build` succeeded; `dotnet test` passed 14 tests; frontend lint/build passed; Playwright 2/2 passed. After the changes below: `dotnet test` passes 15 tests and Playwright passes 4/4.

Prioritized backlog and status:

1. Data / schema safety - fixed. `SchemaInitializer` uses `PRAGMA user_version` plus additive `CREATE TABLE IF NOT EXISTS` / `ALTER TABLE` steps so existing SQLite files gain the `Investigations` table, `Workspaces.IsDemo`, and `TraceRuns.InvestigationId` without deleting data.
2. Investigation lifecycle - implemented. `InvestigationsController`, EF-backed `Investigation`, investigation creation/update, audit events, and trace runs linked by `InvestigationId`. The synthetic seed creates one investigation.
3. Synthetic/pilot separation - implemented. `Workspace.IsDemo` is persisted and returned by the API (with workspace mode, synthetic document count, and primary investigation); `Seed:DemoEnabled=false` (env `Seed__DemoEnabled=false`) starts an empty pilot workspace instead of importing Savanna Bites synthetic evidence. Seeded synthetic records stay tagged when mixed into a pilot workspace.
4. Duplicate ingestion - implemented. Upload hashes the bytes first, returns `status=duplicate` with the existing document id, records a `document_duplicate_skipped` audit event, and does not create a second row.
5. Export safety - fixed. `ExportFormatter` prefixes spreadsheet formula triggers, quotes CSV cells, HTML-encodes report fields, includes a disclaimer/metadata row, and every completed export records a `trace_exported` audit event. Unsupported formats return 400 and record nothing.
6. Access control - implemented as opt-in single-org API key middleware (`RecallGraph:ApiKey`, env `RecallGraph__ApiKey`). Public health/capability routes stay open; all other `/api` routes require `X-Api-Key`. The browser keeps the key in session storage and prompts for it on 401. This is a pilot safeguard, not a claim of enterprise auth, SSO, or compliance.
7. Model integration - unchanged and honest. OpenAI-compatible Llama adapter and contract tests exist; no live inference has been executed. No configured endpoint means extraction falls back to the deterministic fixture parser, and fixture-sourced records are labelled as such in the UI, API payloads, and exports.
8. Optional vision/security models - unchanged and unavailable; not required for this MVP.
9. Remaining: authentication is an API-key guard rather than integrated identity; no live Llama verification; no OCR for scanned images; no PDF report generation (printable HTML is provided). Real pilot data still requires a privacy/security review before import.

## Test coverage now present

- API/domain: health, seed load, forward/backward trace, ground-truth affected lots, near-match warning, missing-dispatch gap, mass-balance insufficiency, fact confirmation + audit, trace snapshot stability, investigation CRUD + linked run, duplicate upload, export safety + audit, API key protection, pilot workspace isolation.
- Provider contract tests: success, malformed JSON, HTTP error, missing credentials, auth header, timeout.
- Browser: seeded trace flow, fact review controls, evidence import + rejected file, investigation creation + linked trace.
