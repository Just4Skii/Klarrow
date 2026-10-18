# Architecture

Klarrow is a single ASP.NET Core Web API with EF Core/SQLite persistence, a React + TypeScript + Vite frontend, and optional model-backed services.

- `src/RecallGraph.Application`: enums and DTOs/entities.
- `src/RecallGraph.Infrastructure`: SQLite context, parser adapters, workspace/evidence/fact/trace/extraction/seed/audit services.
- `src/RecallGraph.Api`: controllers, startup, health/export/audit endpoints.
- `src/frontend`: operations UI.
- `seed/evidence`: synthetic evidence files (24 artifacts).
- `tests`: xUnit API/provider tests.

Core data flow: import → parse → candidate facts → human review → deterministic trace links → trace run snapshot → export/audit. LLM output is treated as untrusted candidate data and never changes confirmed graph relationships.

Large model integrations (Llama, DINOv2, SAM 2, Prompt Guard) are optional and lazy/config-gated; core startup does not download weights.
