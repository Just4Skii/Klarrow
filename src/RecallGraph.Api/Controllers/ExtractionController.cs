using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/extraction")]
public sealed class ExtractionController(WorkspaceService workspaces, ExtractionService extraction, IAiExtractionProvider provider, OllamaHealthService health) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<AiProviderStatusDto>> Status(CancellationToken ct) => Ok(await extraction.GetStatusAsync(ct));

    [HttpGet("health")]
    public Task<OllamaHealthDto> Health(CancellationToken ct) => health.CheckAsync(ct);

    [HttpPost("smoke")]
    public async Task<ActionResult<LlamaSmokeResultDto>> Smoke(CancellationToken ct)
    {
        if (!provider.Status.IsConfigured)
            return Ok(new LlamaSmokeResultDto { Configured = false, Success = false, Error = "No Llama endpoint/model configured; live smoke test not attempted.", CheckedAt = DateTime.UtcNow });

        var result = await provider.ExtractAsync(
            "Delivery note DN-99999 dated 2026-09-10 for raw lot RM-0427, quantity 1200 kg.",
            null, null, "smoke-test.txt", ct);
        return Ok(new LlamaSmokeResultDto
        {
            Configured = true,
            Success = result.IsValid,
            Provider = result.Provider,
            ModelId = result.ModelId,
            FactsReturned = result.Facts.Count,
            Error = result.Error,
            CheckedAt = DateTime.UtcNow
        });
    }

    [HttpPost("run")]
    public async Task<ActionResult<ExtractionRunResultDto>> Run([FromBody] ExtractionRunRequest request, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        var docIds = request?.DocumentIds;
        return Ok(await extraction.RunAsync(ws.Id, docIds, request?.Reviewer ?? "reviewer", ct));
    }
}

public sealed class ExtractionRunRequest
{
    public List<Guid>? DocumentIds { get; set; }
    public string? Reviewer { get; set; }
}
