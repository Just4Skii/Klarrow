# Klarrow - Run 4 Benchmark Slide (markdown)

**Klarrow - Live evidence reconciliation on a toy recall scenario**
Single scenario · local `llama3.2:3b` (real inference, no mocks) · 7 small documents · 1 operator session · 2026-10-18
Full report: `docs/benchmark-run3.md` (Run 4 correction at top, Run 3 preserved in Appendix A) · Machine-readable: `evaluation/run3-results.json`

> Correction: this slide previously showed Run 3. Run 3 headline numbers are superseded. The runs are not directly comparable because the grounding labels, the LLM sample and the review workflow changed.

## Observed (measured from Run 4 artifacts)

- **Extraction:** 50 facts total: 15 AI-extracted plus 35 deterministic-parser facts. AI outcomes: 4 `quote_verified`, 3 `context_match_value_adjacent`, 8 `identifier_matched_quote_unverified`, 0 `needs_review_unverified_source`.
- **Grounding labels changed:** Run 3 counted adjacent-value cases as `quote_verified`; Run 4 labels them `context_match_value_adjacent`. So Run 4's 4 `quote_verified` is not a like-for-like drop from Run 3's 8. Run 4 has 7 quote-or-adjacent grounded AI facts out of 15; the other 8 are explicitly identifier-only.
- **Human fact review:** 7 AI facts reviewed: the 4 `quote_verified` plus 3 adjacent. All 8 identifier-only AI facts were left unreviewed. The 7 distinct parser facts supporting confirmed relationships were then reviewed. Final: **14 Confirmed / 36 Unreviewed** facts.
- **Human relationship gate:** 8 proposals, 7 confirmed, 1 rejected false positive (the doc 05 "Run 2 output" dispatch misparse), 0 unresolved. All 8 proposals report supporting facts reviewed. The RM-O873 / RM-0873 alias was deferred with a recorded reason; confirmed aliases = 0.
- **Trace matches the locked chain:** affected = FL-1021A to Bosveld + Highveld; possible scope empty; FL-1034C excluded from the RM-0873 trace while the alias is deferred; `confirmedScopeEstablished=true`; 3 open findings.
- **Honesty by construction:** mass balance reports `insufficient` for 320 kg input vs 6,600 units output, with an explicit unit-conversion limitation, instead of inventing a ratio.
- **Speed (measured wall-clock):** extraction of 7 documents = **57.9 s**; final trace = **77 ms**. Do not compare directly with Run 3's 53.1 s server run window or 8.4 ms stored timestamp delta; the measurement basis differs.
- **Exports re-checked:** final JSON has 32 top-level keys and 38 audit rows; CSV has UTF-8 BOM, 52 rows, dot-decimal `"180.0"` and no `"180,0"`; HTML carries the pilot notice.

## Not measured - not claimed

- No accuracy %, precision, recall or time-saving claim: single authored scenario, n=1, one model, one operator.
- Not real customer data; not production-ready, not compliant, not certified, **not independently validated**.
- 8 AI identifier-only facts and 36 facts overall remain unreviewed.
- The deferred alias deliberately narrows the RM-0873 trace. The confirmed RM-O873 branch is outside that trace.
- `customers_run2=0` at check time: the Run 3 phantom customer residue was not reproduced. A regression test covers rejection cleanup while preserving audit history.
- Run 4 file hashes were not re-verified; dataset identity rests on the same file names and matching parser counts.
- Extraction sampling variance is real: Run 4 produced 15 AI facts versus Run 3's 13, including 3 AI facts on doc 06 versus 1.
- Test status is separate from the benchmark: full .NET suite 40/40 passed after changes; frontend lint 0 errors / 2 warnings; production build passed; Playwright 25/25 passed. Dependency advisories remain open and documented.
