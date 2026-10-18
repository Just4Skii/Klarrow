using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/relationship-proposals")]
public sealed class RelationshipProposalsController(WorkspaceService workspaces, RelationshipService relationships) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RelationshipProposalDto>>> List([FromQuery] Guid? investigationId, CancellationToken ct = default)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await relationships.ListAsync(ws.Id, investigationId, ct));
    }

    [HttpPost("generate")]
    public async Task<ActionResult<GenerateRelationshipsResultDto>> Generate([FromBody] GenerateRelationshipsRequestDto? request, [FromQuery] Guid? investigationId, CancellationToken ct = default)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        var targetInvestigationId = request?.InvestigationId ?? investigationId;
        var result = await relationships.GenerateProposalsAsync(ws.Id, targetInvestigationId, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<RelationshipProposalDto>> Confirm(Guid id, [FromBody] ReviewDecisionDto? decision, CancellationToken ct = default)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        try
        {
            var result = await relationships.ConfirmAsync(ws.Id, id, decision ?? new ReviewDecisionDto(), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<RelationshipProposalDto>> Reject(Guid id, [FromBody] ReviewDecisionDto? decision, CancellationToken ct = default)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        try
        {
            var result = await relationships.RejectAsync(ws.Id, id, decision ?? new ReviewDecisionDto(), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
