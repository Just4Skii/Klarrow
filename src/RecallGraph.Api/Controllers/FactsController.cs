using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/facts")]
public sealed class FactsController(WorkspaceService workspaces, FactService facts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FactDto>>> List([FromQuery] Guid? investigationId, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await facts.ListAsync(ws.Id, investigationId, ct));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<FactHistoryDto>>> History(Guid id, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await facts.HistoryAsync(ws.Id, id, ct));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<FactDto>> Patch(Guid id, [FromBody] FactPatchDto patch, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        try
        {
            return Ok(await facts.PatchAsync(ws.Id, id, patch, "edited", ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<FactDto>> Confirm(Guid id, [FromBody] ReviewDecisionDto decision, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        try { return Ok(await facts.ConfirmAsync(ws.Id, id, decision ?? new ReviewDecisionDto(), ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<FactDto>> Reject(Guid id, [FromBody] ReviewDecisionDto decision, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        try { return Ok(await facts.RejectAsync(ws.Id, id, decision ?? new ReviewDecisionDto(), ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
