using Microsoft.EntityFrameworkCore;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class WorkspaceService(RecallGraphDbContext db)
{
    public async Task<Workspace> GetOrCreateDemoAsync(CancellationToken ct = default)
    {
        var existing = await db.Workspaces.FirstOrDefaultAsync(ct);
        if (existing != null) return existing;
        var workspace = new Workspace
        {
            Name = "Savanna Bites (Pty) Ltd - synthetic demonstration workspace",
            Scenario = "Synthetic snack producer evidence pack",
            Description = "Fictional South African RTE snack producer. Suspect raw material lot RM-0427. All data is synthetic and must not be used for operational food-safety decisions.",
            IsDemo = true
        };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(ct);
        return workspace;
    }

    public async Task<Workspace> GetOrCreatePilotAsync(CancellationToken ct = default)
    {
        var existing = await db.Workspaces.FirstOrDefaultAsync(ct);
        if (existing != null) return existing;
        var workspace = new Workspace
        {
            Name = "Pilot workspace",
            Scenario = "Operator-supplied records",
            Description = "Empty pilot workspace. Import only records the business has authorised for this investigation.",
            IsDemo = false
        };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(ct);
        return workspace;
    }

    public async Task<WorkspaceDto> GetHomeAsync(CancellationToken ct = default)
    {
        var workspace = await GetOrCreateDemoAsync(ct);
        var docs = await db.SourceDocuments.CountAsync(x => x.WorkspaceId == workspace.Id, ct);
        var syntheticDocs = await db.SourceDocuments.CountAsync(x => x.WorkspaceId == workspace.Id && x.SourceKind == KnowledgeSource.SeededFixture, ct);
        var facts = await db.ExtractedFacts.CountAsync(x => x.WorkspaceId == workspace.Id, ct);
        var unreviewed = await db.ExtractedFacts.CountAsync(x => x.WorkspaceId == workspace.Id && x.ReviewStatus == ReviewStatus.Unreviewed, ct);
        var runs = await db.TraceRuns.CountAsync(x => x.WorkspaceId == workspace.Id, ct);
        // Canonical "open gaps" metric = persisted findings, matching the Findings page. Trace-link
        // Gap rows are a different store and are reported in trace/gap views, not this counter.
        var gaps = await db.GapFindings.CountAsync(x => x.WorkspaceId == workspace.Id && x.Status == "Open", ct);

        var investigations = await db.Investigations
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);
        var seededInvestigationIds = await db.SourceDocuments
            .Where(x => x.WorkspaceId == workspace.Id && x.SourceKind == KnowledgeSource.SeededFixture && x.InvestigationId != null)
            .Select(x => x.InvestigationId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var primaryInvestigation = investigations.FirstOrDefault(x => seededInvestigationIds.Contains(x.Id)) ?? investigations.FirstOrDefault();

        return new WorkspaceDto
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Scenario = workspace.Scenario,
            Description = workspace.Description,
            IsDemo = workspace.IsDemo,
            Mode = workspace.IsDemo ? "demo" : "pilot",
            SyntheticDocumentCount = syntheticDocs,
            HasSyntheticData = syntheticDocs > 0,
            PrimaryInvestigationId = primaryInvestigation?.Id,
            CreatedAt = workspace.CreatedAt,
            DocumentCount = docs,
            FactCount = facts,
            UnreviewedFactCount = unreviewed,
            TraceRunCount = runs,
            OpenGapCount = gaps
        };
    }
}
