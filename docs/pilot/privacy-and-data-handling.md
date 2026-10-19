# Klarrow Privacy and Data Handling (Pilot)

Plain-language description of how a single-tenant Klarrow pilot instance handles data. This document describes engineering behaviour observed in the current code; it is not legal advice and not a certification of any kind. A pilot host should review it with its own legal and data-protection advisers.

## 1. What Klarrow stores

| Data | Detail |
| --- | --- |
| Uploaded source documents | Original file bytes with generated storage names, plus original file name, MIME type, size, SHA-256 hash, upload time and processing status. |
| Derived extracted text | Text extracted at upload by the deterministic parsers, written to a derived-text file per document. |
| Source locations | Page, sheet, row, cell or line coordinates used to ground extracted facts. |
| Extracted facts | Raw and normalized values, claim subject/object, quantity, date, evidence text, grounding classification (for example value-in-quote or identifier-only), review status, reviewer, review reason, and provider/model metadata for AI-extracted rows. |
| Fact review events | Before/after snapshots for each confirm, reject or edit decision, with reviewer and reason. |
| Trace links and runs | Proposed and confirmed relationships with evidence references and link state (Confirmed/Candidate), plus trace-run snapshots and scope results. |
| Findings | Persisted gap findings, quantity mismatches and other reconciliation findings with status. |
| Proposals and aliases | Near-match proposals and any confirmed entity aliases. |
| Audit events | Append-only workspace log of the actions listed in section 8. |
| Workspace entities | Suppliers, raw-material lots, production batches, finished lots, customers and locations that the relationship engine registers while processing evidence. |

## 2. Where it lives

- Everything is stored on the single host running the instance, under the operator-configured storage root (`Storage__DataRoot`).
- The database is a SQLite file: `<Storage__DataRoot>/recallgraph.db`.
- Uploaded files live under `<Storage__UploadsPath>` (default `<DataRoot>/uploads`); derived text lives in its `derived/` subfolder.
- There is no multi-tenant routing: one deployment serves one workspace. Investigation scoping is an application-level view, not a security boundary.

## Data flow summary

1. An operator uploads documents against an investigation; the original bytes and SHA-256 hash are stored, and deterministic parsers derive text and facts on the same host.
2. If an AI extraction run is executed and an endpoint is configured, extracted text or image content is sent to that endpoint; repository guidance is to keep it local (Ollama) so this stays on the host. Returned candidate facts are stored for review.
3. Operators review facts and relationship proposals in the web interface; every decision is stored with reviewer and reason and written to the audit log.
4. Trace runs compute confirmed and possible scope from the stored links; exports are generated on demand from the stored data.

## 3. Local-model guarantee, stated precisely

- By default no extraction-model endpoint is configured, and no document content is sent anywhere.
- If the endpoint is pointed at Ollama on the same machine (`LLAMA_API_BASE_URL=http://127.0.0.1:11434/v1` with `LLAMA_MODEL=llama3.2:3b`), inference runs on that machine and document content does not leave the host.
- When an AI extraction run executes, Klarrow sends the configured endpoint the first 6,000 characters of each document's extracted text, or, for image files, the file content encoded for the model. It does not send the rest of the data root.
- The endpoint is configurable and may be a remote OpenAI-compatible service. If a remote endpoint is configured instead of local Ollama, that document content is sent to the remote host. Treat that as a data-sharing decision: confirm it with the data owner before enabling it, and prefer a local endpoint for confidential evidence.
- The API's own `/api/extraction/health` check contacts the same configured endpoint (Ollama's tags listing) to report reachability.

## 4. What is not collected

- The codebase contains no telemetry or analytics client (verified by searching the backend and frontend sources for telemetry/analytics libraries at documentation time).
- The frontend talks only to the local API; backend outbound calls go only to the configured extraction endpoint.
- No user accounts, profiles, advertising identifiers or usage tracking are collected.

## 5. Retention and deletion

- Deletion is operator-initiated only; there is no automated retention policy.
- Deleting an investigation removes, for that investigation: its documents and derived text files, extracted facts, review events, source locations, trace links, trace runs, and findings. The delete response reports what was removed.
- Deliberately retained after an investigation delete, and listed by the API as limitations: workspace-level entities (suppliers, lots, batches, finished lots, customers, locations), match proposals, entity aliases, extraction-run records, and audit history. A file with identical content (same SHA-256) uploaded to another investigation is also retained.
- Export packages already downloaded exist outside the application database and cannot be removed by deleting an investigation.
- Review decisions are audited; the audit entry for a deletion records counts of removed items.

## 6. Exports

- Formats: HTML, JSON, CSV and XLSX export of a saved trace run (`GET /api/trace-runs/{id}/export?format=...`).
- Exports are generated on demand and streamed to the downloading operator; Klarrow does not archive a copy of an export on the server.
- CSV export prefixes spreadsheet formula trigger characters (`=`, `+`, `-`, `@`, tab, carriage return, `%`) with a single quote and quotes cells, to reduce formula-injection risk when opened in spreadsheet tools. XLSX export applies the same trigger protection cell-by-cell.
- Exports carry the workspace provenance notice: pilot workspaces show a pilot-workspace notice, and synthetic demonstration records are labelled at record level.
- Each export writes an audit event (`trace_exported`).

## 7. Access control honesty

As documented in `docs/security/known-production-gaps.md`:

- No SSO, no per-user accounts, no role-based access control.
- Optional single shared API key (`RecallGraph__ApiKey`, header `X-Api-Key`); when configured, every application route requires it except `/api/health`, `/api/extraction/status` and `/api/capabilities`. When no key is configured, any caller with network access has full read/write access, including deletion.
- The web interface stores the key in browser session storage only.
- No TLS is built in; any networked pilot needs a TLS reverse proxy, and the API should bind to localhost otherwise.
- The audit log is not tamper-evident: audit rows live in the same SQLite database as the data and could be altered by anyone with filesystem access.

## 8. Audit trail

The workspace audit log records, among others: document imports and duplicate skips; extraction runs; fact confirmations, rejections and edits; relationship generation, confirmation and rejection; match-proposal decisions; trace runs; exports; and investigation creation, updates and deletion. Exports include the most recent 500 audit events.

## 9. Known gaps and boundaries

Open security and data-boundary gaps are listed in `docs/security/known-production-gaps.md`. The most relevant for a pilot: shared-key access only, no built-in transport encryption, no tamper-evident audit, no automated retention, no text recognition for scanned images, and open dependency advisories recorded in `docs/pitch-readiness/validation-log.md`.

## 10. Not legal advice

This document describes engineering behaviour and data flows in the current codebase. It is not legal advice, not a data-protection impact assessment, and not a certification that any deployment meets a regulatory or contractual standard. The pilot host remains responsible for lawful basis, authorisations, retention decisions and any local legal requirements.
