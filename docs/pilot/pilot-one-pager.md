# Klarrow Pilot Programme - One-Pager

*Audience: prospective pilot customer. Status: pilot planning document. Klarrow is an engineering-verified prototype; no completed customer pilots exist as of this document.*

## What Klarrow is

Klarrow is a human-in-the-loop evidence-reconciliation workbench for food-traceability investigations. It combines deterministic parsing with optional candidate-fact extraction by a Meta Llama 3.2 3B open-weight model run locally via Ollama. It keeps source grounding, human review decisions, Confirmed Affected Scope versus Possible Exposure Scope, and unresolved gaps visible in one workspace, and it produces export packages (HTML, JSON, CSV, XLSX) that carry a pilot-workspace notice and spreadsheet formula-injection sanitization.

## The problem it addresses

A contamination alert or a mock recall forces a quality team to reconcile records that were never designed to fit together: receiving notes, batch sheets, stock ledgers, dispatch records, spreadsheets and emails, often with lot codes that differ by a single character and quantities that do not reconcile.

Klarrow is built for that reconciliation moment:

- Ingest the documents a business already has (PDF, CSV, TSV, XLSX, XLS, TXT, LOG, EML; images are stored for manual inspection, not parsed for text).
- Extract candidate facts with verbatim source grounding; claims that cannot be located are flagged rather than given a fake location.
- Propose trace relationships across tiers (supplier, raw lot, batch, finished lot, dispatch) for human confirmation.
- Traverse the graph and strictly separate Confirmed Affected Scope from Possible Exposure Scope. Candidate links never count as confirmed exposure.
- Surface unresolved gaps, quantity mismatches and unreviewed facts as findings.
- Export the result with its audit trail for review.

## What a pilot includes

| Item | Detail |
| --- | --- |
| Deployment | One single-tenant Klarrow instance on the customer's chosen host (see "What the customer needs to provide"). |
| Records | Ingestion of historical mock-recall records that the customer authorises for the pilot. |
| Workspace | A clean pilot workspace with synthetic demonstration seeding disabled (`Seed__DemoEnabled=false`), so only authorised records are present. |
| Training | A guided five-minute walk-through using the repository's current demo script (`docs/RecallGraph_Pitch_Assets_Bundle/RecallGraph_5_Minute_Pitch_Script.md`), then operator exercises from `docs/pilot/pilot-runbook.md`. |
| Support | Setup assistance by the vendor team and a review of the first trace run with the operators. |
| Optional | A scripted evaluation run with the repository's evaluation harness (`scripts/evaluation/run-scenario.mjs`) on an isolated data root, using authored scenarios. This is a mechanics check, not a substitute for operator use. |

The older `docs/pitch-readiness/five-minute-demo-runbook.md` is marked superseded in the repository; use the current demo script above.

## How a typical pilot session runs

1. Create an investigation for the mock-recall scenario and upload the authorised documents against it.
2. Deterministic parsers extract structured facts at upload with source coordinates; the optional local model can add prose-derived candidate facts, always flagged for review.
3. Review claims in the Review Desk: confirm, reject or edit each one. Rejecting or editing a supporting fact demotes dependent confirmed links back to Candidate for re-review.
4. Generate relationship proposals from the evidence and confirm or reject each. Candidate links never enter confirmed scope on their own.
5. Run a trace on the suspect lot and inspect Confirmed Affected Scope and Possible Exposure Scope separately, together with mass-balance status and open gaps.
6. Export the HTML, JSON, CSV or XLSX package, which carries the pilot-workspace notice, the scope split, findings and the audit trail.

## What Klarrow does not do

- It is not a recall-decision engine and not a food-safety clearance system. A human decides everything that enters confirmed scope.
- It does not provide certification, regulatory clearance or legal advice.
- It does not read scanned or photographed documents. Images and degraded scans are stored as evidence for manual inspection only.
- It is single-tenant prototype grade: no SSO, no per-user accounts, no role-based access control, no built-in TLS, and no tamper-evident audit. See `docs/security/known-production-gaps.md`.
- It is not production-hardened, and it makes no accuracy, speed or effort-reduction claims.
- Open dependency advisories are recorded in `docs/pitch-readiness/validation-log.md`; re-audit and patch before any deployment.

## What the customer needs to provide

| Item | Detail |
| --- | --- |
| Pilot server | Ubuntu 22.04 LTS or Windows Server 2022 host with the .NET 10 runtime. The pilot risk register also lists a container option; this repository does not currently ship a Dockerfile, so containerisation would be host-provided. |
| Local model (optional) | Ollama on the same host with the `llama3.2:3b` model available. Without it, Klarrow runs on deterministic parsers only. |
| Records | Historical mock-recall documents authorised for the pilot. No live-incident data is required. |
| People | One to two quality/FSQA operators for training and the review work, plus a contact who can authorise record access. |
| Backups | A folder on the host for data-root backups (database file plus uploads), per `docs/pilot/pilot-runbook.md`. |

## How pilot success is measured

Process-based measures only, agreed before the pilot starts:

- A trace was completed against locked ground truth prepared for the pilot scenario.
- Confirmed and possible scope were reported separately for each trace run.
- Findings (gaps, unreviewed facts, quantity mismatches) were surfaced for operator review.
- Review decisions for facts and relationships were recorded in the audit trail and included in exports.
- Export packages (HTML/JSON/CSV/XLSX) were produced and opened by the pilot team.

No accuracy, speed, labour-saving or financial claims are made; the pilot tooling does not measure them.

## Current stage - stated honestly

Klarrow is an engineering-verified prototype. On the recorded evaluation run `eval-dev-20261019-final`, deterministic mode met all 13 locked checks on all three authored scenarios; llama mode passed two scenarios fully and scored 11/13 on the fragmented-evidence scenario, where extra candidate links stayed in Candidate state and entered no confirmed scope. The current tree passes 43/43 .NET tests. There are no completed customer pilots, and no real-customer or real-document validation has been performed.

## Contact

[founder contact]
