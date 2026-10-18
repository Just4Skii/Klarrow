# Evaluation

Ground truth is hand-authored in `evaluation/ground-truth.json` and is not loaded into the application.

Automated test result (current environment): `dotnet test` → Passed: 8, Failed: 0.

Trace exercise RM-0427:
- Affected finished lots: FL-0514, FL-0515.
- Unaffected deceptive lot: FL-0516.
- Known destinations: Tuckshop Arendse (Benoni), Groot Pretoria Foods (Pty) Ltd, Cafe Kapabela.
- Missing dispatch page appears as one explicit gap; it is not treated as absence.
- Near-match `RM-O427` remains a warning and is not merged silently.
- Mass balance status: insufficient (unit mismatch/discrepancy), not a false green pass.

Provenance coverage: every trace link stores an evidence statement and source reference when available; parsed facts retain document, page/sheet/row, raw value, normalized value, confidence note, and provider metadata.
