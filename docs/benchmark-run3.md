# Klarrow - Run 4 Benchmark Correction and Evidence Report

Status: single-scenario, live local-LLM benchmark. **Not** a representative accuracy study, **not** production readiness, compliance, certification or independent validation evidence. Run 4 is the current observed run. Run 3 is superseded and preserved in Appendix A of this file.

Run 4 date: 2026-10-18 (UTC timestamps; local SAST = UTC+2).
Run root: `D:\code randoms\Recall-local\run4`
Correction issued: 2026-10-18.

---

## Run 4 correction (2026-10-18)

This section replaces the Run 3 headline numbers. The old report is not deleted: the full Run 3 text is retained verbatim in Appendix A.

Three things changed, and all three matter for interpretation:

1. **Grounding labels changed.** Run 4 separates `quote_verified` from `context_match_value_adjacent`. Run 3 used `quote_verified` for a quote that was verbatim at the locator even when the extracted value was on an adjacent line. Run 4 only calls a fact `quote_verified` when the value appears inside the quoted span. This makes the Run 4 `quote_verified` count stricter than the Run 3 count.
2. **This is a new live inference, not a re-scoring of Run 3.** Run 4 produced 15 AI facts versus Run 3's 13 for the same 7-document scenario. The difference is LLM sampling variance, not a controlled comparison.
3. **Fact-level review was actually performed in Run 4.** Run 3 left all 48 facts unreviewed. Run 4 ended at 14 Confirmed / 36 Unreviewed facts after the operator reviewed 7 AI facts and the 7 distinct parser facts supporting confirmed relationships.

| Measure | Run 3 (superseded) | Run 4 (current) | Comparability |
|---|---:|---:|---|
| AI facts | 13 | 15 | Not like-for-like: new LLM sample |
| Parser facts | 35 | 35 | Same parser yield for the same document set |
| Total facts | 48 | 50 | Follows from AI sampling |
| `quote_verified` | 8 | 4 | Label semantics changed; see below |
| `context_match_value_adjacent` | Not a separate label | 3 | New, more honest label |
| `identifier_matched_quote_unverified` | 5 | 8 | Sampling variance plus doc 06 differences |
| `needs_review_unverified_source` | Not reported | 0 | New label; no facts in this bucket |
| Facts reviewed | 0 | 14 Confirmed / 36 Unreviewed | Run 4 includes fact review |
| Relationship proposals | 8 | 8 | Same count |
| Confirmed / rejected relationships | 7 / 1 | 7 / 1 | Same outcome |
| Extraction time | 53.1 s (server run window) | **57.9 s (measured wall-clock)** | Measurement basis differs |
| Trace time | 8.4 ms (stored timestamp delta) | **77 ms (measured wall-clock)** | Measurement basis differs |

**Important:** the fall from 8 `quote_verified` in Run 3 to 4 in Run 4 is not a 50 percent grounding regression. Run 3's 8 included 4 facts whose value was on an adjacent line. Run 4 reports 4 `quote_verified` plus 3 `context_match_value_adjacent`, so 7 of 15 AI facts are quote-grounded or adjacent-context grounded. The remaining 8 are explicitly identifier-only and were left unreviewed. The two runs remain not directly comparable because the labels, the review workflow and the LLM sample all changed.

---

## 1. Run identity and environment

| Item | Value | Source |
|---|---|---|
| Workspace id | `3eea195d-a7df-437d-9642-e9148a554fbe` | `proposals-generation1.json`; `relationship-proposals-final.json` |
| Investigation | "Supplier alert CS-CIN-0915 - cinnamon lead (Run 4)" (`INV-RUN4-8873`), id `872650af-899f-437f-81f7-b74be2ad65b4` | `trace-result-final.json`; `investigation-id.txt` |
| AI extraction run | id `b4b095ba-2dcb-453e-a3fb-045699138f82`, provider `OpenAI-compatible Llama`, model `llama3.2:3b`, status `completed`, `isDemo=false`, 7 documents, `factsCreated=15` | `extraction-run.json` |
| Final trace run | id `b88a5916-59fa-4e03-aa45-880bae68112f`, selected `raw-lot` / `RM-0873` / `both` | `trace-result-final.json` |
| Match proposal | `RM-O873` vs `RM-0873`, score `0.5`, kind `heuristic-not-calibrated`, state `Deferred` | `match-proposals.json`; `match-deferred.json` |
| Run root | `D:\code randoms\Recall-local\run4` | artifact location |

The extraction artifact identifies a live OpenAI-compatible Llama provider and reports `isDemo=false`. No independent provider audit, network capture or model-integrity check was performed in this pass. A live smoke test was executed before extraction and returned `success=true`, provider `OpenAI-compatible Llama`, model `llama3.2:3b`, one fact; the artifact is in `D:\code randoms\Recall-local\run4\smoke-result.json`. That smoke test is a plumbing check, not an accuracy result.

## 2. Dataset and extraction counts

The same 7 authored fictional scenario documents were used. Parser fact counts match Run 3 exactly at 35 total. SHA-256 hashes were not re-verified in Run 4, so the file-level hash claim remains a Run 3 artifact claim, not a Run 4 measurement.

| # | File | Parser facts | AI facts |
|---|---|---:|---:|
| 01 | `01_supplier_recall_alert_2026-10-02.txt` | 12 | 2 |
| 02 | `02_goods_received_note_GRN-77412.txt` | 3 | 1 |
| 03 | `03_cinnamon_stock_ledger_sep_2026.csv` | 5 | 5 |
| 04 | `04_batch_sheet_PB-2026-1012-A.txt` | 5 | 1 |
| 05 | `05_production_log_PB-2026-1013-B.txt` | 4 | 2 |
| 06 | `06_dispatch_note_DSP-5011.txt` | 3 | 3 |
| 07 | `07_dispatch_note_DSP-5012.txt` | 3 | 1 |
| **Total** | | **35** | **15** |

All 50 facts (15 AI + 35 parser) were `Unreviewed` immediately after extraction. The doc 06 increase from 1 AI fact in Run 3 to 3 in Run 4 accounts for the total difference and includes two low-value claims ("to follow" and "two pallets shrink-wrapped and sealed") that landed in the identifier-only bucket. This is sampling variance, not a controlled extraction-quality change.

## 3. Grounding taxonomy and AI-fact audit

Run 4 uses four outcome labels:

| Outcome | Count | Meaning |
|---|---:|---|
| `quote_verified` | 4 | The quote is located in the source and the raw value appears inside that quoted span |
| `context_match_value_adjacent` | 3 | The quote is located, but the raw value is on an adjacent line, not inside the quote |
| `identifier_matched_quote_unverified` | 8 | The identifier/value matched a source line, but the quote is not verbatim or the value is not in the quoted span |
| `needs_review_unverified_source` | 0 | Neither quote nor value was located in derived source text; no locator assigned |

Fact-by-fact AI audit (all 15 AI facts):

| Fact id | Doc | Locator | Outcome | rawValue | Review |
|---|---|---|---|---|---|
| `810a8e17` | 07 | lines 2-3 | `context_match_value_adjacent` | `DSP-5012` | Confirmed |
| `4a3d13a8` | 06 | line 3 | `identifier_matched_quote_unverified` | `to follow` | Unreviewed |
| `b2391d66` | 06 | line 3 | `identifier_matched_quote_unverified` | `two pallets shrink-wrapped and sealed` | Unreviewed |
| `f43cf062` | 06 | line 2 | `identifier_matched_quote_unverified` | `DSP-5011` | Unreviewed |
| `02e35419` | 05 | line 3 | `quote_verified` | `PB-2026-1013-B` | Confirmed |
| `446f7485` | 05 | line 2 | `quote_verified` | `PB-2026-1013-B` | Confirmed |
| `0dbc00bc` | 04 | lines 2-3 | `context_match_value_adjacent` | `PB-2026-1012-A \| 2026-09-25 \| Bakery Line 1 \| Oat & cinnamon bites 120 g` | Confirmed |
| `d6d274b6` | 03 | line 2 | `identifier_matched_quote_unverified` | `RM-0873` | Unreviewed |
| `4a74adc5` | 03 | line 4 | `identifier_matched_quote_unverified` | `PB-2026-1013-B` | Unreviewed |
| `35011e7b` | 03 | line 3 | `identifier_matched_quote_unverified` | `PB-2026-1012-A` | Unreviewed |
| `ad9335e9` | 03 | line 4 | `identifier_matched_quote_unverified` | `RM-O873` | Unreviewed |
| `956dd52d` | 03 | line 2 | `identifier_matched_quote_unverified` | `RM-0873` | Unreviewed |
| `f24a0be5` | 02 | lines 2-3 | `context_match_value_adjacent` | `320 kg` | Confirmed |
| `0ade3e2a` | 01 | line 10 | `quote_verified` | `320 kg` | Confirmed |
| `45cd5d8e` | 01 | line 8 | `quote_verified` | `ground cinnamon, food grade` | Confirmed |

Interpretation:

- The operator reviewed exactly the 7 AI facts in the two grounded buckets: 4 `quote_verified` and 3 `context_match_value_adjacent`.
- All 8 `identifier_matched_quote_unverified` AI facts were left unreviewed. Four of those are from the CSV ledger, two are low-value doc 06 text fragments, and two are doc 06 identifier claims.
- The ledger quantity slip seen in Run 3 recurs: fact `d6d274b6` records `0.0 kg` on the `RM-0873` ledger row. It was not counted as verified.
- `needs_review_unverified_source=0` means no fact landed in the no-locator bucket in this run. It does not mean extraction grounding is universally correct.

**Why this is not a Run 3 regression:** Run 3's `quote_verified` label covered quote-verbatim facts even when the extracted value sat on an adjacent line. Four of Run 3's 8 verified facts had that adjacent-value caveat. Run 4 reports those cases as `context_match_value_adjacent` instead. The like-for-like comparison would be Run 4's 4 plus 3 = 7 quote-or-adjacent facts against Run 3's 8, on a different LLM sample (15 vs 13 AI facts).

## 4. Fact review

| Stage | Confirmed | Unreviewed | Source |
|---|---:|---:|---|
| Immediately after extraction | 0 | 50 | `facts-before-review.json` |
| After 7 AI fact reviews, before supporting parser-fact review | 7 | 43 | `check_db.py` snapshot; supplied known output |
| Final, after supporting parser-fact review | 14 | 36 | `check_db.py` re-run against current DB; `relationship-proposals-final.json` |

The final 14 confirmed facts are the 7 reviewed AI facts plus 7 distinct parser facts. The 8 proposals share those supporting parser facts: the rejected false-positive proposal and the confirmed `PB-2026-1013-B -> FL-1034C` proposal both cite the same parser fact, so 8 proposals use 7 distinct parser facts.

All 8 final proposals report `supportingFactsReviewed=true` and `unreviewedSupportingFactCount=0`. The 36 unreviewed facts are 8 AI identifier-only facts plus 28 parser facts. The `unreviewed-candidates` finding therefore reports 36, not Run 3's 48.

Relationship confirmation does not automatically review supporting facts. In Run 4 the supporting parser facts were reviewed in a separate step after the relationship decisions, and that step is visible in the final proposal payload.

## 5. Relationship proposals and human review

Generation #1 response (`proposals-generation1.json`):

| Field | Value | Meaning |
|---|---:|---|
| `generatedCount` | 8 | 8 new unique candidate links persisted |
| `existingCount` | 9 | Legacy field: fact-to-candidate-key hits, not distinct links |
| `existingLinkCount` | 4 | Distinct existing links known at generation time |
| `existingFactKeyHitCount` | 9 | Explicit replacement name for the legacy `existingCount` value |
| `skippedUngroundedCount` | 0 | No proposals skipped for missing verified source location |

The new payload exposes the count semantics explicitly instead of leaving `existingCount` ambiguous.

Final state (`relationship-proposals-final.json`, `relationship-decisions.json`):

| State | Count |
|---|---:|
| Confirmed | 7 |
| Rejected | 1 |
| Candidate / unresolved | 0 |
| **Total** | **8** |

| State | From | To | Link kind | Quantity |
|---|---|---|---|---:|
| Confirmed | Kandy Highland Spice Mill (Pvt) Ltd | RM-0873 | `supplier_delivery` | 320 kg |
| Confirmed | RM-0873 | PB-2026-1012-A | `raw_to_batch` | 180 kg |
| Confirmed | PB-2026-1012-A | FL-1021A | `batch_to_finished` | 3,600 units |
| Confirmed | FL-1021A | Bosveld Tuckshop Distributors | `finished_to_dispatch` | 1,800 units |
| Confirmed | FL-1021A | Highveld Retail Group (Pty) Ltd | `finished_to_dispatch` | 1,200 units |
| Confirmed | RM-O873 | PB-2026-1013-B | `raw_to_batch` | 140 kg |
| Confirmed | PB-2026-1013-B | FL-1034C | `batch_to_finished` | 2,800 units |
| Rejected | FL-1034C | "Run 2 output: PB-2026-1013-B" | `finished_to_dispatch` | 2,800 units |

The rejected link is the same false positive pattern seen in Run 3: doc 05 line 3 (`Run 2 output: PB-2026-1013-B | FL-1034C | 2,800 units | ... awaiting dispatch allocation`) is parsed by the deterministic dispatch heuristic as a finished-to-customer relationship with customer name "Run 2 output: PB-2026-1013-B". The human gate rejected it.

`check_db.py` reports `customers_run2=0`: the phantom customer row that persisted in Run 3 was not present in this Run 4 database at check time. The rejection path now removes auto-registered entities only when no other non-rejected link references them, and a regression test covers the cleanup plus audit retention. The Run 4 observation is consistent with that change; it is still one scenario, not a general proof.

**Alias:** exactly one match proposal, `RM-O873` vs `RM-0873`, score `0.5`, kind `heuristic-not-calibrated`. It was deferred by `operator` at `2026-10-18T10:44:35.856061Z` with the recorded reason "Pending lot-identity verification against physical stock records before treating as the same lot." Confirmed aliases: 0. The `RM-O873` path and `FL-1034C` therefore stay outside the `RM-0873` trace.

## 6. Final trace result

| Field | Run 4 value |
|---|---|
| Run id | `b88a5916-59fa-4e03-aa45-880bae68112f` |
| Selected | `raw-lot` / `RM-0873` / `both` |
| Confirmed scope established | `true` |
| Nodes / edges | 6 / 5 |
| State counts | `{Confirmed: 5, Candidate: 0, Inferred: 0, Gap: 0, Rejected: 0}` |
| Affected finished lots | `FL-1021A` |
| Affected customers | `Bosveld Tuckshop Distributors`, `Highveld Retail Group (Pty) Ltd` |
| Possible finished lots / customers | `[]` / `[]` |
| Unresolved lots | `[]` |
| Findings | 3 |

`FL-1034C` is excluded from the `RM-0873` trace: it is not in the final trace nodes or edges, and it is not in affected or possible scope. It exists in the confirmed relationship set only through the `RM-O873 -> PB-2026-1013-B -> FL-1034C` branch, which is not reachable from `RM-0873` while the `RM-O873` alias is deferred.

Findings (all Open):

| Kind | Scope | Severity |
|---|---|---|
| `unreviewed-candidates` | investigation | low |
| `ambiguous-identifier` | `RM-0873` / `RM-O873` | medium |
| `quantity-mismatch` | `RM-0873` | high |

Mass balance: status `insufficient`; input `320 kg`; output `6,600 units`; waste not recorded; explicit limitation that input and output units differ without a verified conversion. No ratio or percentage is computed or claimed. The live `GET /api/workspace/home` response reports `openGapCount=3`, matching the three persisted open findings.

Audit-trail caveat: `trace-result-final.json` contains an empty `auditTrail` array, while the exported JSON for the same run contains 38 audit rows. Decision evidence in this report comes from `relationship-decisions.json`, `match-proposals.json`, `match-deferred.json` and the exported artifact, not from the empty array in the capture.

## 7. Timings

| Measurement | Value | Basis |
|---|---:|---|
| AI extraction, all 7 documents | **57.9 s** | Measured wall-clock for the run |
| Final trace execution | **77 ms** | Measured wall-clock |
| Persisted trace timestamp span | 4.6 ms | `startedAt`/`completedAt` in `trace-result-final.json`; server-side span, not used as the headline |

Use the measured wall-clock figures above. They are not directly comparable to Run 3's 53.1 s extraction run window or 8.4 ms stored timestamp delta because the measurement basis differs. No UI round-trip latency was instrumented in Run 4.

## 8. Export artifacts

`check_exports.py` was run against the final export set:

| File | Bytes | Check |
|---|---:|---|
| `exports\trace-b88a5916-59fa-4e03-aa45-880bae68112f.json` | 35,888 | Valid JSON, 32 top-level keys, 38 audit rows |
| `exports\trace-b88a5916-59fa-4e03-aa45-880bae68112f.csv` | 22,918 | UTF-8 BOM present, 52 rows, `"180.0"` present, no `"180,0"` comma-decimal cell |
| `exports\trace-b88a5916-59fa-4e03-aa45-880bae68112f.html` | 17,206 | Pilot workspace notice present |

The export checks confirm the previously fixed number formatting and encoding behavior on this run. They do not add new validation coverage.

## 9. Negative cases and limitations

1. One false-positive relationship was generated and rejected: the `FL-1034C -> "Run 2 output: PB-2026-1013-B"` dispatch candidate. The human gate caught it, but the heuristic still produced it.
2. The alias `RM-O873` / `RM-0873` remains deferred. That deliberately excludes the `RM-O873 -> PB-2026-1013-B -> FL-1034C` branch from the `RM-0873` trace. The exclusion is correct under the recorded decision, but it means the trace is narrower than the full relationship set.
3. 8 AI facts are identifier-only and unreviewed. They include low-value doc 06 fragments. The final 36 unreviewed facts remain open work.
4. The Run 4 extraction added 3 AI facts on doc 06 versus 1 in Run 3, and the total moved from 13 to 15. This is sampling variance; it is not evidence of an extraction improvement or regression.
5. Grounding labels are implementation checks, not semantic truth. `quote_verified` means the value appears inside the quoted span; it does not prove the claim is correct.
6. Single scenario, 7 authored documents, one model (`llama3.2:3b`), one operator session, n=1 evidence run. No accuracy percentage, precision, recall or time-saving claim is made.
7. The documents are fictional and authored for this scenario. They are not real customer data.
8. No independent validation was performed. The operator drove the review through the product; this is not a usability study or an external audit.
9. Run 4 file hashes were not re-verified. Dataset identity rests on the same file names and matching parser counts.
10. `customers_run2=0` is an observation, not a cleanup proof.

## 10. Evidence index

- Run root: `D:\code randoms\Recall-local\run4`
- Extraction: `extraction-run.json`, `facts-before-review.json`, `facts-reviewed.json`
- Relationships: `proposals-generation1.json`, `relationship-decisions.json`, `relationship-proposals-final.json`
- Alias: `match-proposals.json`, `match-deferred.json`
- Trace: `trace-result-final.json`, `exports\trace-b88a5916-59fa-4e03-aa45-880bae68112f.{json,csv,html}`
- DB checks: `check_db.py`, `check_exports.py`
- Historical Run 3: Appendix A below; `evaluation/run3-results.json` now carries Run 4 and a superseded Run 3 summary.

The validation and test status for the code changes in this cycle is recorded separately in `docs/pitch-readiness/implementation-and-validation-report.md`. It is internal engineering evidence, not independent validation.

---

# Appendix A - Historical Run 3 report (2026-10-17, superseded, preserved verbatim)

The following is the full Run 3 report as previously published. It is retained for traceability. Its headline numbers are superseded by Run 4 and are not directly comparable because of the grounding-label change, the new LLM sample and the fact-level review added in Run 4.

# Klarrow - Run 3 Benchmark & Evidence Report

Status: single-scenario live-inference benchmark. **Not** a representative accuracy study, **not** production readiness, compliance, certification or independent validation evidence. All product-scope numbers below were re-derived from run artifacts (database, logs, exports) on 2026-10-17 after the run; nothing is copied from intermediate chat notes without corroboration.

Run date: 2026-10-17 (local SAST = UTC+2; run timestamps stored in UTC).
Run root (isolated data root): `C:\Users\mphoj\AppData\Local\Temp\opencode\recall-live-run3\`
Ground truth (locked before extraction): `C:\Users\mphoj\AppData\Local\Temp\opencode\recall-live-run1\ground-truth.md`

---

## 1. Run identity & environment

| Item | Value | Source |
|---|---|---|
| Workspace | "Pilot workspace", mode `pilot`, `isDemo=false`, seeded demo disabled (`Seed__DemoEnabled=false`) | `GET /api/workspace/home`; run root env script |
| Workspace id | `b1782a58-07c7-4ea2-bf1a-1fcc99c6786e` | `recallgraph.db` |
| Investigation | "Supplier alert CS-CIN-0915 - cinnamon lead" (`INV-8873`), id `c7fe0967-8063-4d5f-8afa-2d87ca643e9a`, created 2026-10-17 16:52:07 UTC | `Investigations` table; screenshot `04-case-created.png` |
| AI extraction run | id `a8b8fba8-428d-4794-90a0-1c6bf5b2b6f5`, provider string `OpenAI-compatible Llama`, model `llama3.2:3b`, prompt/schema v1.0, status `completed`, 7 documents | `AiExtractionRuns` table |
| Live inference | local Ollama at `http://127.0.0.1:11434/v1`; smoke test passed before the run (`GET /api/extraction/smoke` → success, model llama3.2:3b) | API responses; `api.log` |
| Trace runs | `b90ef5e9` (pre-confirmation, operator) and `543a20d6` (post-confirmation, reviewer) | `TraceRuns` table |
| UI drives | real Chrome via CDP `127.0.0.1:9222` against Vite `localhost:5173`; scripts `drive-*.mjs` in `recall-live-run1\` | screenshots 03-17 |
| Tests / lint at report time | `dotnet test --no-build` → 36 passed / 0 failed (11 s); `npm run lint` (oxlint) → 0 errors, 2 pre-existing fast-refresh warnings | this session |

## 2. Scope of what was tested

End-to-end product pipeline, all on the isolated root: 7-document upload (UI) → deterministic parsing → live local-LLM extraction (API) → quote/identifier grounding validation → relationship candidate generation → human review gates in Chrome (confirm/reject links, defer alias) → gap checks → bidirectional trace snapshots → export packages (HTML/JSON/CSV fetched through the exact URLs the UI links to).

Not tested: multiple models, multiple scenarios, adversarial documents, concurrency, auth boundaries, hostile inputs, long-run stability.

## 3. Dataset inventory

All seven documents are **author-authored fictional scenario documents** created for this sprint (fictional companies and codes). They are **not** real customer data, and not "real FDA evidence". Provenance: written by the sprint operator as a plausible recall scenario; uploaded unmodified.

| # | File | Format | Parse path | SHA-256 (full) | Bytes |
|---|---|---|---|---|---|
| 01 | `01_supplier_recall_alert_2026-10-02.txt` | plain text, 18 lines | Text Parser v1.0 → 12 parser facts; AI extraction → 2 facts | `2587d582af17d1304658bc9f81ab70de460adadbd37f09cebc3c3964b3223897` | 921 |
| 02 | `02_goods_received_note_GRN-77412.txt` | pipe-delimited, 3 lines | Text Parser → 3 facts; AI → 1 | `af322b0eda26d47f732b9383e2f58f9f3f8490d82e1df68ecf5fc426e1ecfc62` | 365 |
| 03 | `03_cinnamon_stock_ledger_sep_2026.csv` | CSV, 5 lines | CSV/ledger parser → 5 facts; AI → 5 | `4dded2b4a49b10add012ba2744f1cfeba28f87786959a6b2eba01fd3ceef84d2` | 256 |
| 04 | `04_batch_sheet_PB-2026-1012-A.txt` | pipe-delimited, 5 lines | Text Parser → 5 facts; AI → 1 | `b30df6edf6de4432600f99bc6e4ab2e46007e9ddc709a41395a1d530ea023a20` | 390 |
| 05 | `05_production_log_PB-2026-1013-B.txt` | plain text, 4 lines | Text Parser → 4 facts; AI → 2 | `7e516f6d2cb110a2a36f27fbe49fba438755f7d31b9ecd6cd94d077ea32fa48f` | 332 |
| 06 | `06_dispatch_note_DSP-5011.txt` | pipe-delimited, 3 lines | Text Parser → 3 facts; AI → 1 | `8ccb6669154945993febcc0f829c948a5c539c2b358238bce510df82ffbd72bf` | 223 |
| 07 | `07_dispatch_note_DSP-5012.txt` | pipe-delimited, 3 lines | Text Parser → 3 facts; AI → 1 | `5ebfc798cfb294354bc035695e2adebf11cec6aefc9c3102cffcb4b6454c283b` | 224 |

All `ProcessingStatus=parsed`, parser `Text Parser v1.0` (per document record). Parser fact counts (35 total) were computed by the deterministic parser at upload time, **before** any AI run.

## 4. Raw extraction counts

Denominator: all 48 `ExtractedFacts` in investigation scope (`ExtractedFacts` table, grouped by `ExtractionMethod`, `ValidationOutcome`, `ReviewStatus`).

| Group | Count | Outcome field | Review status |
|---|---|---|---|
| AI facts (method `ai-extraction`) | 13 | - | all `Unreviewed` |
| - of which `quote_verified` | 8 | `quote_verified` | Unreviewed |
| - of which identifier-only | 5 | `identifier_matched_quote_unverified` | Unreviewed |
| Parser facts (method `deterministic-parser`) | 35 | `unvalidated` (validation is an AI-fact concept) | Unreviewed |
| Rejected facts | 0 | - | - |

Notes: factsCreated=13 is also recorded in the extraction audit event (`{"fixtureFallback":false,"factsCreated":13}`); `fixtureFallback=false` confirms no mock provider was used. **No fact-level human review was performed in Run 3** - all 48 facts remain Unreviewed; only relationship links and the match proposal were reviewed. A prior run on a different root produced 15 AI facts for the same document set, consistent with LLM sampling variance (that run predates the locator fix and is not used for evidence).

## 5. Source-grounding audit (13 AI facts)

Method: for each AI fact, the stored `EvidenceText` quote and `SourceLocation` (e.g. "line 8") were compared **manually** against the source document text printed with line numbers (docs reproduced from `demo-docs\`, matching SHA-256 above; table rows printed by `stage3-dump.py`). "Verbatim at locator" means the quote text appears character-identical within the lines spanned by the declared locator.

| Fact id | Doc | Locator | rawValue | Outcome | Quote verbatim at locator? | rawValue present in quote? | Verdict |
|---|---|---|---|---|---|---|---|
| `7ef902ff` | 01 | line 8 | `ground cinnamon, food grade` | quote_verified | Yes | Yes | Pass |
| `88491b14` | 01 | line 10 | `320 kg` | quote_verified | Yes | Yes | Pass |
| `e5cb2eca` | 02 | line 3 | `320 kg` | quote_verified | Yes | No (value on line 2) | Pass for quote; value-linkage caveat |
| `491210a4` | 03 | line 2 | `RM-0873` | identifier-only | No (quoted text "2026-09-18 receipt RCP-8891…" vs actual comma-delimited `2026-09-18,receipt,RCP-8891,…`) | Yes | Correctly **not** verified |
| `54d5eab6` | 03 | line 2 | `RM-0873` | identifier-only | No - quote is the **line 5** sample row, locator resolves to first line containing the identifier | Yes | Correctly **not** verified |
| `c7c6183a` | 03 | line 3 | `PB-2026-1012-A` | identifier-only | No (comma/space normalization) | Yes | Correctly **not** verified |
| `66e053ca` | 03 | line 4 | `RM-O873` | identifier-only | No (comma/space normalization) | Yes | Correctly **not** verified |
| `c7024295` | 03 | line 4 | `PB-2026-1013-B` | identifier-only | No (comma/space normalization) | Yes | Correctly **not** verified |
| `f62f0434` | 04 | line 3 | header line text | quote_verified | Yes | No (rawValue is the line-2 header) | Pass for quote; value-linkage caveat |
| `567777e7` | 05 | line 2 | `PB-2026-1013-B` | quote_verified | Yes | Yes | Pass |
| `27ce0d4f` | 05 | line 3 | `PB-2026-1013-B` | quote_verified | Yes | Yes | Pass |
| `728d8ea0` | 06 | line 3 | `1,800 units` | quote_verified | Yes | No (value on line 2) | Pass for quote; value-linkage caveat |
| `9a80a888` | 07 | line 3 | `DSP-5012` | quote_verified | Yes | No (value on line 2) | Pass for quote; value-linkage caveat |

Balance: 8/8 `quote_verified` facts are verbatim at their declared locator (falsification trigger "locator does not resolve to real line" did **not** fire for any verified claim - the two that historically misfired were fixed pre-run and re-verified live). 0/5 identifier-only facts are quote-verbatim; all five are explicitly flagged, never counted as verified. The known pre-fix caveat ("line 18" substring collision) did not recur: fact `7ef902ff` resolved to line 8 this time.

Caveats that matter for interpretation:
- `quote_verified` verifies the **quote**, not the claim. In 4 of 8 verified facts the extracted value does not appear inside the cited quote (it sits on an adjacent line of the same document). A reviewer must still confirm value↔quote linkage.
- For identifier-only facts, the stored locator is "first line containing the raw identifier", which is not necessarily the line the model's text came from (`54d5eab6` demonstrates this). The outcome field correctly marks these as unverified.
- One fact (`54d5eab6`) captured quantity `0.0 kg` where source says `0.5 kg` - extraction quality observation; it was never counted as verified.

## 6. Relationship proposals & human review counts

| Event | Count | Exact meaning | Source |
|---|---|---|---|
| Generation #1 (API, 16:55:09 UTC) | `generatedCount=8`, `existingCount=9`, `skippedUngroundedCount=0` | 8 new unique candidate links persisted; 9 fact→candidate **hits** whose stable key already existed | `AuditEvents` payload; `TraceLinks` rows |
| Generation #2 (UI button, idempotency check) | `generatedCount=0`, `existingCount=17` | second run created nothing; 17 fact→candidate hits matched existing links. No audit event was written because nothing changed (guard in code) | UI toast captured in `07-generate-idempotent.png`; code `RelationshipService.cs` |
| Unique links created | 8 | all `Candidate`, `origin=generated-from-evidence`, `detectionMethod=deterministic-parser`, investigation-scoped | `TraceLinks` |
| Confirmed by human (Chrome UI, reviewer `reviewer`) | 7 | timestamps 17:02:06-17:02:13 UTC | `TraceLinks.ReviewedAt`; audit events |
| Rejected by human | 1 | `FL-1034C → "Run 2 output: PB-2026-1013-B"`, 17:02:14 UTC | audit `relationship_rejected` |
| Deferred relationship links | 0 | link decisions offer confirm/reject only | code |
| Unresolved candidate links | 0 | all 8 links decided | `TraceLinks.State` |

**`existingCount` code meaning (verified, not changed):** `RelationshipService.GenerateProposalsAsync` increments `existingCount` once per *fact→candidate hit* that resolves to a known stable key - where "known" = rows already in DB **plus** links created earlier in the same run (registered at `RelationshipService.cs:128`). It is **not** a count of unique links. First run: 8 created + 9 hits = 17 total fact-candidate hits; second run: 0 created + 17 hits. The two runs are internally consistent. Reported here as a counting-semantics quirk; no code change made (see §16).

Interesting tri-state detail: generation #2 is genuinely idempotent at the data level (0 new rows, no audit row), which is what makes the idempotency claim checkable.

## 7. Near-match proposal & aliases

- Exactly 1 match proposal: `RM-0873` ↔ `RM-O873`, score `0.5`, kind `heuristic-not-calibrated`.
- Human decision via Chrome: **Deferred** at 17:02:18 UTC with recorded note "Pending lot-identity verification against physical stock records before treating as the same lot." (screenshot `09-alias-deferred.png`, opened and inspected). Deferral is a terminal state for the run (the proposal stays pending/visible) - the variant never counts as canonical.
- Confirmed aliases: 0 (`EntityAliases` empty).

## 8. Final trace vs independent ground truth

Selected lot `RM-0873`, direction both. Ground truth expectations were pre-registered in `ground-truth.md` **before** any extraction.

Pre-confirmation snapshot (`b90ef5e9`, 16:55:21.990→16:55:22.386 UTC, 396 ms): confirmed scope empty; possible finished lot `FL-1021A` only; mass-balance input null. Matches pre-registered expectation (all reachable links candidate-only).

Post-confirmation snapshot (`543a20d6`, 17:02:22.057→17:02:22.065 UTC, 8.4 ms; UI displayed "EXECUTION TIME 8 ms"):

| Field | Run value | Ground truth | Match |
|---|---|---|---|
| Confirmed finished lots | `FL-1021A` | FL-1021A | Yes |
| Confirmed customers | `Bosveld Tuckshop Distributors`, `Highveld Retail Group (Pty) Ltd` | both | Yes |
| Possible finished lots / customers | `[]` / `[]` | empty | Yes |
| Confirmed links | 5 (supplier→RM-0873 320 kg; RM-0873→PB-2026-1012-A 180 kg; PB-2026-1012-A→FL-1021A 3,600 units; FL-1021A→Bosveld 1,800 units; FL-1021A→Highveld 1,200 units), state counts `{Confirmed:5, Candidate:0, Inferred:0, Gap:0, Rejected:0}` | primary chain | Yes |
| FL-1034C reachability | Not in nodes, edges, affected or possible scope; appears **only** in the audit trail as a rejected decision record | must be unreachable | Yes |
| `confirmedScopeEstablished` | `true` (only after 7 explicit human confirmations; no link auto-confirmed) | - | Yes |

Source: exported JSON (`exports\trace-543A20D6-….json`, verified in Chrome via the actual UI download link, HTTP 200) and `TraceRuns.ResultJson`. Screenshots `10-confirmed-trace.png`, `11-trace-result-top.png`, `12-trace-result-bottom.png` (all opened and inspected). Rejected links are excluded from all exposure counts (the rejected FL-1034C link does not appear in scope).

## 9. Gap findings

3 open findings, 3 distinct stable keys (no duplicates - the dedup fix verified live):

| Kind | Scope | Severity | Content |
|---|---|---|---|
| `unreviewed-candidates` | investigation | low | 48 candidate facts still unreviewed |
| `ambiguous-identifier` | RM-0873 | medium | evidence variant `RM-O873` resembles canonical lot but is not equal |
| `quantity-mismatch` | RM-0873 | high | received 320 kg vs consumed 180 kg |

Compared with pre-registered list: exact match (same three kinds/entities). Metric nuance recorded for honesty: `GET /api/workspace/home` returns `openGapCount=0` because that field counts *trace-run gap items* (`TraceRuns.GapsJson=[]`), while the Findings tab and `GET /api/gaps` show the 3 `GapFindings` rows. Both are true; they are different stores.

## 10. Mass balance

From stored snapshot + export: status `insufficient`; input total `320 kg`; output total `6,600 units`; waste `not recorded`; limitation text: "Unit conversion required: input is recorded in 'kg' while output is recorded in 'units'." / "Input unit 'kg' differs from output unit 'units' without verified conversion ratio." No percentage, ratio or reconciliation is computed across the incompatible units, and none is claimed here.

## 11. Timings (measured only)

| Measurement | Value | Source (stored) | Precision note |
|---|---|---|---|
| AI extraction of all 7 documents | **53.1 s** (16:53:45.442 → 16:54:38.552 UTC) | `AiExtractionRuns.StartedAt/CompletedAt` | Server-side run boundaries; excludes upload time |
| Trace run pre-confirmation | **396 ms** | `TraceRuns` timestamps | - |
| Trace run post-confirmation | **8.4 ms** (UI showed 8 ms) | `TraceRuns` timestamps | - |
| Test suite | 36 tests / 11 s | `dotnet test --no-build` (this session) | Binaries built after the locator fix |
| Frontend production build | ~6.3 s | `npm run build` (this session, post-UI-change) | - |
| UI decision round-trips, generation #2 latency | **not measured** | - | No instrumentation; do not infer from screenshot spacing |

All DB timestamps are UTC; local wall-clock in screenshots/file times = UTC+2.

## 12. Export artifact verification

Files produced through the exact endpoint the UI links to (`GET /api/trace-runs/{id}/export?format=…`): `exports\trace-543A20D6-….{json,csv,html}` (files as re-captured during the Klarrow rebrand pass: 32,394 / 19,151 / 15,600 bytes; sizes grow slightly with each export because the payload embeds the audit trail, which gains a `trace_exported` event per export). The UI link itself was exercised in Chrome: fetch from the browser session returned HTTP 200 and the expected payload (`affectedFinishedLots=["FL-1021A"]`, possible scope empty). Exporting also appends `trace_exported` audit rows (present in CSV). Screenshot `16-export-pack.png` and `17-export-selected.png` (both opened and inspected). Note (Klarrow rebrand pass, same day): the browser e2e suite reseeds the demo database as part of its flow, so the stored run now lives in these exported artifacts (plus the run3 backups directory), not in the live DB.

Structure checks performed: JSON - 32 top-level keys, valid, includes disclaimers/warnings (no en/em dashes in the current payload); HTML - complete report with pilot watermark banner (the only em dash appears inside a reproduced historical audit row: the pre-rebrand investigation title; audit records are reproductions and are never altered); CSV - banner line + header row + 44 data rows, every cell quoted, parse-verified with a standard CSV parser (consistent 17-column records, edge quantities float-parsable), formula-injection mitigation documented in the UI copy.

Format issues and their resolution:
1. **Decimal comma in CSV/HTML quantity cells** (`"180,0"`, `"3600,0"`) - observed in the original run3 export. Cause: `ExportFormatter.cs:60` and `:153` called `decimal.ToString()` without a culture. **Fixed in the post-run finalize pass** with `CultureInfo.InvariantCulture`; regression test `Csv_and_html_exports_use_invariant_decimal_separator_regardless_of_server_culture` exercises the formatter under a comma-decimal culture (de-DE).
2. **CSV had no UTF-8 BOM** - **fixed**: `ExportFormatter.BuildCsvBytes` now emits the UTF-8 preamble and the export endpoint uses it; covered by `Csv_export_bytes_include_utf8_bom_preamble`.
3. CSV remains a **single flat table** mixing record types (EDGE rows + CONFIRMED_* scope rows + FINDING rows + AUDIT rows) distinguished by a `RecordType` column. Valid and complete, but not a multi-sheet workbook; if per-sheet workbooks are wanted, that is a new feature.

Re-verification after the fix (2026-10-17, same stored run re-exported through the same endpoint): API byte check - CSV starts `EF BB BF`, `"180.0"` cells present, zero comma-decimal quantity cells, 44 data rows, float-parsable quantities; HTML renders `180.0 kg`; JSON valid (32 top-level keys). Browser check through the actual UI download links (Vite proxy → API): CSV `200` with `Content-Disposition: attachment` and first bytes `efbbbf`; HTML `200`. Test suite after the fix: 38/38 passed. The same three files were re-captured once more during the Klarrow rebrand pass with the branded formatter (`# Klarrow export` banner in CSV, brand colors in HTML).

## 13. Failure / negative cases observed

1. **False-positive relationship** (the interesting one): doc 05 line 3 `Run 2 output: PB-2026-1013-B | FL-1034C | 2,800 units | … awaiting dispatch allocation` was parsed by the deterministic dispatch heuristic (`RelationshipService.cs:274-282`) into a bogus `finished_to_dispatch` candidate with customer `"Run 2 output: PB-2026-1013-B"`. Same line correctly produced the genuine `PB-2026-1013-B → FL-1034C` batch_to_finished link. The FP was caught by the human review gate and rejected (audit `relationship_rejected` 17:02:14 UTC). Known residue: the phantom customer entity row remains in the Customers registry (entity rows are not deleted on link rejection).
2. **Deferred alias**: RM-O873 edges and the near-match alias stay excluded from the RM-0873 trace; the quantity-mismatch finding persists (320 received vs 180 consumed) because the 140 kg issue under RM-O873 is not counted - exactly the pre-registered semantics.
3. **Grounding edge cases** from §5 (5 identifier-only facts; 4 verified facts whose quote lacks the rawValue; 1 quantity slip captured as 0.0 vs 0.5 in source). All were handled by the pipeline's honest labels rather than silently passing.
4. **Process interruption**: mid-session the API and Vite processes were externally terminated (`api.err.log` contains a console `^C`); both were relaunched against the same persistent root with zero data loss and the case state verified unchanged. Worth knowing for demo operations: check `/api/health` + Vite before driving UI.

## 14. Limitations

- Single scenario, 7 authored documents, one model (`llama3.2:3b`), one machine, n=1 run used for evidence (an earlier run on a different root produced 15 facts vs 13 - sampling variance).
- Documents are fictional and authored by the sprint team; nothing here demonstrates performance on real-world data.
- No accuracy percentage is computed or implied; ground truth covers one curated chain (11 expected relations), not a labeled corpus.
- `quote_verified` is a verbatim-match implementation check, not semantic truth (see §5 caveats).
- Human decisions were driven programmatically through the real UI (Chrome/CDP) by the operator (reviewer name `reviewer`); this is a single-operator session, not a usability study.
- Relationship candidates are generated by deterministic regexes over evidence lines; the only live-model component is fact extraction.
- No fact-level review was completed (48 unreviewed), and the `unreviewed-candidates` finding correctly remains open.

## 15. Reproduction steps

1. Build: `dotnet build RecallGraph.sln` (or `dotnet test` which builds). Launch API with an isolated root, e.g. the wrapper `recall-live-run3\start-api.cmd` (sets `Storage__DataRoot`, `Storage__UploadsPath`, `Seed__DemoEnabled=false`, `LLAMA_API_BASE_URL=http://127.0.0.1:11434/v1`, `LLAMA_MODEL=llama3.2:3b`, `LLAMA_TIMEOUT_SECONDS=180`), then `npm run dev` in `src/frontend` (or the wrapper). Verify `GET /api/health` and `http://localhost:5173`.
2. Create the investigation in the UI (name + reference; note: suspect lot is taken from the trace tab's lot selector if one is selected, otherwise it stays "Not assigned" - see §16.2) - scripts `drive-create-fill.mjs` + `drive-create-submit.mjs`.
3. Upload the 7 files from `recall-live-run1\demo-docs\` scoped to the investigation - `drive-upload2.mjs` (idempotent: skips already-present files by name).
4. `POST /api/extraction/smoke` (expect success, model llama3.2:3b), then `POST /api/extraction/run {documentIds, reviewer:"operator"}`.
5. `POST /api/relationship-proposals/generate?investigationId=…` - or use the UI "Generate Proposals" button on the Traceability tab.
6. Review in Chrome: confirm/reject candidate links, defer the near-match - `drive-review.mjs`.
7. `GET /api/gaps?investigationId=…&refresh=true`, `GET /api/match-proposals?refresh=true` as needed.
8. Run trace `POST /api/trace-runs {selectedType:"raw-lot", selectedId:"RM-0873", direction:"both"}` or via UI; export via `GET /api/trace-runs/{id}/export?format=json|csv|html`.
9. Sampling variance is expected: a rerun may extract a different number of facts for the same documents.

Artifact index: run root (DB, logs, `proposals.json`, `trace-pre-confirm.json`, `exports\`); ground truth + docs + screenshots + drive scripts in `recall-live-run1\`; ad-hoc read-only checkers `stage3-dump*.py`, `enc-check.py` in the same temp folder. Screenshots referenced as inspected in this report: `04`, `05`, `06`, `08`, `09`, `10`, `11`, `12`, `14`, `15`, `16`, `17` (all opened); `03`, `07`, `13` exist but were not opened during this stage - no claims are made from them.

## 16. Follow-up issues (no code changed in this stage)

1. **`existingCount` semantics** - either rename (e.g. `existingKeyHitCount`) or count distinct stable keys touched. Touching it changes metric semantics and needs its own tests; do not change quietly. *(Reported, not fixed.)*
2. **Suspect-lot assignment** - corroborated: `Investigations.SuspectLotId=""` for this case ("Suspect Lot: Not assigned" in UI). Cause: creation form (`App.tsx:930-935`) picks the suspect lot from the Trace tab's currently selected lot; none was selected at creation time. A `PATCH /api/investigations/{id}` endpoint exists but the UI has no edit surface. Fix = UX change (explicit selector + edit), out of scope for this stage. *(Reported, not fixed.)*
3. **Export number formatting** - *(Fixed in the finalize pass 2026-10-17: `CultureInfo.InvariantCulture` for quantities in `ExportFormatter` CSV/HTML, regression test under de-DE culture, re-verified through the UI download path.)*
4. **CSV encoding** - *(Fixed in the finalize pass 2026-10-17: UTF-8 BOM via `ExportFormatter.BuildCsvBytes`, byte-verified through the UI link.)*
5. **Rejected-link entity residue** - the phantom customer row `Run 2 output: PB-2026-1013-B` remains in the registry after link rejection; consider marking entities as provisional or cleaning up entities that no surviving link references. *(Reported, not fixed.)*
6. **Dispatch heuristic FP** - `RelationshipService.cs:274` matches "Run 2 output: …" phrasing; tighten the dispatch pattern (e.g. require DN-/dispatch context and exclude `output:` phrasing) with a regression test built from doc 05 line 3. *(Reported, not fixed.)*
