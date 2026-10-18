# Klarrow - Validation Log

**Date:** 2026-10-18
**Scope:** Run 4 cycle. This is an engineering verification log, not independent validation.

## Environment

- Repository: `D:\code randoms\Recall`
- Run root: `D:\code randoms\Recall-local\run4`
- .NET SDK: 10.0.x
- Frontend: React/TypeScript/Vite
- Local model: Ollama `llama3.2:3b` at `http://127.0.0.1:11434/v1`
- Database: isolated SQLite under `D:\code randoms\Recall-local\run4\data`
- Repository history: no commits; working tree is untracked. No commit or push was performed.

## Commands and results

| Check | Command | Result | Status |
|---|---|---|---|
| .NET baseline | `dotnet test D:\code randoms\Recall\RecallGraph.slnx --nologo --verbosity minimal` | 38 passed / 0 failed | PASS |
| .NET after changes | same | 40 passed / 0 failed, 11 s | PASS |
| Grounding targeted | `dotnet test ... --filter "FullyQualifiedName~Grounding"` | 3 passed / 0 failed | PASS |
| Selected tests | grounding + gap + investigation + dedup filters | 6 passed / 0 failed | PASS |
| Frontend lint | `npm run lint` | 0 errors, 2 pre-existing fast-refresh warnings | PASS |
| Frontend production build | `npm run build` | `tsc -b` and Vite build passed | PASS |
| Playwright full suite | `npx playwright test --reporter=line` | 25 passed / 25 total, 45.4 s | PASS |
| Llama smoke | `POST /api/extraction/smoke` | `success=true`, `llama3.2:3b`, 1 fact | PASS |
| Run 4 extraction | `POST /api/extraction/run` | 7 docs, 15 AI facts, 57.9 s wall-clock | PASS (functional) |
| Run 4 trace | `POST /api/trace-runs` | FL-1021A, Bosveld, Highveld; possible empty; 77 ms | PASS (functional) |
| Export JSON | `GET /api/trace-runs/{id}/export?format=json` | valid JSON, 32 top-level keys, 38 audit rows | PASS |
| Export CSV | `GET ...format=csv` | UTF-8 BOM, 52 rows, `"180.0"`, no `"180,0"` | PASS |
| Export HTML | `GET ...format=html` | pilot workspace notice present | PASS |
| Dependency audit | `dotnet list ... package --vulnerable --include-transitive` | high-severity advisories remain | OPEN |
| Competition page | direct fetch of `https://events.atmeta.com/aiacademysouthafrica` | official page confirms 20 Oct 2026 deadline and eligibility; rubric unpublished | PASS (partial) |
| Pitch deck | PowerPoint COM save/export | 10 slides; 176 animation effects; PDF 10 pages | PASS |
| One-pager | PowerPoint COM export | 1 page; visually checked | PASS |
| Visual QA | all 10 final slide PNGs and one-pager PNG opened | no clipping/overlap after fixes | PASS |

## Run 4 observed state

- 50 facts: 15 AI / 35 parser.
- AI grounding: 4 `quote_verified`, 3 `context_match_value_adjacent`, 8 `identifier_matched_quote_unverified`, 0 `needs_review_unverified_source`.
- Review: 7 AI + 7 parser facts reviewed; final 14 Confirmed / 36 Unreviewed.
- Relationships: 8 proposals; 7 confirmed, 1 rejected; alias deferred; 0 aliases confirmed.
- Final trace: FL-1021A + Bosveld + Highveld; FL-1034C excluded; possible scope empty.
- Mass balance: `insufficient`, 320 kg vs 6,600 units.
- Findings: 3 open (36 unreviewed, ambiguous identifier, quantity mismatch).
- Customer residue check: `customers_run2=0`; regression test covers cleanup and audit retention.
- Exports: JSON/CSV/HTML checks passed.

## Notes / limitations

- The benchmark is one authored seven-document fictional scenario, one model, one operator, n=1. It is not an accuracy, time-savings, customer-demand or real-document study.
- The two Playwright failures in the first full run were root-caused: an upload/refresh error race and an order/state-sensitive test. After the fix, both passed in isolation and the full suite passed.
- The dependency advisories were not patched in this pass. A broad package upgrade immediately before submission was judged higher risk than documenting the residual.
- The binary pitch deck and PDF were not regenerated; editable markdown sources were updated.
