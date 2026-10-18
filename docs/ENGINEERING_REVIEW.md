> **HISTORICAL BASELINE.** This review predates the Run 4 changes and says Llama was not live. Current status is in `docs/pitch-readiness/implementation-and-validation-report.md`; Run 4 used real local `llama3.2:3b` inference on the authored scenario.
# Engineering Review

Baseline verified:
- Backend build succeeds.
- `dotnet test`: Passed 8, Failed 0.
- Frontend `npm run build`: success.
- Playwright E2E: 2/2 pass (core trace plus fact review controls).

Current highest-cost constraints are documentation around optional model integrations and honest capability status. The core traceability workflow passes its independently asserted ground-truth scenario using application code, not LLM reasoning. The seeded RM-0427 trace reaches FL-0514 and FL-0515, excludes FL-0516, shows three known destinations, and leaves one missing-dispatch gap. Mass balance remains insufficient.

Real statuses:
- Llama: adapter code and stub/contract tests only; no live inference executed.
- DINOv2 + FAISS: unavailable; dependencies/checkpoint absent.
- SAM 2: unavailable; weights/runtime absent.
- Prompt Guard classifier: not live; deterministic framing/validator safeguards documented.
