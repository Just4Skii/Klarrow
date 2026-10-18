# Klarrow
## The evidence-reconciliation workbench for food-traceability investigations

**One-line description.** Klarrow helps an operator turn fragmented supplier, receiving, production and dispatch records into an inspectable traceability investigation - showing what is supported, what may be related, where records disagree and what is missing.

### The problem we are testing
When a supplier flags a raw-material lot, a quality manager needs to determine which batches used it, where the finished product went, and which records do not reconcile. The evidence may be spread across text files, spreadsheets, invoices and dispatch notes with inconsistent identifiers or quantities. The operational pain and willingness to pay still need validation with food-safety and quality practitioners.

### How Klarrow works
1. **Ingest** supported documents and retain originals with hashes and processing status.
2. **Extract** structured claims using deterministic parsers and locally hosted Meta Llama for supported text content.
3. **Ground** claims in source text where possible; flag identifier-only or unverifiable claims rather than inventing a location.
4. **Review** candidate relationships with a human before they enter confirmed trace scope.
5. **Trace and report** confirmed scope, possible exposure, conflicts, gaps, caveats and an audit trail.

### Run 4 - observed results
A single isolated run using **seven authored fictional documents** and local `llama3.2:3b`:

- 57.9 seconds measured wall-clock for AI extraction across 7 documents.
- 50 total facts: 15 AI-extracted candidate facts and 35 parser facts.
- Grounding was split honestly: 4 facts were value-in-quote verified, 3 were adjacent-line context matches, and 8 were identifier-only and left unreviewed.
- 8 relationship proposals were generated; a reviewer confirmed 7 and rejected 1 false-positive dispatch relationship. The RM-O873 / RM-0873 alias was deferred, not merged.
- The final confirmed trace returned finished lot **FL-1021A** and customers **Bosveld** and **Highveld**. The rejected false-positive to **FL-1034C** did not enter the RM-0873 confirmed scope.
- Mass balance stayed **insufficient**: 320 kg received, 180 kg recorded consumed, and 6,600 units downstream cannot be reconciled without an evidenced conversion.
- Final fact review state: 14 confirmed, 36 still unreviewed. The 7 distinct parser facts supporting confirmed relationships were reviewed after the relationship decisions.

**Important limitations of this run:** the seven documents are authored fiction; this is one model, one scenario and one operator. It is not an accuracy, time-savings or customer-demand study. The 8 identifier-only AI facts were not counted as grounded and were not reviewed. The current automated suite passes (40 .NET tests, 25 browser tests), but that is engineering verification, not real-world validation.

### What is demonstrated vs. not yet proven
**Demonstrated in the current prototype:** local Llama extraction on supported text; parser-backed claims; source locators; relationship proposals; human confirmation/rejection; a persisted trace; visible quantity/identifier findings; HTML, JSON, CSV and five-worksheet XLSX exports; candidate vs. confirmed scope.

**Not yet established:** customer validation, willingness to pay, time saved against a manual baseline, performance on real or independently supplied records, statistical extraction accuracy, ERP integrations, OCR for scans/images, or production-grade identity and access management.

### Why this approach
Klarrow is positioned as an evidence-reconciliation layer alongside the records a business already has, rather than as a replacement ERP. That differentiation is a hypothesis to validate against current traceability products and practitioner workflows; no ERP integration or competitive advantage has yet been independently established.

### The ask
We are seeking **food-safety / quality practitioner feedback**, safely authorised or redacted test records, and a supervised mock-recall evaluation comparing the manual workflow with Klarrow. The next milestone is measured validation, not a claim of production readiness.

*Klarrow assists investigation. People remain responsible for food-safety decisions. This prototype is not a compliance, certification or operational food-safety decision tool.*
