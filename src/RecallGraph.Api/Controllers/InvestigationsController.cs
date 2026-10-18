using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/investigations")]
public sealed class InvestigationsController(WorkspaceService workspaces, InvestigationService investigations) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<InvestigationDto>>> List(CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await investigations.ListAsync(ws.Id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<InvestigationDto>> Create([FromBody] SaveInvestigationDto request, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        try
        {
            var created = await investigations.CreateAsync(ws.Id, request, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvestigationDto>> Get(Guid id, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        var investigation = await investigations.GetAsync(ws.Id, id, ct);
        return investigation == null ? NotFound() : Ok(investigation);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<InvestigationDto>> Update(Guid id, [FromBody] SaveInvestigationDto request, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        try
        {
            return Ok(await investigations.UpdateAsync(ws.Id, id, request, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
