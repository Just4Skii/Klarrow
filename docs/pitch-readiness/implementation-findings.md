# Klarrow Implementation Findings & Baseline Verification

**Date:** 2026-10-16  
**Environment:** ASP.NET Core 10, SQLite, React + TypeScript + Vite  
**Baseline Test Suite:** `RecallGraph.Tests` (23 passed, 0 failed prior to stage 1 implementation)

---

## 1. Verified Baseline State

- **Git Status:** Master branch initialized, files tracked in working directory without commits.
- **Persistence Architecture:**
  - Relational SQLite schema managed via `RecallGraphDbContext` and versioned migration steps in `SchemaInitializer.cs` (`PRAGMA user_version`).
  - Schema version upgraded from 5 to 6 to support additive relationship provenance (`Origin`, `SupportingFactIdsJson`, `Reason`, `ConflictNote`, `StableKey`, `ReviewedBy`, `ReviewedAt`, `ReviewReason`, `NeedsReReview`, `Superseded`, `StaleReason`) and extracted fact grounding (`SourceQuote`, `SourceMatchMethod`).
  - No existing data is wiped; test harnesses use isolated temporary data roots (`RecallGraphFactory`).

---

## 2. Code Inspection & Verification of Reported Defects

### Defect 1: Document-to-Trace Relationship Pipeline Absence
- **Observation:** `TraceLink` rows in the default workspace were historically generated solely by `SeedService.cs` (Savanna Bites) or external SQL scripts.
- **Root Cause:** Parsers (`TextDocumentParser`, `PdfDocumentParser`, `ExcelDocumentParser`) produced `ExtractedFact` records, and `LlamaProvider` produced candidate facts, but there was no service to analyze facts/evidence and generate `TraceLink` relationship proposals for human review.
- **Resolution Plan (Stage 1):** Implement `RelationshipGenerator` and `RelationshipService` to deterministically extract entity relationships (Supplier -> RawLot, RawLot -> ProductionBatch, ProductionBatch -> FinishedLot, FinishedLot -> Customer) from parsed facts, persist them as `LinkState.Candidate` trace links with explicit supporting evidence references, and provide operator review endpoints (`/confirm`, `/reject`).

### Defect 2: Exposure Semantics - Candidate Links Traversed as Confirmed
- **Observation:** In `TraceService.cs`, `while (queue.Count > 0)` traversed all edges where `link.State != LinkState.Gap`. Candidate and Inferred edges were collected into `edges`, and all finished lots reached were placed into `affectedFinishedLots`.
- **Impact:** Candidate and unreviewed links were reported as confirmed affected lots and confirmed customer destinations.
- **Resolution Plan (Stage 2):** Explicitly separate traversal into:
  1. **Confirmed Scope:** Paths traversing strictly `LinkState.Confirmed` edges. Reaches `AffectedFinishedLots` and `AffectedCustomers`.
  2. **Possible Exposure:** Paths containing one or more `Candidate` or `Inferred` edges. Reaches `PossibleFinishedLots` and `PossibleCustomers` with `PossiblePaths` explaining unreviewed dependencies.
  3. `LinkState.Rejected` and `LinkState.Gap` edges are non-traversable.

### Defect 3: Cross-Investigation Findings Leaking & Overwriting
- **Observation:** `GapEngine.RefreshAsync` queried `db.GapFindings.Where(x => x.WorkspaceId == workspaceId)` without filtering by `InvestigationId`, and resolved any existing finding not present in the current computed set.
- **Impact:** Refreshing findings for Investigation B would mark open findings in Investigation A as `Resolved`.
- **Resolution Plan (Stage 4):** Filter findings strictly by `InvestigationId` when an investigation context is provided, scope `StableKey` by investigation, and prevent cross-case mutation.

### Defect 4: Hardcoded Demo Suspect Detection
- **Observation:** `TraceService.ListLotsAsync` contained `IsSuspect = r.Code.Contains("0427", StringComparison.Ordinal)`.
- **Impact:** Any real lot not containing "0427" was marked not suspect, while any lot containing "0427" was marked suspect regardless of investigation settings.
- **Resolution Plan (Stage 2):** Resolve suspect status using the investigation's configured `SuspectLotId` and confirmed aliases.

### Defect 5: Stale / Inaccurate Run History Metrics
- **Observation:** `TraceService.ListRunsAsync` hardcoded `GapCount = 0` and `MassBalanceStatus = ""`.
- **Impact:** Historical run lists showed blank and inaccurate summary values.
- **Resolution Plan (Stage 2):** Read metrics directly from stored `ResultJson`, `GapsJson`, or `MassBalanceJson`.

### Defect 6: LLM Source Location Fallback Flaw
- **Observation:** In `ExtractionService.cs`, when a candidate fact had no matching parser fact, it defaulted to `documentLocations.FirstOrDefault()?.Id` and logged `"document-level location only"`, attaching line 1 or page 1 coordinate to facts that had no source match.
- **Impact:** Facts extracted without verifiable text position appeared to cite the first line of the document.
- **Resolution Plan (Stage 5):** Do not fall back to line 1. If text is unverified in server-derived text, set `SourceLocationId = null`, `SourceMatchMethod = "none"`, and `ValidationOutcome = "needs_review_unverified_source"`.

### Defect 7: Export Truthfulness
- **Observation:** `ExportFormatter.cs` hardcoded `"DEMO / NOT FOR OPERATIONAL FOOD-SAFETY DECISIONS"` regardless of whether the workspace was demo or pilot, and did not separate confirmed from possible scope.
- **Resolution Plan (Stage 6):** Use dynamic workspace mode, separate confirmed from possible exposure, and maintain CSV formula sanitization.

---

## 3. Verified Execution Order

1. **Stage 1:** Relationship generation engine & review API (`RelationshipService`, `TraceLinksController` / `RelationshipController`).
2. **Stage 2:** Correct trace semantics (`Confirmed` vs `Possible` scope, mass balance limitations, suspect lot derivation, run history fixes).
3. **Stage 3:** Alias integration into relationship generation & rejected claim invalidation.
4. **Stage 4:** Investigation-scoped `GapEngine` isolation & two-investigation regression test.
5. **Stage 5:** Model source grounding & verification against derived text.
6. **Stage 6:** Truthful export formatting (demo vs pilot, formula injection protection).
7. **Stage 7:** Independent acceptance dataset & end-to-end integration test.
8. **Stage 8:** Customer discovery & pitch readiness documentation under `docs/pitch-readiness/`.
9. **Stage 9:** Security & admin protection audit.
