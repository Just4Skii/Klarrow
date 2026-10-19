# Scenario C - Fragmented evidence (cocoa moisture recall)

A freshly authored synthetic scenario for the evaluation suite (never executed before its
lock-in), built around one physical lot recorded three different ways across the evidence.

> A cocoa supplier (Sunland Cocoa Trading) recalls production batch CB-2210 for moisture.
> Karoo Crunch received it as internal lot **RM-7702** (GRN: 320 kg; the supplier's own note
> says 300 kg). The ledger then issues it under two different spellings - **RM-77O2** (letter
> O) and **RM-07702** (leading zero). Two batches (PB-2026-1205-C, PB-2026-1206-D) produce
> FL-5501 and FL-5502. There is **no dispatch record at all** in this evidence pack.

## Contents

| File | Purpose |
| --- | --- |
| `documents/` | Six authored evidence documents (do not modify; hashes in `manifest.json`). |
| `manifest.json` | Per-file SHA-256 and size, provenance. |
| `ground-truth.json` | Locked machine-readable expectations. |
| `README.md` | This file. |

## What this scenario tests

1. **Conflicting receipt quantities** - GRN says 320 kg, the supplier's delivery note says
   300 kg. Both must be captured verbatim; neither may overwrite the other, and no average or
   silent reconciliation is allowed.
2. **O/0 confusable variant** - `RM-77O2` must be stored verbatim, proposed as a near-match of
   RM-7702, and (per the script) confirmed by the reviewer, after which regeneration maps the
   canonical code.
3. **Pre-registered limitation: leading-zero variant** - `RM-07702` is *not* flagged by the
   deterministic near-match rule (near-match keys only equate O/0, I/1, L/1 - not a prepended
   zero). The suite locks this as an expected blind spot: no match proposal for RM-07702 may
   appear, and its 120 kg chain stays outside the RM-7702 scope in every trace. The evaluation
   report states this openly instead of patching the scenario.
4. **Credit-return false positive** - the ledger's `credit-return` row (40 kg on RM-77O2) must
   be captured as a fact and never become a relationship.
5. **Missing dispatch evidence** - with no dispatch documents, no customer link may appear
   anywhere; finished lots reached by the trace have no customer destinations.
6. **Alias-then-regenerate flow** - the script confirms the RM-7702 / RM-77O2 alias,
   regenerates relationship proposals, confirms the new canonical `RM-7702 -> PB-2026-1205-C`
   link, and re-runs the trace: FL-5501 (1,900 units) enters confirmed scope; FL-5502 does not.

## Scripted human decisions

See `decisionScript` in `ground-truth.json`. Decisions mirror what a careful operator would
do given the evidence: confirm the unambiguous links, confirm the O/0 alias after the physical
count matches, and leave the leading-zero variant untouched (no mechanized decision exists).

## Do not modify

Any change to the documents invalidates the SHA-256 hashes in `manifest.json` and the locked
reference. This scenario had never been executed when its expectations were locked; if a run
exposes an expectation error, the correction is recorded as a new scenario version plus a note
in the evaluation report - never by silently editing this file after the fact.
