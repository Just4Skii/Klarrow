# Data model

Key entities:
- `Workspace`: scenario and counts.
- `SourceDocument`: file metadata, hash, type, import status.
- `SourceLocation`: page/sheet/row/line/cell reference.
- `ExtractedFact`: raw and normalized values, quantity, date, source references, confidence, review status, reviewer, timestamps, provider metadata.
- `Supplier`, `RawMaterialLot`, `ProductionBatch`, `FinishedLot`, `Customer`, `Location`.
- `TraceLink`: from/to kind and id, link kind, measured quantity/unit/date, evidence text, source references, state (Confirmed/Candidate/Inferred/Gap), detection method.
- `TraceRun`: selected lot, direction, timestamps, result snapshot JSON, gaps, mass balance, status.
- `AuditEvent`: import/edit/confirm/reject/trace/export events.
- `AiExtractionRun`: provider, model, status, errors; no secrets.
- `CapabilityStatusDto`: reports real configured/available state; status is not inferred solely from non-empty configuration.
