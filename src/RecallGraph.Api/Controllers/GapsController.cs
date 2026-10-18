using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/gaps")]
public sealed class GapsController(WorkspaceService workspaces, GapEngine gaps) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GapFindingDto>>> List([FromQuery] Guid? investigationId, [FromQuery] bool refresh = false, CancellationToken ct = default)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(refresh
            ? await gaps.RefreshAsync(ws.Id, investigationId, ct)
            : await gaps.ListAsync(ws.Id, investigationId, ct));
    }
}
