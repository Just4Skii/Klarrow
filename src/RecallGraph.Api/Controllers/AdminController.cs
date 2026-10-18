using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(SeedService seed, WorkspaceService workspaces) : ControllerBase
{
    public sealed record SeedRequest(bool Reset);

    [HttpPost("seed")]
    public async Task<ActionResult<SeedResultDto>> Seed([FromBody] SeedRequest request, CancellationToken ct)
    {
        return Ok(await seed.SeedAsync(request?.Reset ?? false, ct));
    }

    [HttpGet("healthcheck")]
    public ActionResult<object> Health() => Ok(new { status = "ok", time = DateTime.UtcNow });
}
