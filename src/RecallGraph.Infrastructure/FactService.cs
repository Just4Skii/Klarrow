using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class FactService(RecallGraphDbContext db, AuditService audit)
{
    public async Task<List<FactDto>> ListAsync(Guid workspaceId, Guid? investigationId = null, CancellationToken ct = default)
    {
        var scopedDocumentIds = await db.SourceDocuments
            .Where(x => x.WorkspaceId == workspaceId && (investigationId == null || x.InvestigationId == investigationId))
            .Select(x => x.Id)
            .ToListAsync(ct);
        var facts = await db.ExtractedFacts.Where(x => x.WorkspaceId == workspaceId && scopedDocumentIds.Contains(x.SourceDocumentId)).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var docNames = await db.SourceDocuments.Where(x => x.WorkspaceId == workspaceId).ToDictionaryAsync(x => x.Id, x => x.FileName, ct);
        var docKinds = await db.SourceDocuments.Where(x => x.WorkspaceId == workspaceId).ToDictionaryAsync(x => x.Id, x => x.SourceKind, ct);
        var locations = await db.SourceLocations.Where(x => true).ToDictionaryAsync(x => x.Id, x => x, ct);
        return facts.Select(x => Map(x, docNames, docKinds, locations)).ToList();
    }

    public async Task<FactDto?> GetAsync(Guid workspaceId, Guid id, CancellationToken ct = default)
    {
        var fact = await db.ExtractedFacts.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct);
        if (fact == null) return null;
        var doc = await db.SourceDocuments.FirstOrDefaultAsync(x => x.Id == fact.SourceDocumentId, ct);
        var loc = fact.SourceLocationId.HasValue ? await db.SourceLocations.FirstOrDefaultAsync(x => x.Id == fact.SourceLocationId.Value, ct) : null;
        var mapDoc = doc == null ? new Dictionary<Guid, string>() : new Dictionary<Guid, string> { { doc.Id, doc.FileName } };
        var mapKind = doc == null ? new Dictionary<Guid, KnowledgeSource>() : new Dictionary<Guid, KnowledgeSource> { { doc.Id, doc.SourceKind } };
        var mapLoc = loc == null ? new Dictionary<Guid, SourceLocation>() : new Dictionary<Guid, SourceLocation> { { loc.Id, loc } };
        return Map(fact, mapDoc, mapKind, mapLoc);
    }

    public async Task<FactDto> PatchAsync(Guid workspaceId, Guid id, FactPatchDto patch, string action, CancellationToken ct = default)
    {
        var fact = await db.ExtractedFacts.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct) ?? throw new KeyNotFoundException("Fact not found.");
        var before = JsonSerializer.Serialize(fact);
        if (patch.RawValue != null) fact.RawValue = patch.RawValue;
        if (patch.NormalizedValue != null) fact.NormalizedValue = patch.NormalizedValue;
        if (patch.ClaimSubject != null) fact.ClaimSubject = patch.ClaimSubject;
        if (patch.ClaimObject != null) fact.ClaimObject = patch.ClaimObject;
        if (patch.ClaimType != null) fact.ClaimType = patch.ClaimType;
        if (patch.Quantity.HasValue) fact.Quantity = patch.Quantity;
        if (patch.QuantityUnit != null) fact.QuantityUnit = patch.QuantityUnit;
        if (patch.EventDate.HasValue) fact.EventDate = patch.EventDate;
        fact.ReviewStatus = ReviewStatus.Edited;
        fact.Reviewer = string.IsNullOrWhiteSpace(patch.Reviewer) ? "reviewer" : patch.Reviewer;
        fact.ReviewedAt = DateTime.UtcNow;
        fact.ReviewReason = patch.Reason ?? string.Empty;
        fact.UpdatedAt = DateTime.UtcNow;
        await InvalidateDependentTraceLinksAsync(workspaceId, fact.Id, $"Supporting fact '{fact.ClaimSubject}' was edited by {fact.Reviewer}: {patch.Reason}", demoteIfConfirmed: true, ct);
        await db.SaveChangesAsync(ct);
        await RecordReviewAsync(workspaceId, fact, action, before, JsonSerializer.Serialize(fact), fact.Reviewer, patch.Reason, ct);
        await audit.AddAsync(workspaceId, $"fact_{action}", fact.Reviewer, $"Edited claim for '{fact.ClaimSubject}' ({action}).", "ExtractedFact", id.ToString(), $"{{\"reason\":\"{Escape(patch.Reason)}\"}}", ct);
        var doc = await db.SourceDocuments.FirstOrDefaultAsync(x => x.Id == fact.SourceDocumentId, ct);
        var loc = fact.SourceLocationId.HasValue ? await db.SourceLocations.FirstOrDefaultAsync(x => x.Id == fact.SourceLocationId.Value, ct) : null;
        var docMap = doc == null ? new Dictionary<Guid, string>() : new Dictionary<Guid, string> { { doc.Id, doc.FileName } };
        var kindMap = doc == null ? new Dictionary<Guid, KnowledgeSource>() : new Dictionary<Guid, KnowledgeSource> { { doc.Id, doc.SourceKind } };
        var locMap = loc == null ? new Dictionary<Guid, SourceLocation>() : new Dictionary<Guid, SourceLocation> { { loc.Id, loc } };
        return Map(fact, docMap, kindMap, locMap);
    }

    public async Task<FactDto> ConfirmAsync(Guid workspaceId, Guid id, ReviewDecisionDto decision, CancellationToken ct = default)
    {
        var fact = await db.ExtractedFacts.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct) ?? throw new KeyNotFoundException("Fact not found.");
        var before = JsonSerializer.Serialize(fact);
        fact.ReviewStatus = ReviewStatus.Confirmed;
        fact.Reviewer = string.IsNullOrWhiteSpace(decision.Reviewer) ? "reviewer" : decision.Reviewer;
        fact.ReviewedAt = DateTime.UtcNow;
        fact.ReviewReason = decision.Notes ?? string.Empty;
        fact.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecordReviewAsync(workspaceId, fact, "confirmed", before, JsonSerializer.Serialize(fact), fact.Reviewer, decision.Notes, ct);
        await audit.AddAsync(workspaceId, "fact_confirmed", fact.Reviewer, $"Confirmed claim for '{fact.ClaimSubject}'.", "ExtractedFact", id.ToString(), $"{{\"notes\":\"{Escape(decision.Notes)}\"}}", ct);
        return await GetAsync(workspaceId, id, ct) ?? throw new InvalidOperationException("Fact missing.");
    }

    public async Task<FactDto> RejectAsync(Guid workspaceId, Guid id, ReviewDecisionDto decision, CancellationToken ct = default)
    {
        var fact = await db.ExtractedFacts.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct) ?? throw new KeyNotFoundException("Fact not found.");
        var before = JsonSerializer.Serialize(fact);
        fact.ReviewStatus = ReviewStatus.Rejected;
        fact.Reviewer = string.IsNullOrWhiteSpace(decision.Reviewer) ? "reviewer" : decision.Reviewer;
        fact.ReviewedAt = DateTime.UtcNow;
        fact.ReviewReason = decision.Notes ?? string.Empty;
        fact.UpdatedAt = DateTime.UtcNow;
        await InvalidateDependentTraceLinksAsync(workspaceId, fact.Id, $"Supporting fact '{fact.ClaimSubject}' was rejected by {fact.Reviewer}: {decision.Notes}", demoteIfConfirmed: true, ct);
        await db.SaveChangesAsync(ct);
        await RecordReviewAsync(workspaceId, fact, "rejected", before, JsonSerializer.Serialize(fact), fact.Reviewer, decision.Notes, ct);
        await audit.AddAsync(workspaceId, "fact_rejected", fact.Reviewer, $"Rejected claim for '{fact.ClaimSubject}'.", "ExtractedFact", id.ToString(), $"{{\"notes\":\"{Escape(decision.Notes)}\"}}", ct);
        return await GetAsync(workspaceId, id, ct) ?? throw new InvalidOperationException("Fact missing.");
    }

    private async Task InvalidateDependentTraceLinksAsync(Guid workspaceId, Guid factId, string reason, bool demoteIfConfirmed, CancellationToken ct)
    {
        var factIdStr = factId.ToString();
        var dependentLinks = await db.TraceLinks
            .Where(x => x.WorkspaceId == workspaceId && !string.IsNullOrEmpty(x.SupportingFactIdsJson) && x.SupportingFactIdsJson.Contains(factIdStr))
            .ToListAsync(ct);

        foreach (var link in dependentLinks)
        {
            link.NeedsReReview = true;
            link.StaleReason = reason;
            link.UpdatedAt = DateTime.UtcNow;
            if (demoteIfConfirmed && link.State == LinkState.Confirmed)
            {
                link.State = LinkState.Candidate;
                link.ConfidenceNote = $"{link.ConfidenceNote} [Demoted to Candidate: {reason}]".Trim();
            }
        }
    }

    public async Task<List<FactHistoryDto>> HistoryAsync(Guid workspaceId, Guid factId, CancellationToken ct = default)
    {
        return await db.FactReviewEvents
            .Where(x => x.WorkspaceId == workspaceId && x.ExtractedFactId == factId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new FactHistoryDto
            {
                Id = x.Id,
                ExtractedFactId = x.ExtractedFactId,
                Action = x.Action,
                PreviousValuesJson = x.PreviousValuesJson,
                NewValuesJson = x.NewValuesJson,
                Reviewer = x.Reviewer,
                Reason = x.Reason,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);
    }

    private async Task RecordReviewAsync(Guid workspaceId, ExtractedFact fact, string action, string previousJson, string newJson, string reviewer, string? reason, CancellationToken ct)
    {
        db.FactReviewEvents.Add(new FactReviewEvent
        {
            WorkspaceId = workspaceId,
            ExtractedFactId = fact.Id,
            Action = action,
            PreviousValuesJson = previousJson,
            NewValuesJson = newJson,
            Reviewer = string.IsNullOrWhiteSpace(reviewer) ? "reviewer" : reviewer,
            Reason = reason ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private static string Escape(string? s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static FactDto Map(ExtractedFact x, Dictionary<Guid, string> docs, Dictionary<Guid, KnowledgeSource> docKinds, Dictionary<Guid, SourceLocation> locs)
    {
        var loc = x.SourceLocationId.HasValue && locs.TryGetValue(x.SourceLocationId.Value, out var l) ? l : null;
        var locationText = loc == null ? (x.SourceLocationId.HasValue ? x.SourceLocationId.Value.ToString() : "not recorded")
            : (!string.IsNullOrWhiteSpace(loc.RawCoordinate) ? loc.RawCoordinate! : "location recorded");
        if (loc != null)
        {
            var parts = new List<string>();
            if (loc.PageNumber.HasValue) parts.Add($"page {loc.PageNumber}");
            if (!string.IsNullOrWhiteSpace(loc.SheetName)) parts.Add($"sheet {loc.SheetName}");
            if (loc.RowNumber.HasValue) parts.Add($"row {loc.RowNumber}");
            if (!string.IsNullOrWhiteSpace(loc.CellReference)) parts.Add(loc.CellReference!);
            if (loc.LineStart.HasValue) parts.Add($"lines {loc.LineStart}-{loc.LineEnd}");
            locationText = parts.Count == 0 ? "location recorded" : string.Join(", ", parts);
        }
        return new FactDto
        {
            Id = x.Id,
            SourceDocumentId = x.SourceDocumentId,
            SourceDocumentName = docs.TryGetValue(x.SourceDocumentId, out var n) ? n : string.Empty,
            SourceLocation = locationText,
            SourceLocationId = x.SourceLocationId,
            FactType = x.FactType,
            ClaimType = x.ClaimType,
            ClaimSubject = x.ClaimSubject,
            ClaimObject = x.ClaimObject,
            SubjectType = x.SubjectType,
            SubjectValue = x.SubjectValue,
            Predicate = x.Predicate,
            ObjectType = x.ObjectType,
            ObjectValue = x.ObjectValue,
            ValidationOutcome = x.ValidationOutcome,
            ExtractionMethod = x.ExtractionMethod,
            Provider = x.Provider,
            ModelId = x.ModelId,
            PromptVersion = x.PromptVersion,
            ReviewReason = x.ReviewReason,
            RawValue = x.RawValue,
            NormalizedValue = x.NormalizedValue,
            EvidenceText = x.EvidenceText,
            SourceQuote = x.SourceQuote,
            SourceMatchMethod = x.SourceMatchMethod,
            Quantity = x.Quantity,
            QuantityUnit = x.QuantityUnit,
            EventDate = x.EventDate,
            ReviewStatus = x.ReviewStatus,
            Reviewer = x.Reviewer,
            ReviewedAt = x.ReviewedAt,
            ConfidenceNote = x.ConfidenceNote,
            Confidence = x.Confidence,
            ProviderMetadata = x.ProviderMetadata,
            SourceKind = docKinds.TryGetValue(x.SourceDocumentId, out var kind) ? kind : KnowledgeSource.ImportedFile
        };
    }
}