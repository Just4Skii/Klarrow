using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Infrastructure;

public sealed class MetaCapabilityService(LlamaOptions llamaOptions, RecallGraphDbContext db)
{
    public List<CapabilityStatusDto> GetStatuses()
    {
        var liveFacts = db.ExtractedFacts.Any(x => x.ExtractionMethod == "ai-extraction");
        return
        [
            new CapabilityStatusDto
            {
                Id = "llama-extraction",
                Name = "Meta Llama",
                Version = string.IsNullOrWhiteSpace(llamaOptions.Model) ? "not-configured" : llamaOptions.Model,
                ExecutionMode = llamaOptions.IsConfigured ? "local-openai-compatible" : "unavailable",
                Availability = llamaOptions.IsConfigured ? "configured" : "unavailable",
                RealInferenceExecuted = liveFacts,
                Detail = llamaOptions.IsConfigured
                    ? (liveFacts
                        ? $"Configured and a live run of {llamaOptions.Model} has produced candidate facts with per-fact grounding status. Facts still require human review."
                        : "Configured. No live extraction has completed yet; run the smoke endpoint or an extraction first.")
                    : "Set LLAMA_API_BASE_URL, LLAMA_API_KEY, and LLAMA_MODEL. Ollama or another OpenAI-compatible local server may be used.",
                LicenseRegister = "docs/model-and-software-licenses.md#meta-llama",
                Setup = "LLAMA_API_BASE_URL=http://127.0.0.1:11434/v1 LLAMA_MODEL=llama3.2:3b dotnet run --project src/RecallGraph.Api"
            },
            new CapabilityStatusDto
            {
                Id = "dino-v2",
                Name = "Meta DINOv2 + FAISS",
                Version = "not-installed",
                ExecutionMode = "unavailable",
                Availability = "unavailable",
                RealInferenceExecuted = false,
                Detail = "torchvision/faiss runtime dependencies or a checkpoint are not installed. A candidate-only interface is kept in docs; visual similarity never confirms identity.",
                LicenseRegister = "docs/model-and-software-licenses.md#dinov2-and-faiss",
                Setup = "Install the verified runtime and provide a checkpoint path through an environment variable. Do not enable it in the default workspace configuration."
            },
            new CapabilityStatusDto
            {
                Id = "sam-2",
                Name = "Meta SAM 2",
                Version = "not-installed",
                ExecutionMode = "unavailable",
                Availability = "unavailable",
                RealInferenceExecuted = false,
                Detail = "SAM 2, PyTorch, and weights are unavailable. Segmentation would only isolate pixels and never identify a product or establish traceability.",
                LicenseRegister = "docs/model-and-software-licenses.md#sam-2",
                Setup = "Install the verified runtime and provide a checkpoint path through an environment variable, loading lazily."
            },
            new CapabilityStatusDto
            {
                Id = "prompt-guard",
                Name = "Document-injection / Prompt Guard",
                Version = "deterministic-safeguards-only",
                ExecutionMode = "local-deterministic",
                Availability = "partial",
                RealInferenceExecuted = false,
                Detail = "Untrusted document text is framed separately from control instructions and schema-validated. No live classifier has been executed.",
                LicenseRegister = "docs/model-and-software-licenses.md#prompt-guard-and-input-isolation",
                Setup = "No external service is required for the current deterministic isolation safeguards."
            }
        ];
    }
}
