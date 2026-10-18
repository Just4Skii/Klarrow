# Assumptions and open questions

- The producer scenario is fictional and all seeded data is synthetic and labelled as such in the UI and exports.
- No external AI model was required for the seeded synthetic demonstration workspace.
- The synthetic graph is seeded by a deterministic fixture but trace traversal and report generation are application code. Fixture-sourced records are distinguished from model-extracted facts in the UI, API payloads, and exported reports.
- Llama, DINOv2, SAM 2, and Prompt Guard classifiers are not verified as live in this environment.
- Whether investigators find the UI faster than manual document review is not validated; any speed claim requires an evaluator-recorded manual baseline.
- `FL-0516` is intentionally a deceptively similar unaffected lot.
