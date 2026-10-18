using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/workspace")]
public sealed class WorkspaceController(WorkspaceService workspaces, FactService facts, TraceService trace, AuditService audit) : ControllerBase
{
    [HttpGet("home")]
    public Task<WorkspaceDto> Home(CancellationToken ct) => workspaces.GetHomeAsync(ct);

    [HttpGet("facts")]
    public async Task<ActionResult<List<FactDto>>> Facts([FromQuery] Guid? investigationId, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await facts.ListAsync(ws.Id, investigationId, ct));
    }

    [HttpGet("lots")]
    public async Task<ActionResult<List<LotOptionDto>>> Lots([FromQuery] string? query, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await trace.ListLotsAsync(ws.Id, query, ct));
    }

    [HttpGet("audits")]
    public async Task<ActionResult<List<AuditEventDto>>> Audits(CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await audit.ListAsync(ws.Id, 300, ct));
    }
}
