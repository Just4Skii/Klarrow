# Architecture evolution (evidence reconciliation)

Stack preserved: ASP.NET Core + EF Core/SQLite + React/TypeScript/Vite. SQLite relational tables hold the first evidence graph; no graph database or new service was added.

Implemented in this pass:
- Phase 1 provenance: `SourceDocument` now records `ProcessingStatus`, `ParserName`, `ParserVersion`, `ProcessingError`, `DerivedTextPath`, `IsOriginal`, `SupersededByDocumentId`. Extracted text is written to `data/uploads/derived/<document-id>.txt` separately from the immutable original. Facts record structure fields, validation outcome, extraction method/provider/model/prompt version and review reason.
- Fact history: `FactReviewEvent` stores append-only previous/new values, reviewer, reason and timestamp on confirm/reject/edit.
- Phase 2 entity resolution: `MatchProposal` and `EntityAlias`; near-match identifiers such as `RM-O427` versus `RM-0427` produce heuristic proposals with human confirm/reject/defer. Confirmation records an alias; nothing auto-merges.
- Phase 4 rule-based gaps: `GapEngine` persists `GapFinding` rows with stable keys, checks for missing downstream dispatch, received-vs-consumed quantity mismatch, missing batch input links, ambiguous identifiers, and unreviewed candidates. Reruns update existing rows instead of duplicating them.
- Phase 6 provider readiness: `OllamaHealthService` probes `/api/tags` and reports configured vs reachable vs installed model; `POST /api/extraction/smoke` performs a real request when a model exists.
- Schema versions 1-4 are applied by `SchemaInitializer` using `PRAGMA user_version` and additive SQL; existing databases are upgraded without deleting data.

Schema tables now include: Workspaces, Investigations, SourceDocuments, SourceLocations, ExtractedFacts, FactReviewEvents, Suppliers, RawMaterialLots, ProductionBatches, FinishedLots, Customers, Locations, TraceLinks, TraceRuns, MatchProposals, EntityAliases, GapFindings, AuditEvents, AiExtractionRuns.

Still deferred or blocked:
- Live Llama inference: verified in this workspace. Ollama at `http://127.0.0.1:11434` exposes `llama3.2:3b` and `llama3.1:8b`; the smoke endpoint succeeded with `llama3.2:3b` and a document extraction produced an unreviewed, schema-validated candidate fact with a source locator. Provider status now reports the last successful run and whether live facts exist.
- OCR for scanned images: originals are retained and marked `retained_not_ocr_processed`; no OCR dependency is installed or claimed.
- Full identity/SSO: the pilot guard is an optional single API key, not user identity or role-based authorization.
- PDF report export: printable HTML, JSON and CSV are provided.
- Embedding-based matching: not added; deterministic normalization and heuristic proposals only.
