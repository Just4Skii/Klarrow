# Klarrow - Five-minute pitch script

**Use only after a full pre-demo dry run.** Run 4 used an authored fictional scenario. Do not describe its input documents as customer data. Replace no numbers unless new evidence has been collected and checked.

## 0:00-0:30 - Hook

Imagine a supplier calls a food producer and flags a raw-material lot. The immediate questions are: which production batches used it, which customers received those batches, and where does the evidence stop lining up?

The answer may be spread across receiving notes, batch sheets and dispatch records - with lot codes that differ by one character and quantities that do not reconcile.

## 0:30-1:05 - Problem and positioning

Klarrow is an evidence-reconciliation workbench for food-traceability investigations. It is designed for the messy investigation moment: use the records a business already has, show the source behind a claim, and make uncertainty visible instead of treating every possible link as a fact.

We are not replacing an ERP and we have not yet proven that existing platforms cannot handle this workflow. We are testing whether an evidence-first layer can make cross-document investigations more reviewable.

## 1:05-2:50 - Live product

Start in the isolated Run 4 investigation. Show the seven authored scenario documents and explain that local Llama extracted candidate facts from supported text documents while deterministic parsers handled structured text and tables.

Open a claim's source locator. Explain that the system distinguishes quote-verbatim grounding from an identifier-only match. It does not read scanned images yet, and a matching quote alone does not prove the model interpreted the value correctly.

Open Candidate Trace Links. Show the proposed relationship, its evidence, and the explicit decision. In the recorded run, a reviewer confirmed seven relationship proposals and rejected one false-positive dispatch relationship. Show that the confusable identifier RM-O873 was deferred rather than silently merged.

Run/open the saved trace. Show FL-1021A and the known Bosveld and Highveld destinations. Show FL-1034C excluded from the RM-0873 trace while the alias is deferred. Then show the quantity mismatch and explain why mass balance remains insufficient: 320 kg received, 180 kg recorded consumed and 6,600 units downstream cannot be reconciled without an evidenced conversion.

## 2:50-3:35 - Evidence, uncertainty and AI

In this run, AI extraction took 57.9 seconds measured wall-clock. The app produced 15 AI candidate facts and 35 parser facts. Four model facts were value-in-quote verified, three were honest adjacent-line context matches, and eight were identifier-only and left unreviewed. That split is why we do not call this an accuracy result and still require human review.

The point is not that the AI is always right. The point is that the system should show its source, preserve uncertainty and let the operator reject a bad relationship before it becomes part of confirmed scope.

## 3:35-4:15 - What this proves, and what it doesn't

We have demonstrated local Llama inference, two-layer review, a persisted trace and export generation in one controlled, fictional scenario. The current suite passes 40 .NET tests and 25 browser tests. We have not measured time saved versus a manual process, validated on independent business records, interviewed customers, or established willingness to pay. We will not claim those things before we measure them.

## 4:15-5:00 - Ask

Our next step is a supervised evaluation with a food-safety or quality practitioner. We are looking for expert review, authorised or redacted test records, and a timed comparison of the manual mock-recall process against Klarrow. We want to learn where the evidence model helps, where it fails, and what must be true before anyone could rely on it in a real workflow.

Klarrow: the evidence-reconciliation workbench for food-traceability investigations.

**Presenter reminder:** keep the Llama API running; confirm the current model status; open the selected investigation before the pitch; use a saved trace where appropriate; have a 2-3 minute screen recording ready as a backup; never use customer, accuracy, time-saving or market claims that are not supported by evidence.
