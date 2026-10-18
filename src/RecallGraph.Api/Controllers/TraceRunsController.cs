using Microsoft.AspNetCore.Mvc;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Api.Controllers;

[ApiController]
[Route("api/trace-runs")]
public sealed class TraceRunsController(WorkspaceService workspaces, TraceService trace, AuditService audit, FactService factsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TraceRunSummaryDto>>> List(CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        return Ok(await trace.ListRunsAsync(ws.Id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<TraceResultDto>> Run([FromBody] TraceRunRequestDto request, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        try
        {
            return Ok(await trace.RunAsync(ws.Id, request, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TraceResultDto>> Get(Guid id, CancellationToken ct)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        var result = await trace.GetRunAsync(ws.Id, id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromQuery] string format, [FromQuery] string actor = "operator", CancellationToken ct = default)
    {
        var ws = await workspaces.GetOrCreateDemoAsync(ct);
        var result = await trace.GetRunAsync(ws.Id, id, ct);
        if (result == null) return NotFound();
        var audits = await audit.ListAsync(ws.Id, 500, ct);
        result.AuditTrail = audits;
        format = (format ?? "html").ToLowerInvariant();

        byte[] bytes;
        string contentType;
        string fileName;
        switch (format)
        {
            case "xlsx":
                var facts = await factsService.ListAsync(ws.Id, result.InvestigationId, ct);
                bytes = ExportFormatter.BuildXlsx(result, audits, facts);
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                fileName = BuildExportFileName(result, "xlsx");
                break;
            case "csv":
                bytes = ExportFormatter.BuildCsvBytes(result, audits);
                contentType = "text/csv";
                fileName = BuildExportFileName(result, "csv");
                break;
            case "json":
                bytes = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
                contentType = "application/json";
                fileName = BuildExportFileName(result, "json");
                break;
            case "html":
                bytes = System.Text.Encoding.UTF8.GetBytes(ExportFormatter.BuildHtml(result, audits));
                contentType = "text/html";
                fileName = BuildExportFileName(result, "html");
                break;
            default:
                return BadRequest("Unsupported export format. Use xlsx, html, json or csv.");
        }

        await audit.AddAsync(ws.Id, "trace_exported", string.IsNullOrWhiteSpace(actor) ? "operator" : actor, $"Exported trace report for '{result.SelectedId}' as {format.ToUpperInvariant()} package.", "TraceRun", id.ToString(), $"{{\"format\":\"{format}\"}}", ct);
        return File(bytes, contentType, fileName);
    }

    private static string BuildExportFileName(TraceResultDto result, string extension)
    {
        var reference = string.IsNullOrWhiteSpace(result.InvestigationReference) ? result.SelectedId : result.InvestigationReference;
        var raw = $"Klarrow_{reference}_{result.CompletedAt:yyyyMMdd}";
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(raw.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", "-");
        clean = System.Text.RegularExpressions.Regex.Replace(clean, "-+", "-").Trim('-', '.');
        if (clean.Length > 80) clean = clean[..80].Trim('-', '.');
        return $"{clean}.{extension}";
    }
}
