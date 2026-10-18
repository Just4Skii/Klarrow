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
