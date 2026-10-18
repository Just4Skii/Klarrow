# Klarrow - Claims and Evidence Register

**Version:** 2.0.0
**Date:** 2026-10-18
**Status:** Current-facing register after the Run 4 correction. It replaces the earlier 28-test/8-quote-verified wording.

This register separates what is implemented and observed from what is still unproven. It is written for pitch and submission use. It is not an independent validation report.

## 1. Current one-line description

> Klarrow is a human-in-the-loop evidence-reconciliation workbench for food-traceability investigations. It combines deterministic parsing with local Meta Llama 3.2 3B candidate-fact extraction and keeps source grounding, human decisions, confirmed versus possible scope, and unresolved gaps visible.

Use "open-weight" for Llama, not unrestricted "open source". Klarrow is not a recall-decision engine, certified food-safety system, compliance certification, or ERP replacement.

## 2. Core claims

| # | Claim suitable for the pitch | Evidence in the current tree / Run 4 | Allowed wording | Limit / what not to say |
|---|---|---|---|---|
| 1 | Multi-tier trace traversal | `TraceService.cs`; bidirectional BFS; confirmed-only and full traversal; Run 4 confirmed FL-1021A and both customers | "Traverses supplier, raw lot, batch, finished lot and dispatch records." | Do not claim general supply-chain coverage; one authored scenario only. |
| 2 | Confirmed vs possible scope is separate | Trace result separates `AffectedFinishedLots` / `AffectedCustomers` from `PossibleFinishedLots` / `PossibleCustomers`; Run 4 possible scope empty | "Candidate links never count as confirmed exposure." | Do not say "zero leakage" across all cases; test coverage is finite. |
| 3 | Evidence-to-trace proposals with human decisions | `RelationshipService.cs`; Run 4 generated 8 proposals; 7 confirmed, 1 false positive rejected, alias deferred | "AI and deterministic rules propose; a human confirms or rejects." | Relationship confirmation is not the same as reviewing every underlying fact. |
| 4 | Rejection invalidates dependent confirmed links | `FactService.cs` demotes confirmed links to candidate and marks re-review when supporting facts are edited/rejected; regression tests | "Rejecting or editing a supporting fact requires the dependent link to be re-reviewed." | This is tested behavior, not a guarantee against every stale-state path. |
| 5 | Source grounding is explicit, not assumed | `GroundingSemantics.cs`; Run 4: 4 `quote_verified`, 3 `context_match_value_adjacent`, 8 `identifier_matched_quote_unverified`, 0 unverified-source | "The system distinguishes value-in-quote, adjacent-line context, identifier-only and unlocated claims." | Do not call all 15 AI facts "verified"; 8 were identifier-only and left unreviewed. |
| 6 | No fabricated fallback locator | Extraction assigns no location when quote and value are not found; no line-1 fallback; regression test | "Unlocated claims are flagged rather than given a fake line number." | Do not claim every locator is semantically correct; it is a locator, not truth. |
| 7 | Human review is split into two layers | `RelationshipProposalDto.SupportingFactsReviewed` and `UnreviewedSupportingFactCount`; Run 4: 7 AI + 7 parser facts confirmed, 36 facts still unreviewed | "A relationship decision is separate from a fact-level review decision." | Do not say "all evidence is reviewed." |
| 8 | Mass balance refuses unsupported reconciliation | Run 4 mass balance `insufficient`: 320 kg input vs 6,600 units output; unit-conversion limitation recorded | "When units do not reconcile, Klarrow says so instead of inventing a conversion." | Do not claim mass-balance reconciliation or a percentage. |
| 9 | Audit and export package | HTML/JSON/CSV/XLSX exports; CSV BOM, formula-injection protection and invariant decimals; 32-key JSON; 52-row CSV; five-sheet XLSX workbook with table, wrap and print-layout checks; pilot notice | "Exports preserve confirmed/possible scope, findings, limitations and audit history." | CSV remains a single flat table; the separate XLSX export contains five worksheets. Workbook printing was checked locally, not independently across Excel versions. |
| 10 | Rejected candidate does not leave a phantom active customer | Reject path removes auto-registered entity only when no other non-rejected link references it; regression test; Run 4 `customers_run2=0` | "Rejecting the false-positive dispatch link removed the orphaned auto-registered customer row." | Seeded/manual entities are preserved; this is a targeted lifecycle rule. |
| 11 | Workspace open-gap count matches persisted findings | `WorkspaceService` counts open `GapFindings`; test compares to `/api/gaps` | "The workspace open-gap count uses the same persisted findings as the Findings page." | Do not confuse trace `Gap` links with persisted findings. |
| 12 | Explicit suspect-lot assignment | Both creation forms have an optional suspect-lot selector; unassigned remains valid | "An operator can assign the suspect lot explicitly or leave it unassigned." | Existing legacy cases may still show "Not assigned"; that is valid. |
| 13 | Real local Meta Llama integration | Run 4: local Ollama `llama3.2:3b`, smoke test passed, 57.9 s wall-clock extraction, 15 AI facts; provider/model metadata | "Local Meta Llama 3.2 3B proposes candidate facts; deterministic code and humans decide." | Not a thin wrapper, but the model is used for extraction, not graph traversal. One run; no accuracy study. |
| 14 | Engineering verification | 40/40 .NET tests; separate targeted XLSX test 1/1; frontend lint 0 errors/2 pre-existing warnings; production build; 25/25 Playwright | "The current tree passes its automated checks." | Not production readiness, not real-document validation, not hostile-input/concurrency testing. |

## 3. Run 4 benchmark truth

Single controlled scenario, seven **authored fictional** documents, locked team-prepared ground truth, local `llama3.2:3b`, one operator.

- 50 facts: 15 AI-extracted and 35 deterministic-parser facts.
- AI outcomes: 4 `quote_verified`, 3 `context_match_value_adjacent`, 8 `identifier_matched_quote_unverified`, 0 `needs_review_unverified_source`.
- 7 AI facts reviewed (4 quote-verified + 3 adjacent); 8 identifier-only AI facts left unreviewed.
- Final fact review: 14 Confirmed / 36 Unreviewed, after the 7 distinct parser facts supporting confirmed relationships were reviewed.
- 8 relationship proposals: 7 confirmed, 1 false-positive dispatch relationship rejected; `RM-O873` / `RM-0873` alias deferred; 0 aliases confirmed.
- Final trace: `FL-1021A`, `Bosveld Tuckshop Distributors`, `Highveld Retail Group (Pty) Ltd`; possible scope empty; `FL-1034C` excluded from the `RM-0873` trace.
- 3 open findings: 36 unreviewed facts, ambiguous identifier, quantity mismatch (320 kg received vs 180 kg consumed).
- Mass balance: `insufficient`, 320 kg vs 6,600 units, explicit unit-conversion limitation.
- Measured wall-clock: extraction 57.9 s; final trace 77 ms. These are not a performance benchmark.
- Exports: JSON valid; CSV UTF-8 BOM, dot-decimal quantities, 52 rows; HTML pilot notice; separate XLSX export has five worksheets, tables, frozen headers, wrapped cells and print configuration.

**Do not claim:** accuracy percentage, precision/recall, time saved, real customer data, independent validation, production readiness, compliance, certification, OCR for scans, or that all evidence was reviewed.

## 4. Safe proof points

- The workflow ran end-to-end on one controlled scenario.
- Local Llama inference produced candidate facts; the app did not substitute a mock and call it live.
- The stricter grounding taxonomy now separates value-in-quote from adjacent-line context.
- A human rejected one false-positive relationship before it entered confirmed scope.
- The alias was deferred, not silently merged.
- The trace matched the locked ground truth.
- Mass balance stayed insufficient rather than inventing a conversion.
- The rejected false-positive customer row was removed while the audit record remained.
- The current automated suite is green: 40 .NET tests and 25 browser tests.

## 5. Claims to avoid

- "AI-powered recall platform that tells you which products/customers are affected."
- "100% grounded", "verified accuracy", "zero hallucination", "zero data leakage".
- "Validated on real/FDA data" or "real customer data".
- "53 seconds instead of hours" or any time-saving claim.
- "Production-ready", "enterprise-grade", "compliant", "certified".
- "Reads any document", "reads scans/photos", "OCR available".
- "Agentic", "real-time", "autonomous".
- "All evidence reviewed" (36 facts remained unreviewed at the end of Run 4).
- "Scales to enterprise" or "replaces your ERP".
- "Open-source Llama" without licence qualification; use "Meta Llama 3.2 open-weight model".
