using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/match-proposals")]
public sealed class MatchProposalsController(WorkspaceService workspaces, MatchService matches) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MatchProposalDto>>> List([FromQuery] bool refresh = false, CancellationToken ct = default)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(refresh ? await matches.RefreshAsync(ws.Id, ct) : await matches.ListAsync(ws.Id, ct));
    }

    [HttpGet("/api/entity-aliases")]
    public async Task<ActionResult<List<EntityAliasDto>>> Aliases(CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await matches.AliasesAsync(ws.Id, ct));
    }

    [HttpPost("{id:guid}/confirm")]
    public Task<ActionResult<MatchProposalDto>> Confirm(Guid id, [FromBody] MatchDecisionDto decision, CancellationToken ct)
        => Decide(id, "confirm", decision, ct);

    [HttpPost("{id:guid}/reject")]
    public Task<ActionResult<MatchProposalDto>> Reject(Guid id, [FromBody] MatchDecisionDto decision, CancellationToken ct)
        => Decide(id, "reject", decision, ct);

    [HttpPost("{id:guid}/defer")]
    public Task<ActionResult<MatchProposalDto>> Defer(Guid id, [FromBody] MatchDecisionDto decision, CancellationToken ct)
        => Decide(id, "defer", decision, ct);

    private async Task<ActionResult<MatchProposalDto>> Decide(Guid id, string action, MatchDecisionDto decision, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        try
        {
            return Ok(await matches.DecideAsync(ws.Id, id, action, decision ?? new MatchDecisionDto(), ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
