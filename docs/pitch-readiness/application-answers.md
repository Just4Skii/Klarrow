# Klarrow - Application Answer Bank (Meta AI Academy South Africa Pitchathon)

**Status:** Draft answer bank for internal use. Not a submission. The official application form (Google Form linked from the event page) requires sign-in and could not be inspected; the questions were not captured as of 2026-10-18. Per `submission/final-2026-10-18/APPLICATION_INPUTS_TO_CAPTURE.md`: do not guess the questions.

**Date:** 2026-10-19.

**Event reference:** Meta AI Academy South Africa Pitchathon, part of AI Academy South Africa (in partnership with DCDT). Official page: https://events.atmeta.com/aiacademysouthafrica. Applications close 20 October 2026 per the official page; the page does not state a time zone.

**Adaptation rule:** Verify every answer against the exact captured question text and the form's character/word limits before pasting, and replace every `[bracketed]` placeholder with confirmed facts. Use the short version for character-limited fields and the full version only where space allows. Name the model exactly as "Meta Llama 3.2 3B open-weight model, run locally via Ollama" in every answer, and keep the stage statements exactly as written in section 2.5. The founder submits; do not submit on the founder's behalf.

## 1. Placeholders to fill in before pasting

| Placeholder | Fill with | Confirm before pasting |
|---|---|---|
| `[founder name]` | Full name of the founder | Yes |
| `[team members and roles]` | Named team members and their roles; include only people who have agreed to be named | Yes |
| `[contact email]` | Application contact email | Yes |
| `[phone]` | Contact phone number | Yes |
| `[city]` | City in South Africa where the team is based | Yes |
| `[legal entity status]` | Registered company, close corporation, or unregistered founding team; state only what is true | Yes |
| `[registration details if asked]` | Company registration number and related details, only if the form asks | Yes |

The availability placeholder `[yes/no]` is used in section 2.10 and must also be confirmed with the founder before pasting.

## 2. Question-category answer bank

Adapt the wording to the exact captured question text and replace `[bracketed]` placeholders. "Short" is 1-2 sentences for character-limited fields. "Full" is 3-5 sentences for fields with more room.

### 2.1 One-line product description

**Short:** Klarrow is a human-in-the-loop evidence-reconciliation workbench for food-traceability investigations.

**Full:** Klarrow is a human-in-the-loop evidence-reconciliation workbench for food-traceability investigations. It combines deterministic parsing of heterogeneous records with an optional extraction step using the Meta Llama 3.2 3B open-weight model, run locally via Ollama, that proposes candidate facts. Source grounding, human confirm/reject decisions, confirmed versus possible scope and unresolved gaps stay visible, and audit exports preserve all of it. Klarrow is an investigation aid, not a recall-decision engine or a compliance certification.

### 2.2 The problem you are solving

**Short:** Food-traceability investigations, mock recalls and withdrawals require reconciling evidence that is fragmented across ERP/MES records, QA releases, certificates of analysis, packing records, spreadsheets and paper, and the hard part is contradictory or missing evidence, not document storage.

**Full:** During mock recalls, withdrawals and traceability investigations, the evidence needed to reconstruct lot genealogy is typically fragmented across ERP/MES records, QA releases, certificates of analysis, packing records, spreadsheets and paper; that pattern is an assumption we are validating with practitioners, not a measured statistic. The hard part is not storing documents but reconciling contradictory, incomplete or ambiguous evidence and showing what a trace conclusion actually rests on. Investigations also need to keep confirmed exposure separate from possible exposure, so unresolved candidates never silently become "affected" claims. The work is consequential: Consumer Protection Act 68 of 2008 s60 allows the National Consumer Commission to require a producer to conduct an investigation or carry out a recall programme. We do not claim that every producer has this pain or lacks a tool; that is exactly what we are validating.

### 2.3 Your solution and how it works

**Short:** Klarrow ingests raw records, extracts candidate facts with verbatim source grounding, proposes candidate trace links across supplier, raw lot, batch, finished lot and dispatch records, and keeps confirmed scope strictly separate from possible exposure with a human deciding every confirmation.

**Full:** Klarrow ingests heterogeneous records - text-searchable PDFs, CSV/TSV, XLSX/XLS, TXT, LOG and EML files up to 10 MB - rejects duplicate payloads by SHA-256 hash, and stores image files with a visible note that they are retained without OCR processing. Deterministic parsers extract facts, and an optional extraction step using the Meta Llama 3.2 3B open-weight model, run locally via Ollama, proposes additional candidate facts, each classified by grounding: value-in-quote, adjacent-line context, identifier-only or unlocated. A human review desk confirms, rejects or edits facts; rejecting or editing a supporting fact demotes dependent confirmed links back to re-review, and unlocated claims are flagged rather than given a fake line number. The relationship engine proposes candidate links across supplier, raw lot, batch, finished lot and dispatch tiers, and bidirectional graph traversal separates Confirmed Affected Scope from Possible Exposure Scope: candidate links never count as confirmed exposure, and when quantities do not reconcile, mass balance reports "insufficient" instead of inventing a conversion. Findings (unreviewed facts, ambiguous identifiers, quantity mismatches) and exports (JSON, CSV, HTML and a five-sheet XLSX workbook) preserve confirmed/possible scope, limitations and audit history, with CSV formula-trigger sanitization on export.

### 2.4 How you use Meta AI technology

**Short:** We use the Meta Llama 3.2 3B open-weight model, run locally via Ollama, as an optional extraction step: it proposes candidate facts with source grounding, deterministic code and human reviewers decide what is confirmed, and Klarrow falls back to deterministic parsing when the model service is unavailable.

**Full:** The Meta AI technology in Klarrow is the Meta Llama 3.2 3B open-weight model, run locally via Ollama; it is used for candidate-fact extraction, not graph traversal or decisions. The model proposes candidate facts with verbatim source grounding and a review state; deterministic rules and a human reviewer confirm, reject or edit, and no AI-extracted content enters confirmed state without human review. When the model service is unavailable, Klarrow falls back to deterministic parsing, which is also the standalone baseline: deterministic mode passed all 13 checks on all three authored scenarios; llama mode passed two scenarios (13/13) and scored 11/13 on the fragmented-evidence scenario where extra candidate links stayed in Candidate state and none were auto-confirmed; 43/43 .NET tests. This is a working local integration, not a substituted mock: Run 4 used live local Llama inference and produced 15 AI candidate facts from seven authored fictional documents. It is one small local model in one supervised pipeline, and we have not run an accuracy study; scenario C of the evaluation suite shows where it adds candidate noise on fragmented evidence, and we say so.

### 2.5 Stage / traction

**Short:** Engineering-verified prototype: the current tree passes 43/43 .NET tests; deterministic mode passed all 13 checks on all three authored scenarios; llama mode passed two scenarios (13/13) and scored 11/13 on the fragmented-evidence scenario where extra candidate links stayed in Candidate state and none were auto-confirmed. There are no customers, no completed pilots and no revenue.

**Full:** Klarrow is an engineering-verified prototype built and tested in South Africa; the current tree passes 43/43 .NET tests, plus a recorded 25/25 browser suite, and the evaluation run is `eval-dev-20261019-final`. The primary benchmark is one authored fictional seven-document scenario (Run 4) with locked ground truth, extended by a three-scenario evaluation suite: deterministic mode passed all 13 checks on all three authored scenarios; llama mode passed two scenarios (13/13) and scored 11/13 on the fragmented-evidence scenario where extra candidate links stayed in Candidate state and none were auto-confirmed. In Run 4 a human rejected one false-positive relationship before it entered confirmed scope, an ambiguous identifier was deferred rather than merged, and mass balance stayed "insufficient" rather than inventing a conversion. We say this plainly: there is no real customer data, no completed pilots, no customers, no revenue and no measured accuracy, and no independent validation has been performed. The prototype is not production-ready, and the evaluation suite is internal engineering verification with authored fictional documents, not a customer deployment.

### 2.6 What is novel

**Short:** The novelty is the discipline around the evidence: confirmed scope and possible exposure are computed separately, every evidence-to-trace proposal requires a human decision, source grounding is classified rather than assumed, and the system refuses to fabricate reconciliation when records do not support it.

**Full:** Klarrow is not a chatbot or a document store; it is a reconciliation workbench where the trace itself is built from sources a reviewer can inspect. Four design choices do the work: candidate links never count as confirmed exposure; relationship confirmation is kept separate from fact-level review; grounding is classified into value-in-quote, adjacent-line context, identifier-only and unlocated, and unlocated claims are flagged rather than given a fake line number. Rejecting or editing a supporting fact demotes dependent links back to re-review, and when quantities do not reconcile, mass balance reports "insufficient" instead of inventing a conversion. We do not claim to be first or best; the claim is that this combination of confirmed-versus-possible scope, evidence-to-trace proposals with human decisions, and an explicit grounding taxonomy is the part that differs from the ERP and document-management tooling we have reviewed.

### 2.7 South Africa relevance

**Short:** South African producers carry primary responsibility for food safety and accurate labelling under the Department of Health's Food Control framework, enforcement is delegated to municipal health services, and PPECB Food Safety Services audits for regulated agricultural export products cover traceability (scope: perishable produce). Klarrow targets the evidence-reconciliation layer that sits beside existing ERP/FSMS systems rather than replacing them.

**Full:** The South African framing is plausible rather than proven: the National Department of Health's Food Control page states that primary responsibility for food safety and accurate labelling rests with the producer, manufacturer, seller or importer, with enforcement delegated to municipal health services. Traceability is audited for regulated agricultural exports: PPECB Food Safety Services says its audits cover traceability, within a scope limited to perishable produce. Regulators can compel investigation: Consumer Protection Act 68 of 2008 s60 lets the National Consumer Commission require a producer to conduct an investigation or carry out a recall programme, and NICD's 2018 listeriosis statement shows how much multi-source evidence reconciliation such work involves (clinical isolates, patient interviews, environmental samples and final product samples). We position Klarrow as an overlay beside ERP/FSMS platforms: different audiences (municipal health services, PPECB/SA GAP and NCC/DoH reviewers, retail buyers) expect different evidence packs, and we are validating that contradictions in fragmented records are the real pain. One boundary: US FSMA 204 is a United States rule, not South African law; it may matter to South African exporters selling into the US, and we only say that when it is relevant.

### 2.8 Data privacy and security posture

**Short:** Klarrow is single-tenant and the Meta Llama 3.2 3B open-weight model runs locally via Ollama, so extraction does not require sending evidence to a third-party AI provider in the documented configuration; an optional API-key guard and CSV export sanitization are in place, and we publish the remaining gaps.

**Full:** Klarrow is designed as a single-tenant workbench running on the operator's own machine or network, and the documented extraction configuration uses a local Ollama endpoint with the Meta Llama 3.2 3B open-weight model; evidence files are not sent to a third-party AI provider for extraction. Guard rails that exist: an optional API-key header guard for application routes, SHA-256 duplicate-payload rejection at ingest, workspace provenance notices (synthetic or clean workspace) in exports, and CSV formula-trigger sanitization so exported files cannot inject spreadsheet formulas. Honest gaps, pointing to the open-risks section of `docs/pitch-readiness/validation-log.md`: the API-key guard is optional and leaves health, extraction-status and capabilities endpoints public when enabled, it is not a substitute for production authentication, TLS or deployment hardening, and no production security review has been completed. The hosted CI run also emitted dependency-vulnerability warnings that must be re-audited and patched before any deployment, and scanned images are stored and flagged without OCR processing. None of this is a certification or a guarantee of data safety; it is the current state of a prototype, and we would rather state the gaps than overclaim.

### 2.9 Team

**Short:** Klarrow is built by [founder name] in [city], South Africa, with [team members and roles]. Contact: [contact email], [phone]; legal status: [legal entity status].

**Full:** Klarrow is developed by [founder name], [role], based in [city], South Africa, with [team members and roles]. Legal entity status: [legal entity status]; [registration details if asked]. Relevant background: [founder and team background - fill only from facts you can state directly]. Contact details for this application: [contact email], [phone]. We are not claiming advisors, investors or partners who have not agreed to be named.

### 2.10 Availability for the in-person Johannesburg pitch

**Short:** Availability placeholder: [yes/no - confirm with the founder]. We [can/cannot] attend the in-person pitch competition in Johannesburg.

**Full:** The official event page states that selected startups pitch live in Johannesburg and that applicants must be available to participate fully in all required Pitchathon activities, including an in-person pitch competition. Our availability: [yes/no - confirm with the founder]. Note: the official page does not publish the exact pitch date; a 30 October 2026 date appears only in a secondary social-media post, and the deadline time zone is not stated on the page, so treat logistics as unconfirmed until the organiser communicates them.

### 2.11 What you want from the program

**Short:** Accelerator support, structured feedback on our practitioner-validation plan, and introductions to South African food producers, exporters or QA practitioners who could evaluate a first controlled deployment.

**Full:** Beyond the equity-free funding, advertising credits and summit invitation listed on the event page, the support that matters most to Klarrow is mentorship on moving an engineering-verified prototype into controlled evaluation with real organisations, and feedback on our traceability-overlay hypothesis. Concretely, introductions to South African food producers, exporters or QA practitioners who could evaluate the workbench in a controlled setting would let us test whether the reconciliation problem is real. We have no partners or endorsements to name, and we are not claiming any.

### 2.12 Anything else / final statement

**Short:** Klarrow is an engineering-verified prototype with no real customer data, no completed pilots and no revenue; we are applying to test whether the reconciliation problem is real for South African food businesses and to get honest feedback on what we have built.

**Full (one paragraph):** Klarrow is an engineering-verified prototype, not a company with traction: one authored fictional scenario benchmark (Run 4), a three-scenario evaluation suite, 43/43 .NET tests, and no real customer data, no completed pilots and no revenue. We built a working, auditable workbench and documented where it does not perform well, including the candidate noise the small local model adds on fragmented evidence. We are applying to find out whether this reconciliation problem is real for South African food businesses, and to get the feedback and introductions that would let us find out honestly. Being explicit about what is proven and what is not is part of the product, and it is how we would run any future evaluation.

## 3. Facts to have ready

Distilled from `submission/final-2026-10-18/APPLICATION_INPUTS_TO_CAPTURE.md`.

- [ ] Team/entity is based in South Africa (confirm the exact legal/eligibility wording the form requires).
- [ ] Working prototype/MVP: engineering-verified prototype; current tree passes 43/43 .NET tests.
- [ ] Meta technology: Meta Llama 3.2 3B open-weight model, run locally via Ollama (extraction only; deterministic code and humans decide).
- [ ] Problem: evidence reconciliation for food-traceability investigations (mock recalls, withdrawals, fragmented records).
- [ ] Demo: saved Run 4 scenario; pitch deck and one-pager prepared in `submission/final-2026-10-18/`.
- [ ] Honest stage: one authored fictional scenario benchmark (Run 4) plus a three-scenario evaluation suite; deterministic 13/13 on all three authored scenarios; llama 13/13 on two and 11/13 on the fragmented-evidence scenario with extra candidate links remaining in Candidate state and none auto-confirmed; no real customer data, no completed pilots, no customers, no revenue.
- [ ] Availability for the in-person Johannesburg pitch (confirm; see section 2.10).
- [ ] Contact details and team roles (fill the section 1 placeholders).
- [ ] Any question about traction, customers, revenue or pilots: answer honestly from the Stage section.
- [ ] Confirmation email or reference number to save after submission.

## 4. Do not say

Distilled from the input-capture rules and the claims and evidence register (v2.1.0) section 5.

**Traction and results**

- Do not claim customers, completed pilots, revenue, users or traction of any kind.
- Do not claim accuracy, precision or recall percentages, "verified accuracy" or "100% grounded".
- Do not claim "zero hallucination" or "zero data leakage".
- Do not claim time savings, including "53 seconds instead of hours" or any equivalent.
- Do not claim independent validation, real/FDA data or real customer data.
- Do not claim production readiness: no "production-ready", "enterprise-grade", "scales to enterprise" or "replaces your ERP".
- Do not claim compliance or certification: no "compliant", no "certified".
- Do not claim OCR: no "reads any document", no "reads scans/photos", no "OCR available"; scanned images are retained and flagged, not processed.
- Do not claim "agentic", "real-time" or "autonomous" behavior.
- Do not claim "all evidence reviewed": 36 facts remained unreviewed at the end of Run 4.
- Do not describe Klarrow as an "AI-powered recall platform that tells you which products/customers are affected".

**Technology and events**

- Do not write "open-source Llama" without licence qualification; use "Meta Llama 3.2 3B open-weight model, run locally via Ollama".
- Do not claim the organisers endorse Ollama or have expressly accepted local Ollama deployment; that acceptance is an inference, not an organiser confirmation.
- Do not say FSMA 204 is South African law; it is a US rule, and if it is mentioned at all, say explicitly that it applies to exporters selling into the US.
- Do not invent South African recall statistics or mock-recall deadlines; do not say R638/R146/R723 requires a specific lot-level trace or recall deadline; do not say PPECB certifies all food exports (its scope is perishable produce); do not say ISO 22000/BRCGS/FSSC 22000 is legally mandatory in South Africa.
- Do not say Klarrow is first, best, cheapest or only.
- Do not paste any answer before checking it against the captured question text and limits, and do not submit on the founder's behalf.

## 5. Pre-submission checklist

- [ ] Before answering: open the form while signed in and capture every question and help text exactly (screenshot or copy); do not guess questions.
- [ ] Capture character/word limits for each field.
- [ ] Capture required file formats and size limits (deck, PDF, video, screenshots).
- [ ] Determine whether a live demo link, recorded video or public URL is required.
- [ ] Confirm whether the deck must be uploaded or only a PDF is accepted.
- [ ] Confirm the exact deadline time in the portal's time zone (official page: applications close 20 October 2026; no time zone published; internal buffer date is 19 October 2026).
- [ ] Capture any eligibility declaration or terms checkbox.
- [ ] Capture any question about South African registration, team location or startup stage.
- [ ] Capture the exact wording of the Meta AI technology question and answer with the permitted model naming.
- [ ] Capture any traction/customers/revenue/pilots question and answer from the Stage section, stating plainly that none exist yet.
- [ ] Capture any pitch-date or in-person availability question and answer from the Availability section.
- [ ] Attach the pitch deck and one-pager in the requested formats from `submission/final-2026-10-18/`; verify PDF rendering and file sizes, and make sure the attachments contain no forbidden claims.
- [ ] Re-check every pasted answer against the Do not say list.
- [ ] After submitting, save the confirmation email or reference number.
- [ ] The founder submits; do not submit on the founder's behalf.
