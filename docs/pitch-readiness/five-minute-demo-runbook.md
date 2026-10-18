> **SUPERSEDED FOR THE CURRENT SUBMISSION (2026-10-18).** Use `docs/RecallGraph_Pitch_Assets_Bundle/RecallGraph_5_Minute_Pitch_Script.md` and the Run 4 artifacts. This older runbook references a different FDA-inspired bundle; those files are not customer data and must not be presented as independently validated or real customer evidence. Do not use the `real_fda_evidence` bundle in the pitch unless its provenance is documented.
# Klarrow - 5-Minute Pitch Demonstration Runbook

**Audience:** Pitch Competition Judges, Food Safety Executives, Seed Investors  
**Objective:** Deliver a crisp, defensible live demonstration showing evidence reconciliation from messy documents to an auditable recall package in under 5 minutes.  
**Environment Requirements:** Local backend running at `http://127.0.0.1:5000` (or `http://localhost:5000`), frontend at `http://127.0.0.1:5173`, Ollama running locally with `llama3.2:3b` (or fallback to structured deterministic parsers).  

---

## Demonstration Timeline Overview

```
[0:00 - 1:00] Ingest & Grounding (Evidence Inbox & Review Desk)
[1:00 - 2:00] Ambiguity Resolution (Near-Matches & Aliases)
[2:00 - 3:00] Pipeline Generation (Evidence-to-Trace Links)
[3:00 - 4:00] Graph Traversal (Confirmed Scope vs. Possible Exposure)
[4:00 - 5:00] Discrepancy Findings & Immutable Export
```

---

## Detailed Step-by-Step Runbook

### Minute 0:00 - 1:00: Problem & Ingestion Grounding

* **Presenter Narrative:**  
  *"When a contamination alert hits, food safety teams don't have clean database records-they receive a flood of PDFs, bill of ladings, spreadsheets, and batch logs from five different suppliers. Klarrow ingests this evidence and grounds every claim back to verbatim text."*
* **Presenter Action:**
  1. Open `http://127.0.0.1:5173/#/evidence`.
  2. Show the ingested evidence bundle (e.g. `FDA Event 93340 - Cinnamon Applesauce Lead Recall`).
  3. Upload `real_fda_evidence/10_openfda_enforcement_f0402_f0403_f0404_official_record.csv`.
  4. Attempt to upload the identical file a second time to show SHA-256 duplicate detection.
  5. Navigate to `#/review`. Select an unreviewed claim and click on it.
* **What Audience Sees:**
  - Clear file type badges (`.csv`, `.pdf`, `.xlsx`, `.png`).
  - Stored vs. Parsed status indicator (e.g., images marked `retained_not_ocr_processed`).
  - SHA-256 duplicate rejection banner preventing duplicate evidence clutter.
  - Review desk displaying verbatim source text highlighting the exact line coordinates.
* **Backend Capability:**
  - `DocumentService.cs` SHA-256 hashing, `Parsers.cs` line extraction, `ExtractionService.cs` quote verification.
* **Expected Result:**
  - Document imports cleanly; duplicate shows warning; source line preview displays highlighted coordinate.
* **Contingency / Failure Mode:**
  - *If upload hangs:* Point to preloaded records in the evidence list; click on existing `.csv` or `.pdf` document to view inline source coordinates.

---

### Minute 1:00 - 2:00: Human-in-the-Loop Ambiguity Resolution

* **Presenter Narrative:**  
  *"In real investigations, typos kill traceability. A barcode scanner reads zero as capital 'O'. Pure AI systems either hallucinate a merge or silently fail. Klarrow surfaces near-matches as explicit proposals-an operator must review and confirm."*
* **Presenter Action:**
  1. Navigate to `#/matches`.
  2. Highlight the proposal: `RM-093340` vs. `RM-O93340` (or `FL-0502328` vs. `FL-O502328`).
  3. Show the heuristic score (`heuristic-not-calibrated`) and state (`Candidate`).
  4. Click **Confirm**, enter reason: `"Verified against packaging photo"`, and submit.
  5. Switch to the **Aliases** tab to show the newly registered alias: `RM-O93340 -> RM-093340`.
* **What Audience Sees:**
  - Levenshtein near-match table clearly labeled as heuristic.
  - Absence of silent automatic merging.
  - Immediate alias registration logged to the audit trail.
* **Backend Capability:**
  - `MatchService.cs`, `IdentifierNormalizer.cs`, `AuditService.cs`.
* **Expected Result:**
  - Proposal moves from Candidate to Confirmed; alias appears in registry.

---

### Minute 2:00 - 3:00: Automated Document-to-Trace Link Generation

* **Presenter Narrative:**  
  *"Previously, bridging documents to a traceability graph required custom SQL scripts. Klarrow features an automated relationship engine that extracts candidate links directly from reviewed evidence."*
* **Presenter Action:**
  1. Navigate to `#/matches` (or Relationship Proposals view).
  2. Click **Generate Proposals from Evidence**.
  3. Point out generated proposals across all tiers: `supplier_delivery` -> `raw_to_batch` -> `batch_to_finished` -> `finished_to_dispatch`.
  4. Emphasize: *"Notice these are generated as Candidate links. They do NOT infect our confirmed exposure until an authorized QA operator reviews them."*
  5. Click **Confirm** on the key candidate links.
* **What Audience Sees:**
  - Table of generated proposals with evidence citations and supporting fact links.
  - Clear badge showing `Candidate` status before operator confirmation.
* **Backend Capability:**
  - `RelationshipService.cs` (`GenerateProposalsAsync`), `RelationshipProposalsController.cs`.
* **Expected Result:**
  - Proposals generated and confirmed with reviewer audit events.

---

### Minute 3:00 - 4:00: Forward & Backward Trace with Scope Integrity

* **Presenter Narrative:**  
  *"Now we execute the trace. Watch how Klarrow separates Confirmed Affected Scope from Possible Exposure Scope."*
* **Presenter Action:**
  1. Navigate to `#/trace`.
  2. Select `RM-093340` (Raw Material Lot) with Direction `Both` and click **Run Trace**.
  3. Switch to **Paths** view, then **Graph** view (interactive SVG).
  4. Click on an edge in the SVG to show the evidence drawer with source document citations.
  5. Highlight the summary panel:
     - **Confirmed Affected Finished Lots:** `FL-0502329`, `FL-0502328`, `FL-0502319`.
     - **Confirmed Customer Destinations:** `Dollar Tree Stores Inc.`, `Weis Markets Inc.`, `Schnuck Markets Inc.`.
     - **Mass Balance Reconciliation:** Flags quantity mismatch (`2,500 kg` received vs `1,850 kg` consumed) with explicit caveats.
* **What Audience Sees:**
  - Clean interactive multi-tier graph.
  - Explicit cards separating confirmed affected finished lots from candidate/possible exposure.
  - Mass balance discrepancy warning.
* **Backend Capability:**
  - `TraceService.cs` dual-traversal BFS (`RunAsync`), `MassBalanceDto`.
* **Expected Result:**
  - Full supply chain graph rendered within 200ms; scope separation clearly displayed.

---

### Minute 4:00 - 5:00: Reconciliation Gaps & Immutable Export

* **Presenter Narrative:**  
  *"Klarrow tells you what records exist, and more importantly, what is MISSING. And when regulators demand proof, we export a defensible evidence package."*
* **Presenter Action:**
  1. Navigate to `#/findings`.
  2. Point out the missing downstream dispatch gap: `UNKNOWN EXPORT CONSIGNEE` for `FL-0502319` (Schnucks).
  3. Navigate to `#/export` and click **Download HTML Report** and **Download CSV Package**.
  4. Open the downloaded HTML report in a new browser tab.
  5. Highlight:
     - Pilot workspace disclaimer watermark.
     - Confirmed vs. Possible scope breakdown.
     - Formula injection sanitization on CSV cells.
     - Complete cryptographic audit trail.
* **What Audience Sees:**
  - Professional, audit-ready HTML report with tables, evidence quotes, and audit log.
  - Prominent disclaimer: *"Investigation aid, not an autonomous food-safety determination."*
* **Backend Capability:**
  - `GapEngine.cs`, `ExportFormatter.cs`, `TraceRunsController.cs`.
* **Expected Result:**
  - Export renders instantly, preserving complete provenance.

---

## Presenter "Cheat Sheet" & Timing Benchmarks

| Milestone | Target Timestamp | Screen to Display | Key Word to Speak |
|-----------|------------------|-------------------|-------------------|
| Problem & Grounding | 0:45 | `#/evidence` | *"Ground truth, not hallucinations"* |
| Typo Resolution | 1:45 | `#/matches` | *"Heuristics propose; humans decide"* |
| Relationship Engine | 2:45 | `#/matches` | *"Evidence-to-trace pipeline"* |
| Trace Traversal | 3:45 | `#/trace` | *"Confirmed vs. Possible scope"* |
| Gaps & Export | 4:45 | `#/export` | *"Audit-ready compliance package"* |
