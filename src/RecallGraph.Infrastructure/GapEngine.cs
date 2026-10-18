using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class GapEngine(RecallGraphDbContext db)
{
    public async Task<List<GapFindingDto>> ListAsync(Guid workspaceId, Guid? investigationId = null, CancellationToken ct = default)
    {
        var query = db.GapFindings.Where(x => x.WorkspaceId == workspaceId);
        if (investigationId.HasValue)
            query = query.Where(x => x.InvestigationId == investigationId.Value);

        return await query.OrderBy(x => x.Status).ThenByDescending(x => x.UpdatedAt).Select(Map).ToListAsync(ct);
    }

    public async Task<List<GapFindingDto>> RefreshAsync(Guid workspaceId, Guid? investigationId, CancellationToken ct = default)
    {
        var computed = await ComputeAsync(workspaceId, investigationId, ct);
        
        var existingQuery = db.GapFindings.Where(x => x.WorkspaceId == workspaceId);
        if (investigationId.HasValue)
        {
            existingQuery = existingQuery.Where(x => x.InvestigationId == investigationId.Value);
        }
        else
        {
            existingQuery = existingQuery.Where(x => x.InvestigationId == null);
        }
        var existing = await existingQuery.ToListAsync(ct);
        var computedKeys = computed.Select(x => x.StableKey).ToHashSet(StringComparer.Ordinal);

        foreach (var finding in computed.DistinctBy(f => f.StableKey, StringComparer.Ordinal))
        {
            var row = existing.FirstOrDefault(x => x.StableKey == finding.StableKey);
            if (row == null)
            {
                row = new GapFinding { WorkspaceId = workspaceId, StableKey = finding.StableKey };
                db.GapFindings.Add(row);
                existing.Add(row);
            }
            row.InvestigationId = investigationId;
            row.Kind = finding.Kind;
            row.Severity = finding.Severity;
            row.Title = finding.Title;
            row.Explanation = finding.Explanation;
            row.Scope = finding.Scope;
            row.RaisedByCheck = finding.RaisedByCheck;
            row.RelatedEntityType = finding.RelatedEntityType;
            row.RelatedEntityId = finding.RelatedEntityId;
            row.EvidenceJson = finding.EvidenceJson;
            row.Status = "Open";
            row.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var stale in existing.Where(x => !computedKeys.Contains(x.StableKey) && x.Status == "Open"))
        {
            stale.Status = "Resolved";
            stale.UpdatedAt = DateTime.UtcNow;
        }

        // Resolve legacy duplicate rows that share a stable key with an already-updated row.
        foreach (var duplicate in existing.Where(x => x.Status == "Open")
                     .GroupBy(x => x.StableKey, StringComparer.Ordinal)
                     .SelectMany(g => g.Skip(1)))
        {
            duplicate.Status = "Resolved";
            duplicate.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return await ListAsync(workspaceId, investigationId, ct);
    }

    public async Task<List<GapFindingDto>> ComputeAsync(Guid workspaceId, Guid? investigationId, CancellationToken ct = default)
    {
        var findings = new List<GapFindingDto>();
        var keyPrefix = investigationId.HasValue ? $"inv:{investigationId.Value:N}:" : "";

        var scopedDocIds = await db.SourceDocuments
            .Where(x => x.WorkspaceId == workspaceId && (!investigationId.HasValue || x.InvestigationId == investigationId.Value))
            .Select(x => x.Id)
            .ToListAsync(ct);

        var gapLinksQuery = db.TraceLinks.Where(x => x.WorkspaceId == workspaceId && x.State == LinkState.Gap);
        if (investigationId.HasValue)
            gapLinksQuery = gapLinksQuery.Where(x => x.InvestigationId == investigationId.Value);
        var gapLinks = await gapLinksQuery.ToListAsync(ct);

        foreach (var link in gapLinks)
        {
            findings.Add(new GapFindingDto
            {
                InvestigationId = investigationId,
                Kind = "missing-downstream-evidence",
                Severity = "high",
                Title = "Dispatch evidence not located",
                Explanation = link.EvidenceText,
                Scope = $"{link.FromType} {link.FromId}",
                RaisedByCheck = "trace-link-gap",
                RelatedEntityType = link.ToType,
                RelatedEntityId = link.ToId,
                EvidenceJson = JsonSerializer.Serialize(new { linkId = link.Id, link.EvidenceText, link.Quantity, link.QuantityUnit, link.ConfidenceNote }),
                Status = "Open",
                StableKey = $"{keyPrefix}trace-link-gap:{link.Id}"
            });
        }

        var linksQuery = db.TraceLinks.Where(x => x.WorkspaceId == workspaceId);
        if (investigationId.HasValue)
            linksQuery = linksQuery.Where(x => x.InvestigationId == investigationId.Value);
        var links = await linksQuery.ToListAsync(ct);

        var rawLots = await db.RawMaterialLots.Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct);
        var batches = await db.ProductionBatches.Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct);

        if (investigationId.HasValue)
        {
            var relevantLotCodes = links.Select(l => l.FromId).Concat(links.Select(l => l.ToId)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var inv = await db.Investigations.FirstOrDefaultAsync(x => x.Id == investigationId.Value, ct);
            if (!string.IsNullOrWhiteSpace(inv?.SuspectLotId))
                relevantLotCodes.Add(inv.SuspectLotId.Trim());

            if (relevantLotCodes.Count > 0)
            {
                rawLots = rawLots.Where(r => relevantLotCodes.Contains(r.Code)).ToList();
                batches = batches.Where(b => relevantLotCodes.Contains(b.Code)).ToList();
            }
        }

        foreach (var lot in rawLots)
        {
            var received = links.Where(x => x.ToType == "raw-lot" && x.ToId == lot.Code && x.LinkKind.Contains("supplier_delivery", StringComparison.OrdinalIgnoreCase)).ToList();
            var consumed = links.Where(x => x.FromType == "raw-lot" && x.FromId == lot.Code && x.LinkKind.Contains("raw_to_batch", StringComparison.OrdinalIgnoreCase)).ToList();
            var receivedTotal = received.Where(x => x.Quantity.HasValue).Sum(x => x.Quantity!.Value);
            var consumedTotal = consumed.Where(x => x.Quantity.HasValue).Sum(x => x.Quantity!.Value);
            if (received.Count > 0 && consumed.Count > 0 && (receivedTotal > 0 || consumedTotal > 0))
            {
                var tolerance = 0.01m * Math.Max(receivedTotal, 1m);
                if (Math.Abs(receivedTotal - consumedTotal) > tolerance)
                {
                    var recFormatted = receivedTotal.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
                    var conFormatted = consumedTotal.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
                    findings.Add(new GapFindingDto
                    {
                        InvestigationId = investigationId,
                        Kind = "quantity-mismatch",
                        Severity = "high",
                        Title = "Received and consumed quantities differ",
                        Explanation = $"Raw lot {lot.Code} received {recFormatted} {received.FirstOrDefault()?.QuantityUnit} and consumed {conFormatted} {consumed.FirstOrDefault()?.QuantityUnit} according to the persisted evidence. This may indicate missing issue, waste or rework records.",
                        Scope = lot.Code,
                        RaisedByCheck = "received-vs-consumed-quantity",
                        RelatedEntityType = "raw-lot",
                        RelatedEntityId = lot.Code,
                        EvidenceJson = JsonSerializer.Serialize(new { linkIds = received.Concat(consumed).Select(x => x.Id), receivedTotal, consumedTotal }),
                        Status = "Open",
                        StableKey = $"{keyPrefix}quantity-mismatch:{lot.Code}"
                    });
                }
            }
        }

        foreach (var batch in batches)
        {
            var hasInput = links.Any(x => x.ToType == "production-batch" && x.ToId == batch.Code);
            if (!hasInput)
            {
                findings.Add(new GapFindingDto
                {
                    InvestigationId = investigationId,
                    Kind = "missing-upstream-evidence",
                    Severity = "medium",
                    Title = "No upstream input link recorded",
                    Explanation = $"Production batch {batch.Code} has no persisted incoming lot or rework relationship in this evidence set. Absence in this evidence set does not prove the input did not exist.",
                    Scope = batch.Code,
                    RaisedByCheck = "batch-input-link",
                    RelatedEntityType = "production-batch",
                    RelatedEntityId = batch.Code,
                    EvidenceJson = "[]",
                    Status = "Open",
                    StableKey = $"{keyPrefix}missing-batch-input:{batch.Code}"
                });
            }
        }

        var factsQuery = db.ExtractedFacts.Where(x => x.WorkspaceId == workspaceId && scopedDocIds.Contains(x.SourceDocumentId));

        var evidenceCodes = (await factsQuery.Select(x => x.ClaimSubject).ToListAsync(ct))
            .Concat(await factsQuery.Select(x => x.NormalizedValue).ToListAsync(ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var linkCounts = links.SelectMany(l => new[] { l.FromId, l.ToId })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var registeredLots = rawLots.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenAmbiguousPairs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var lot in rawLots)
        {
            foreach (var code in evidenceCodes.Where(c => IdentifierNormalizer.IsNearMatch(c, lot.Code)))
            {
                // One finding per unordered identifier pair: when both sides are registered lots the same
                // ambiguity would otherwise surface twice (once per direction) and duplicates would multiply
                // for every duplicate registry row.
                var pairKey = string.CompareOrdinal(lot.Code, code) <= 0 ? $"{lot.Code}|{code}" : $"{code}|{lot.Code}";
                if (!seenAmbiguousPairs.Add(pairKey)) continue;

                var lotCount = linkCounts.TryGetValue(lot.Code, out var lc) ? lc : 0;
                var codeCount = linkCounts.TryGetValue(code, out var cc) ? cc : 0;
                var preferCode = registeredLots.Contains(code) &&
                    (codeCount > lotCount || (codeCount == lotCount && string.CompareOrdinal(code, lot.Code) < 0));
                var canonical = preferCode ? code : lot.Code;
                var variant = preferCode ? lot.Code : code;

                findings.Add(new GapFindingDto
                {
                    InvestigationId = investigationId,
                    Kind = "ambiguous-identifier",
                    Severity = "medium",
                    Title = "Ambiguous identifier needs review",
                    Explanation = $"Evidence uses '{variant}', which resembles canonical lot '{canonical}' but is not identically equal. This remains a candidate only and must be confirmed or rejected by a reviewer.",
                    Scope = canonical,
                    RaisedByCheck = "near-match-identifier",
                    RelatedEntityType = "raw-lot",
                    RelatedEntityId = canonical,
                    EvidenceJson = JsonSerializer.Serialize(new { canonical, evidenceVariant = variant }),
                    Status = "Open",
                    StableKey = $"{keyPrefix}ambiguous-identifier:{canonical}:{variant}"
                });
            }
        }

        var unreviewed = await factsQuery.CountAsync(x => x.ReviewStatus == ReviewStatus.Unreviewed, ct);
        if (unreviewed > 0)
        {
            findings.Add(new GapFindingDto
            {
                InvestigationId = investigationId,
                Kind = "unreviewed-candidates",
                Severity = "low",
                Title = "Candidate facts still need review",
                Explanation = $"{unreviewed} extracted candidate facts are still unreviewed in this scope. Candidate facts are not confirmed evidence until a human reviews them.",
                Scope = investigationId.HasValue ? $"investigation:{investigationId.Value:N}" : "workspace",
                RaisedByCheck = "fact-review-status",
                RelatedEntityType = investigationId.HasValue ? "investigation" : "workspace",
                RelatedEntityId = investigationId?.ToString() ?? workspaceId.ToString(),
                EvidenceJson = JsonSerializer.Serialize(new { unreviewed }),
                Status = "Open",
                StableKey = $"{keyPrefix}unreviewed-candidates"
            });
        }

        return findings.DistinctBy(f => f.StableKey, StringComparer.Ordinal).ToList();
    }

    private static System.Linq.Expressions.Expression<Func<GapFinding, GapFindingDto>> Map => x => new GapFindingDto
    {
        Id = x.Id,
        StableKey = x.StableKey,
        InvestigationId = x.InvestigationId,
        TraceRunId = x.TraceRunId,
        Kind = x.Kind,
        Severity = x.Severity,
        Title = x.Title,
        Explanation = x.Explanation,
        Scope = x.Scope,
        RaisedByCheck = x.RaisedByCheck,
        RelatedEntityType = x.RelatedEntityType,
        RelatedEntityId = x.RelatedEntityId,
        EvidenceJson = x.EvidenceJson,
        Status = x.Status,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };
}
