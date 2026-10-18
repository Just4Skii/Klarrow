# Model and software licenses

| Component | Version/Checkpoint | Purpose | Code license | Weights license | Commercial caveat | Runtime required | Actual execution | Source / verification date |
|---|---|---|---|---|---|---|---|---|
| UglyToad.PdfPig | 1.7.0-custom-5 | PDF text extraction | Apache 2.0 / project terms | n/a | Verify current package notice | Core runtime | Yes | NuGet, 2026-10-14 |
| ClosedXML | 0.105.0 | XLSX parsing | MIT | n/a | None noted | Core runtime | Yes | NuGet, 2026-10-14 |
| CsvHelper | 33.0.1 | CSV parsing | Apache 2.0 / project terms | n/a | Verify notice requirements | Core runtime | Yes | NuGet, 2026-10-14 |
| EF Core Sqlite | 10.0.0 | Persistence | MIT (Microsoft) | n/a | None noted | Core runtime | Yes | NuGet, 2026-10-14 |
| Vite/React | project templates | Frontend build/runtime | MIT | n/a | None noted | Frontend | Yes | npm, 2026-10-14 |
| Meta Llama | `llama3.2:3b` (Q4_K_M) via Ollama OpenAI-compatible API; `llama3.1:8b` also installed | Candidate fact extraction only | Community license terms | Meta Llama 3.2 Community License; not Apache/MIT | Not unrestricted OSS; consult license; "Built with Llama" notice required | Optional | Yes - smoke endpoint and one document extraction succeeded 2026-10-15 | Ollama tags + Meta docs; re-verify terms before commercial use |
| DINOv2 | not installed | Optional visual evidence retrieval | Research/test as applicable | Requires checkpoint-specific terms | Verify before commercial use | Optional | No | not installed |
| FAISS | not installed | Nearest-neighbour search | Platform license varies | n/a | Verify terms | Optional | No | not installed |
| SAM 2 | not installed | Optional mask crop | Platform terms vary | Requires checkpoint-specific terms | Non-commercial or use-specific restrictions may apply | Optional | No | not installed |
| PyTorch | not installed | Backend for optional vision models | BSD-style project terms | n/a | Verify CUDA/runtime constraints | Optional | No | not installed |
| Prompt Guard / LlamaFirewall | not used | Potential classifier | n/a | n/a | Not verified; deterministic safeguards only | Optional/unavailable | No | not installed |

Built with Llama - optional integration only. The default app is synthetic and Llama-agnostic.
