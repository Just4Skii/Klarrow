> **Render status:** this outline is updated to Run 4. The existing PPTX/PDF were not regenerated in this pass; render and inspect them before submission.

# Klarrow - 9-slide pitch deck outline

This outline corresponds to `RecallGraph_Pitch_Deck.pptx`. It is a draft to adapt to the official competition form and time limit once confirmed.

1. **Cover - The evidence layer for food-traceability investigations**
   - One-line positioning and prototype label.
   - Visual: source -> claim -> review -> trace; missing evidence stays visible.

2. **Problem - The investigation starts before the data is clean**
   - Fragmented source documents, drifting identifiers and inconsistent quantities.
   - Avoid unsupported industry statistics.

3. **Solution - An evidence-reconciliation workbench**
   - Ingest, extract, review, trace.
   - Position alongside existing systems is a hypothesis; no ERP integration is demonstrated.

4. **How it works - AI proposes; evidence and people control the trace**
   - Parsers and local Llama -> grounding check -> human review -> trace and export.
   - Explain current limits: no OCR, quote-verbatim is not semantic correctness.

5. **Live case - One authored scenario**
   - Seven authored fictional documents; 15 AI facts, 35 parser facts; 4 value-in-quote verified, 3 adjacent-line context, 8 identifier-only flagged.
   - Eight relationship proposals; seven confirmed, one false positive rejected; RM-O873 deferred.

6. **Result - A trace showing supported and unresolved evidence**
   - Confirmed: FL-1021A, Bosveld and Highveld; FL-1034C excluded after false-positive rejection.
   - 320 kg received vs 180 kg consumed and 6,600 units downstream -> insufficient mass balance.

7. **Differentiation - Reviewable evidence, not an autonomous recall decision**
   - Demonstrated implementation vs unvalidated hypotheses.
   - Do not claim superior to incumbents without comparative research.

8. **Benchmark - Observed results, not an accuracy study**
   - 57.9s measured wall-clock for 7 docs; 4 value-in-quote, 3 adjacent-line context, 8 identifier-only; 7 confirmed links / 1 rejected.
   - Caveat: 14 facts confirmed, 36 still unreviewed; single authored scenario; no manual baseline or accuracy claim.

9. **Ask - Help validate the workflow**
   - Practitioner review, authorised/redacted records, measured manual-vs-tool evaluation.
   - Explicitly not production-ready, certified or compliant.
