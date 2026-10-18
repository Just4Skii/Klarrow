using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(WorkspaceService workspaces, DocumentService documents, AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> List([FromQuery] Guid? investigationId, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await documents.ListAsync(ws.Id, investigationId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> Get(Guid id, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        var dto = await documents.GetAsync(ws.Id, id, ct);
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<ActionResult<SourceContentDto>> Content(Guid id, [FromQuery] Guid? investigationId, [FromQuery] string? locator, [FromQuery] int context, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        var content = await documents.GetSourceContentAsync(ws.Id, id, investigationId, locator, context == 0 ? 10 : context, ct);
        return content == null ? NotFound() : Ok(content);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<ImportResultDto>> Upload(IFormFile file, [FromForm] Guid? investigationId, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequest("File is required.");
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        try
        {
            var result = await documents.UploadAsync(ws.Id, file.FileName, file.ContentType, ms.ToArray(), false, investigationId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
