# Scenario A - Run 4 baseline (Karoo Crunch cinnamon lead alert)

This is the frozen **Run 4** end-to-end benchmark, re-packaged for the evaluation suite.
The seven documents are byte-identical copies (SHA-256 verified) of the authored synthetic
evidence used in the live Run 4 execution on 2026-10-18. The workspace scenario:

> Karoo Crunch Foods receives a lead-spec alert from spice supplier Kandy Highland Spice Mill
> about ground cinnamon batch CS-CIN-0915. 320 kg arrived as internal lot RM-0873. The operator
> must trace which batches, finished lots and customers are affected - and which remain merely
> possible - pending review.

## Contents

| File | Purpose |
| --- | --- |
| `documents/` | The seven evidence documents (do not modify; hashes in `manifest.json`). |
| `manifest.json` | Provenance, Run 4 identifiers, per-file SHA-256 and size. |
| `ground-truth.json` | Locked machine-readable expectations for scoring (see below). |
| `README.md` | This file. |

## What this scenario tests

1. **Structured extraction fidelity** - deterministic parser must capture every code, quantity
   and date in the pipe/CSV documents without alteration.
2. **Relationship generation + deduplication** - two ledger issues, two batch sheets and two
   dispatch notes must generate exactly one candidate link per real-world relationship.
3. **The confusable-code trap** - `RM-O873` (letter O) must be stored verbatim, flagged as a
   near-match of `RM-0873`, and must never be silently canonicalised. Until an operator decides,
   the RM-O873 chain must stay outside the confirmed RM-0873 scope.
4. **One expected false positive** - the production-log output line produces a dispatch link
   with a pseudo-customer (line fragment); Run 4 recorded it and the operator rejected it. The
   suite expects the candidate to appear and to be rejected by the scripted decision, never
   silently suppressed or confirmed.
5. **Honest mass balance** - 320 kg input vs output in units must stay `insufficient` with a
   unit-conversion limitation; no fabricated reconciliation.
6. **AI extraction comparison** - in Llama mode the extraction run additionally produces
   structured descriptive rows; `expectedAiClaims` in `ground-truth.json` lists six such rows
   that the frozen llama3.2:3b run produced and that the review desk can consume. Deterministic
   mode scores `null` for this metric by design.

## The locked ground truth

`ground-truth.json` is the scoring reference. It was pre-registered by hand before the live
run (see the provenance note) and cross-checked against the frozen Run 4 artefacts without
modification afterwards. It contains:

- required facts (tokens + quantities + the verbatim-trap rule),
- the 7 required links with final states,
- the 1 expected noise link (final state `Rejected`),
- forbidden link patterns (context-only document; ledger sample row),
- the expected near-match proposal (`RM-0873` / `RM-O873`, deferred in the script),
- expected findings (ambiguous-identifier, quantity-mismatch, unreviewed-candidates),
- an AI-claim reference list used only in Llama mode,
- a scripted decision sequence mirroring the human review of Run 4,
- expected trace results before and after those decisions.

## Reproduction

Scenario A is executed by `scripts/evaluation/run-scenario.mjs` through the public API only
(upload -> extraction -> relationship generation -> match refresh -> scripted decisions ->
trace -> findings). Each run uses an isolated `Storage__DataRoot` and writes raw results plus
the computed metrics under `results/evaluation/runs/`.

## Do not modify

Any change to the documents invalidates the SHA-256 hashes in `manifest.json`. Any change to
`ground-truth.json` after a run has been recorded defeats the purpose of pre-registration and
must be recorded as a new scenario version instead.
