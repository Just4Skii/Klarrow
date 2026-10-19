using Microsoft.EntityFrameworkCore;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class InvestigationService(RecallGraphDbContext db, AuditService audit)
{
    public async Task<List<InvestigationDto>> ListAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var investigations = await db.Investigations.Where(x => x.WorkspaceId == workspaceId).OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);
        var runCounts = await db.TraceRuns.Where(x => x.WorkspaceId == workspaceId && x.InvestigationId != null)
            .GroupBy(x => x.InvestigationId!.Value)
            .Select(g => new { InvestigationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InvestigationId, x => x.Count, ct);
        return investigations.Select(x => Map(x, runCounts.TryGetValue(x.Id, out var count) ? count : 0)).ToList();
    }

    public async Task<InvestigationDto?> GetAsync(Guid workspaceId, Guid id, CancellationToken ct = default)
    {
        var investigation = await db.Investigations.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct);
        if (investigation == null) return null;
        var count = await db.TraceRuns.CountAsync(x => x.WorkspaceId == workspaceId && x.InvestigationId == id, ct);
        return Map(investigation, count);
    }

    public async Task<InvestigationDto> CreateAsync(Guid workspaceId, SaveInvestigationDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Investigation name is required.");
        var investigation = new Investigation
        {
            WorkspaceId = workspaceId,
            Name = request.Name.Trim(),
            Reference = request.Reference.Trim(),
            SuspectLotType = request.SuspectLotType.Trim(),
            SuspectLotId = request.SuspectLotId.Trim(),
            Description = request.Description.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Open" : request.Status.Trim(),
            CreatedBy = string.IsNullOrWhiteSpace(request.Actor) ? "operator" : request.Actor.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Investigations.Add(investigation);
        await db.SaveChangesAsync(ct);
        await audit.AddAsync(workspaceId, "investigation_created", investigation.CreatedBy, $"Created investigation '{investigation.Name}' ({investigation.Reference}).", "Investigation", investigation.Id.ToString(), "{}", ct);
        return Map(investigation, 0);
    }

    public async Task<InvestigationDeleteResultDto> DeleteAsync(Guid workspaceId, Guid id, string actor, CancellationToken ct = default)
    {
        var investigation = await db.Investigations.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct)
            ?? throw new KeyNotFoundException("Investigation not found.");

        var documents = await db.SourceDocuments.Where(x => x.WorkspaceId == workspaceId && x.InvestigationId == id).ToListAsync(ct);
        var documentIds = documents.Select(x => x.Id).ToList();
        var facts = documentIds.Count == 0 ? new List<ExtractedFact>() : await db.ExtractedFacts.Where(x => documentIds.Contains(x.SourceDocumentId)).ToListAsync(ct);
        var factIds = facts.Select(x => x.Id).ToList();
        var reviewEvents = factIds.Count == 0 ? new List<FactReviewEvent>() : await db.FactReviewEvents.Where(x => factIds.Contains(x.ExtractedFactId)).ToListAsync(ct);
        var locations = documentIds.Count == 0 ? new List<SourceLocation>() : await db.SourceLocations.Where(x => documentIds.Contains(x.SourceDocumentId)).ToListAsync(ct);
        var runs = await db.TraceRuns.Where(x => x.WorkspaceId == workspaceId && x.InvestigationId == id).ToListAsync(ct);
        var runIds = runs.Select(x => x.Id).ToList();
        var links = await db.TraceLinks.Where(x => x.WorkspaceId == workspaceId && x.InvestigationId == id).ToListAsync(ct);
        var findings = await db.GapFindings.Where(x => x.WorkspaceId == workspaceId && (x.InvestigationId == id || (x.TraceRunId != null && runIds.Contains(x.TraceRunId.Value)))).ToListAsync(ct);

        var filePaths = documents.SelectMany(x => new[] { x.StoredPath, x.DerivedTextPath })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        db.FactReviewEvents.RemoveRange(reviewEvents);
        db.SourceLocations.RemoveRange(locations);
        db.ExtractedFacts.RemoveRange(facts);
        db.SourceDocuments.RemoveRange(documents);
        db.TraceLinks.RemoveRange(links);
        db.GapFindings.RemoveRange(findings);
        db.TraceRuns.RemoveRange(runs);
        db.Investigations.Remove(investigation);
        await db.SaveChangesAsync(ct);

        var filesDeleted = 0;
        foreach (var path in filePaths)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                    filesDeleted++;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        var result = new InvestigationDeleteResultDto
        {
            Deleted = true,
            InvestigationId = id,
            Reference = investigation.Reference,
            DocumentsDeleted = documents.Count,
            FactsDeleted = facts.Count,
            ReviewEventsDeleted = reviewEvents.Count,
            TraceLinksDeleted = links.Count,
            TraceRunsDeleted = runs.Count,
            FindingsDeleted = findings.Count,
            FilesDeleted = filesDeleted,
            Limitations =
            [
                "Workspace-level entities (suppliers, raw-material lots, batches, finished lots, customers, locations), match proposals, entity aliases, extraction-run records and audit history are not investigation-scoped in this prototype and are retained.",
                "Export packages downloaded before deletion exist outside the application database and cannot be removed by this action.",
                "Only evidence uploaded against this investigation (documents and derived text files) is removed; documents uploaded without an investigation link are untouched."
            ]
        };

        await audit.AddAsync(workspaceId, "investigation_deleted", string.IsNullOrWhiteSpace(actor) ? "operator" : actor,
            $"Deleted investigation '{investigation.Name}' ({investigation.Reference}) and its investigation-linked evidence. Audit history retained; evidence content and derived files removed.",
            "Investigation", id.ToString(),
            $"{{\"documents\":{documents.Count},\"facts\":{facts.Count},\"reviewEvents\":{reviewEvents.Count},\"traceLinks\":{links.Count},\"traceRuns\":{runs.Count},\"findings\":{findings.Count},\"files\":{filesDeleted}}}", ct);

        return result;
    }

    public async Task<InvestigationDto> UpdateAsync(Guid workspaceId, Guid id, SaveInvestigationDto request, CancellationToken ct = default)
    {
        var investigation = await db.Investigations.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct) ?? throw new KeyNotFoundException("Investigation not found.");
        if (!string.IsNullOrWhiteSpace(request.Name)) investigation.Name = request.Name.Trim();
        investigation.Reference = request.Reference.Trim();
        investigation.SuspectLotType = request.SuspectLotType.Trim();
        investigation.SuspectLotId = request.SuspectLotId.Trim();
        investigation.Description = request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status)) investigation.Status = request.Status.Trim();
        investigation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.AddAsync(workspaceId, "investigation_updated", string.IsNullOrWhiteSpace(request.Actor) ? "operator" : request.Actor, $"Updated investigation '{investigation.Name}' ({investigation.Reference}) status {investigation.Status}.", "Investigation", investigation.Id.ToString(), "{}", ct);
        var count = await db.TraceRuns.CountAsync(x => x.WorkspaceId == workspaceId && x.InvestigationId == id, ct);
        return Map(investigation, count);
    }

    private static InvestigationDto Map(Investigation x, int traceRunCount) => new()
    {
        Id = x.Id,
        WorkspaceId = x.WorkspaceId,
        Name = x.Name,
        Reference = x.Reference,
        SuspectLotType = x.SuspectLotType,
        SuspectLotId = x.SuspectLotId,
        Description = x.Description,
        Status = x.Status,
        CreatedBy = x.CreatedBy,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
        TraceRunCount = traceRunCount
    };
}
