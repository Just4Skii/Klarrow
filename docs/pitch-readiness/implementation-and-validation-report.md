# Klarrow - Implementation and Validation Report

**Date:** 2026-10-18
**Scope:** Internal engineering summary for the Run 4 cycle
**Status:** Candid working report. This is **not** independent validation, certification, compliance evidence or production-readiness evidence.

---

## 1. Purpose and evidence boundary

This report summarizes the code changes visible in the current source tree and the test results observed in this cycle. It is paired with `docs/benchmark-run3.md` (Run 4 correction at the top, archived Run 3 in Appendix A) and `evaluation/run3-results.json` (Run 4 payload plus superseded Run 3 summary).

The code-change notes below come from reading the current source. They are not an exhaustive diff review, and they do not imply that every changed line was exercised by the test runs listed here. The Run 4 benchmark is a single-scenario, single-model, one-operator observation. It is not independent validation.

## 2. Code changes observed in this cycle

### 2.1 Grounding taxonomy and source matching

Relevant source: `src/RecallGraph.Infrastructure/GroundingSemantics.cs`, `src/RecallGraph.Infrastructure/ExtractionService.cs`.

- `GroundingSemantics` now defines four explicit outcome constants:
  - `quote_verified`
  - `context_match_value_adjacent`
  - `identifier_matched_quote_unverified`
  - `needs_review_unverified_source`
- The matching helpers normalize whitespace, use token boundaries for identifier-like values, and avoid prefix matches such as `RM-0873` matching inside `RM-08730` or `RM-O873`. Numeric values cannot match the prefix of a decimal.
- Extraction now uses a narrower rule for `quote_verified`: the quote must resolve to a real line and the raw value must appear inside that quoted span.
- If the quote resolves but the value is on an adjacent line, extraction stores the broader two-line span and labels the fact `context_match_value_adjacent` instead of `quote_verified`.
- If the quote is not verbatim but an identifier matches a line, or the identifier matches a deterministic fact, the fact is explicitly `identifier_matched_quote_unverified`.
- If neither quote nor value is located, the fact is `needs_review_unverified_source` and no invented line-1 locator is assigned.

This is the main reason Run 4's `quote_verified` count is not comparable with Run 3's. Run 3's label covered adjacent-value cases; Run 4 separates them.

### 2.2 Supporting-fact review visibility

Relevant source: `src/RecallGraph.Application/Contracts.cs`, `src/RecallGraph.Infrastructure/RelationshipService.cs`.

- Relationship proposal DTOs now expose:
  - `SupportingFactsReviewed`
  - `UnreviewedSupportingFactCount`
- The mapping in `RelationshipService` calculates both from the supporting fact IDs and current review statuses.
- `SupportingFactsReviewed` is true only when every supporting fact has a human review decision.
- Relationship confirmation remains separate from fact review. The confirmation audit text explicitly says that confirming a relationship does not imply review of every supporting fact.
- In Run 4 the operator reviewed the 7 distinct parser facts supporting confirmed relationships after the relationship decisions. All 8 proposals then reported `supportingFactsReviewed=true` and `unreviewedSupportingFactCount=0`.

### 2.3 Proposal count semantics

Relevant source: `src/RecallGraph.Application/Contracts.cs`, `src/RecallGraph.Infrastructure/RelationshipService.cs`.

- The generation response now exposes:
  - `ExistingCount` as a legacy compatibility field, documented as fact-to-candidate-key hits rather than distinct links.
  - `ExistingLinkCount` as the distinct existing link count.
  - `ExistingFactKeyHitCount` as the explicit name for the legacy value.
- Run 4 generation #1 returned `generatedCount=8`, `existingCount=9`, `existingLinkCount=4`, `existingFactKeyHitCount=9`, `skippedUngroundedCount=0`.
- The UI toast now says "existing link(s)" and separately reports fact-to-link matches.
- This addresses the Run 3 reporting ambiguity without silently changing the legacy field's value.

### 2.4 Rejected candidate entity lifecycle

Relevant source: `src/RecallGraph.Infrastructure/RelationshipService.cs`.

- Rejecting a relationship now removes an auto-registered entity only when no other non-rejected trace link in the workspace references it.
- Legitimate seeded, manually entered or still-referenced entities are not removed. Audit rows are preserved.
- Run 4 observed `customers_run2=0` after rejecting the false-positive dispatch phrase; the new regression test asserts both the cleanup and the audit retention.

### 2.5 Workspace gap count

Relevant source: `src/RecallGraph.Infrastructure/WorkspaceService.cs`.

- `WorkspaceDto.OpenGapCount` now counts persisted `GapFindings` with status `Open`, matching the Findings page.
- It no longer counts `TraceLink` rows with state `Gap`, which are a separate store.
- A regression assertion compares the workspace counter to the `/api/gaps` open-finding count.

### 2.6 Explicit suspect lot assignment

Relevant source: `src/frontend/src/App.tsx`.

- Both investigation-creation forms now include an explicit optional suspect-lot selector.
- "Unassigned / decide later" remains a valid choice.
- Creation no longer depends on whatever lot happened to be selected in the Trace tab.

### 2.7 Upload error race

Relevant source: `src/frontend/src/App.tsx`.

- A successful upload starts an asynchronous refresh. The old refresh unconditionally cleared the global error banner, so a later failed upload could have its "Unsupported file type" error erased by the earlier refresh.
- The refresh no longer clears errors; the explicit Retry action does.
- The previously failing browser test now passes, and the full browser suite is green.

### 2.8 Dependency audit

- `dotnet list RecallGraph.slnx package --vulnerable --include-transitive` still reports high-severity advisories in transitive `Microsoft.OpenApi` 2.0.0, `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, `System.Security.Cryptography.Pkcs` 9.0.0 and `System.Security.Cryptography.Xml` 9.0.0.
- No dependency upgrade was applied in this pass. A broad framework/package jump immediately before submission is higher risk than the current documented residual. This is a pilot-hardening item, not a claim that the advisories are fixed.

## 3. Test and build status

| Check | Result | Status |
|---|---|---|
| .NET baseline before changes | 38 passed / 0 failed | Passed |
| Grounding-targeted tests after changes | 3 passed | Passed |
| Selected tests after changes | 6 passed | Passed |
| Full .NET suite after changes | 40 passed / 0 failed | Passed |
| Frontend lint | 0 errors / 2 pre-existing warnings | Passed |
| Frontend production build | Passed | Passed |
| Playwright end-to-end, full suite | 25 passed / 25 total | Passed |
| Export checks | JSON parsed (32 top-level keys); CSV UTF-8 BOM, 52 rows, dot-decimal `180.0`, no `180,0`; HTML pilot notice present | Passed |
| Dependency audit | High-severity transitive advisories remain | Open, documented |

The full .NET suite is green after the changes. The two earlier Playwright failures were root-caused: one was the upload/refresh error race, the other was order/state-sensitive. Both pass in isolation and the full 25-test suite passed after the fix. No claim is made that these tests cover real-world documents, hostile inputs, concurrency or production operation.

## 4. Run 4 evidence summary

Run 4 is documented in detail in `docs/benchmark-run3.md`. The key observed values are:

- 50 facts: 15 AI-extracted and 35 deterministic-parser.
- AI outcomes: 4 `quote_verified`, 3 `context_match_value_adjacent`, 8 `identifier_matched_quote_unverified`, 0 `needs_review_unverified_source`.
- 7 AI facts reviewed (4 quote-verified plus 3 adjacent); 8 identifier-only AI facts left unreviewed.
- Final fact review: 14 Confirmed / 36 Unreviewed, after 7 distinct parser facts supporting confirmed relationships were reviewed.
- 8 relationship proposals: 7 confirmed, 1 rejected false positive, 0 unresolved.
- Alias proposal `RM-O873` / `RM-0873` deferred; confirmed aliases = 0.
- Final trace: affected `FL-1021A`, `Bosveld Tuckshop Distributors`, `Highveld Retail Group (Pty) Ltd`; possible scope empty; `FL-1034C` excluded from the `RM-0873` trace while the alias is deferred; `confirmedScopeEstablished=true`.
- 3 open findings: 36 unreviewed facts, ambiguous identifier, quantity mismatch (320 kg received vs 180 kg consumed).
- Mass balance: `insufficient`, 320 kg input vs 6,600 units output, explicit unit-conversion limitation.
- Measured wall-clock: extraction 57.9 s; final trace 77 ms. These differ in measurement basis from Run 3's stored timestamps and must not be compared directly.
- Exports: JSON valid with 32 top-level keys and 38 audit rows; CSV has UTF-8 BOM, 52 rows, dot-decimal `"180.0"` and no `"180,0"`; HTML carries the pilot notice.

## 5. Competition verification

The official Meta event page (https://events.atmeta.com/aiacademysouthafrica) was verified directly on 2026-10-18. It confirms the AI Academy South Africa Pitchathon, the **20 October 2026** application deadline, South African startup/team eligibility, a working prototype/MVP requirement, Meta technology examples including Llama, ten finalists, the US$5,000 top-two prizes, and an in-person Johannesburg pitch. It does **not** publish a judging rubric, pitch duration, deck/video limits or the application questions. The application form requires Google sign-in and could not be inspected. The 30 October pitch date is from a secondary LinkedIn post, not the official page text. See `docs/pitch-readiness/competition-verification.md`.

## 6. What the evidence does not show

- It does not show extraction accuracy, precision, recall or time savings. No labeled corpus or accuracy percentage exists for this scenario.
- It does not show performance on real customer data. The 7 documents are authored fiction.
- It does not show that the pipeline is production-ready, compliant, certified or safe for regulatory clearance.
- It does not show independent validation. The same team built and exercised the system; the operator drove the review.
- It does not show that the 8 identifier-only AI facts are wrong, only that they were not reviewed and were not counted as grounded.
- The Run 3 phantom-customer residue is addressed by a targeted cleanup rule and regression test for auto-registered entities; this is not a general entity-lifecycle proof.
- It does not show that the dispatch heuristic false positive is fixed. The same "Run 2 output" misparse still appeared and was rejected by the human gate.
- It does not show that Run 4 file hashes match Run 3. Hashes were not re-verified in Run 4.

## 7. Open items

1. Review or explicitly reject the 8 identifier-only AI facts and the remaining 36 unreviewed facts.
2. Decide the alias question for `RM-O873` / `RM-0873` against physical stock records. Until then the `FL-1034C` branch stays outside the `RM-0873` trace.
3. Tighten or explicitly accept the deterministic dispatch heuristic that produces the `FL-1034C -> "Run 2 output: PB-2026-1013-B"` false positive. The human gate caught it; the heuristic itself is unchanged.
4. Resolve or explicitly accept the transitive NuGet advisories listed in section 2.8.
5. Re-verify Run 4 input hashes if file-level dataset identity is needed for an external audience.
6. The animated deck and one-pager were regenerated and visually checked in submission/final-2026-10-18/; the deck has 10 slides and 176 animation effects, the one-pager is one page. Re-render only if content changes.
7. The official South African application form questions and judging rubric remain inaccessible/not published; confirm with the organiser.
8. Keep the benchmark language explicit: single scenario, one model, n=1, not independent validation, not a time-savings study.
