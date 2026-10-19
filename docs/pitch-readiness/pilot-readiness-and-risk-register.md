# Klarrow - Pilot Readiness & Risk Register

**Target Pilot Profile:** Mid-market food manufacturers (\$25M-\$250M revenue) managing 10-50 raw ingredient suppliers and conducting quarterly mock recalls.  
**Deployment Model:** Single-tenant on-premise container or isolated cloud instance (SQLite persistence with non-destructive schema migrations).

---

## 1. Pilot Readiness Scorecard

| Area | Status | Current Engineering State | Pilot Pre-requisite |
|------|--------|---------------------------|---------------------|
| **Core Trace Traversal** | **Ready** | Dual-traversal BFS cleanly isolates confirmed scope from candidate exposure. Tested via the current 43-test automated suite and the three-scenario evaluation suite. | Single-tenant prototype; not production-grade. |
| **Evidence Extraction** | **Ready (Pilot)** | Deterministic parsers handle CSV, TSV, TXT, PDF, EML, and LOG. Ollama Llama 3.2:3b local LLM integration operational; grounding is split into value-in-quote, adjacent-line context, identifier-only and unlocated states. | Scanned PDFs require clean text layer (native text or pre-OCR). |
| **Relationship Pipeline** | **Ready** | Automated relationship engine generates candidate proposals across receipt, batching, packaging, and dispatch. Auto-registers entities in catalog. | Operator review required before confirming candidate edges. |
| **Integrity & Auditing** | **Ready** | Rejection invalidation cascade, immutable audit log, CSV formula sanitization, dynamic suspect lot detection. | Prototype-grade; dependency advisories and SSO/RBAC remain open. |
| **Multi-Tenancy** | **Partial** | Workspace and Investigation scoping implemented in database schema. Multi-tenant auth requires enterprise SSO (OIDC/SAML). | Deploy as dedicated single-tenant instance per pilot customer. |
| **Optical Character Recognition (OCR)** | **Deferred** | Images and raster scans are stored safely with `retained_not_ocr_processed` badge. | Integrate Tesseract OCR or cloud vision API before onboarding facilities with handwritten paper logs. |

---

## 2. Risk Register

| Risk ID | Risk Category | Risk Description | Likelihood | Impact | Mitigation Strategy |
|---------|---------------|------------------|------------|--------|---------------------|
| **R-01** | **Regulatory / Legal** | Customer mistakes Klarrow output for an autonomous regulatory food-safety clearance. | Medium | High | Prominent, persistent disclaimers across UI, API, and export package. Enterprise Pilot Agreement strictly defines software as an *"evidence reconciliation workbench / decision-support tool"* that does not certify food safety. |
| **R-02** | **Data Integrity** | OCR or parser extracts an incorrect lot number from a degraded document, leading to an incorrect candidate link. | Medium | Medium | Rule 7 & 8 enforced: Candidate links never auto-confirm. Extracted facts must be grounded against verbatim derived text; review desk highlights exact line coordinate. |
| **R-03** | **Security / Injection** | Malicious spreadsheet formulas in ingested supplier CSVs execute in operator's Excel (`=cmd|...`). | Low | High | `ExportFormatter.CsvCell` sanitizes all formula trigger characters (`=, +, -, @, \t, \r, %`) by prepending a single quote. Verified by automated security test. |
| **R-04** | **Model Availability** | Local Ollama inference service is offline or unreachable on pilot customer server. | Medium | Low | Graceful fallback to deterministic structured parsers. System health dashboard displays truthful provider connectivity status; never fakes live inference. |
| **R-05** | **Supply Chain Graph Loops** | Complex industrial rework loops (batch rework added across multiple production days) cause cyclic traversal. | Low | Medium | Graph traversal uses `visitedNodes` and `edgeIds` sets in BFS, preventing infinite cyclic loops during backward/forward exploration. |
| **R-06** | **Operator Trust & Adoption** | QA operators find multi-step verification tedious compared to quick-and-dirty manual spreadsheets. | Medium | Medium | 1-click batch proposal confirmation (`Confirm All Validated Proposals`). Clear side-by-side evidence preview reduces document hunting time from hours to seconds. |

---

## 3. Pilot Deployment Checklist

```
[X] 1. Automated integration test suite passing (43/43 tests) after the Run 4 and evaluation-suite changes.
[X] 2. Dual confirmed vs. possible scope traversal implemented and verified.
[X] 3. Verbatim source text grounding and no line-1 fallback in extraction.
[X] 4. Multi-investigation gap finding isolation verified.
[X] 5. Formula injection protection on CSV exports verified.
[ ] 6. Customer Master Services Agreement (MSA) with non-decision disclaimer executed.
[ ] 7. Pilot server provisioning (Ubuntu 22.04 LTS or Windows Server 2022 with Docker or .NET 10 runtime).
[ ] 8. Local Ollama LLM service installed and warmed with llama3.2:3b weights (optional; fallback enabled).
[ ] 9. Initial customer document ingestion batch test (historical mock recall drill data).
[ ] 10. Operator training session (30-minute walkthrough using five-minute-demo-runbook).
```
