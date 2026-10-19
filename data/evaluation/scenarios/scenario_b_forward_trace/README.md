# Scenario B - Forward trace (peanut aflatoxin notice, RM-3301)

A freshly authored synthetic scenario for the evaluation suite (never executed before its
lock-in). The workspace scenario:

> A peanut supplier issues an aflatoxin notice for consignment PN-3301. 500 kg arrived as
> internal lot RM-3301 and was drawn into two production batches (300 kg and 150 kg), producing
> FL-4412 and FL-4413. One dispatch note references FL-4412; a second references "FL-44I2".
> A stock-ledger credit-return row records 50 kg of damaged bags returned to the supplier.

## Contents

| File | Purpose |
| --- | --- |
| `documents/` | Seven authored evidence documents (do not modify; hashes in `manifest.json`). |
| `manifest.json` | Per-file SHA-256 and size, provenance. |
| `ground-truth.json` | Locked machine-readable expectations. |
| `README.md` | This file. |

## What this scenario tests

1. **Forward trace semantics** - from the raw lot downstream: batches -> finished lots ->
   customers, before any confirmation and after scripted confirmations.
2. **Credit-return false positive** - the ledger's `credit-return` row (50 kg) must be captured
   as a fact but must never become a consumption or delivery relationship. The remaining
   quantity mismatch (500 received vs 450 consumed) is expected and is the correct signal.
3. **I/1 confusable variant** - the dispatch to "Karoo Corner Stores" references `FL-44I2`
   (capital I), not `FL-4412`. The match service must propose the pair, the script defers it,
   and the 900-unit dispatch must stay a candidate on its own node. From RM-3301, neither
   FL-44I2 nor Karoo Corner Stores may appear in any trace scope (the variant has no incoming
   production link).
4. **Pre-registered asymmetry** - the gap engine's ambiguous-identifier check scans raw lots
   only; the FL-pair ambiguity surfaces as a match proposal, not as a gap finding. This is
   recorded in the ground truth as expected behaviour.
5. **Mass balance honesty** - 500 kg input vs output in units must remain `insufficient` with
   a unit-conversion limitation.

## Scripted human decisions

The decision script in `ground-truth.json` mirrors a realistic review:

- confirm the six evidence-backed links (supplier, two raw-to-batch, two batch-to-finished,
  the canonical dispatch);
- leave the FL-44I2 dispatch candidate unconfirmed;
- defer the FL-4412 / FL-44I2 near-match proposal.

## Do not modify

Any change to the documents invalidates the SHA-256 hashes in `manifest.json` and the locked
reference. This scenario had never been executed when its expectations were locked; if a run
exposes an expectation error, the correction is recorded as a new scenario version plus a note
in the evaluation report - never by silently editing this file after the fact.
