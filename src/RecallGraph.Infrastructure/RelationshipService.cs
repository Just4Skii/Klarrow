using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class RelationshipService(RecallGraphDbContext db, AuditService audit)
{
    private static readonly Regex RawLotRegex = new(@"\b(RM-[0-9A-Z-]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BatchRegex = new(@"\b(PB-[0-9A-Z-]+|PB\d{4,8})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FinishedLotRegex = new(@"\b(FL-[0-9A-Z-]+|FL\d{5,8})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<List<RelationshipProposalDto>> ListAsync(Guid workspaceId, Guid? investigationId = null, CancellationToken ct = default)
    {
        var query = db.TraceLinks.Where(x => x.WorkspaceId == workspaceId);
        if (investigationId.HasValue)
            query = query.Where(x => x.InvestigationId == investigationId.Value);

        var links = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var statuses = await LoadSupportingFactStatusesAsync(workspaceId, links, ct);
        return links.Select(l => Map(l, statuses)).ToList();
    }

    public async Task<GenerateRelationshipsResultDto> GenerateProposalsAsync(Guid workspaceId, Guid? investigationId, CancellationToken ct = default)
    {
        var aliases = await db.EntityAliases.Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct);
        var aliasMap = aliases.ToDictionary(a => a.AliasValue.ToUpperInvariant(), a => a.CanonicalValue, StringComparer.OrdinalIgnoreCase);

        string ResolveCanonical(string code) =>
            aliasMap.TryGetValue(code.Trim().ToUpperInvariant(), out var canon) ? canon : code.Trim();

        var existingLinks = await db.TraceLinks
            .Where(x => x.WorkspaceId == workspaceId && (investigationId == null || x.InvestigationId == investigationId))
            .ToListAsync(ct);

        var existingByStableKey = existingLinks
            .Where(x => !string.IsNullOrWhiteSpace(x.StableKey))
            .ToDictionary(x => x.StableKey, x => x, StringComparer.OrdinalIgnoreCase);

        var docsQuery = db.SourceDocuments.Where(x => x.WorkspaceId == workspaceId);
        if (investigationId.HasValue)
            docsQuery = docsQuery.Where(x => x.InvestigationId == investigationId.Value);
        var docs = await docsQuery.ToListAsync(ct);
        var docMap = docs.ToDictionary(d => d.Id, d => d);
        var docIds = docs.Select(d => d.Id).ToList();

        var facts = await db.ExtractedFacts
            .Where(x => x.WorkspaceId == workspaceId && docIds.Contains(x.SourceDocumentId) && x.ReviewStatus != ReviewStatus.Rejected)
            .ToListAsync(ct);

        var locations = await db.SourceLocations
            .Where(x => docIds.Contains(x.SourceDocumentId))
            .ToDictionaryAsync(x => x.Id, x => x, ct);

        var generatedCount = 0;
        var existingCount = 0;
        var existingLinkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedUngrounded = 0;
        var ensuredEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fact in facts)
        {
            docMap.TryGetValue(fact.SourceDocumentId, out var doc);
            var docName = doc?.FileName ?? "Document";
            if (fact.SourceLocationId == null || !locations.TryGetValue(fact.SourceLocationId.Value, out var loc))
            {
                skippedUngrounded++;
                continue;
            }
            var locStr = string.IsNullOrWhiteSpace(loc.RawCoordinate) ? "location not recorded" : loc.RawCoordinate;
            var text = fact.EvidenceText ?? fact.RawValue ?? string.Empty;

            var candidates = ExtractCandidateRelations(fact, text, docName, ResolveCanonical);

            foreach (var cand in candidates)
            {
                var stableKey = $"rel:{investigationId?.ToString("N") ?? "ws"}:{cand.FromType}:{cand.FromId}:{cand.ToType}:{cand.ToId}:{cand.LinkKind}";

                if (existingByStableKey.TryGetValue(stableKey, out var existing))
                {
                    existingCount++;
                    existingLinkKeys.Add(stableKey);
                    // If marked for re-review or had missing quantity, update non-overwriting attributes
                    if (existing.NeedsReReview && fact.ReviewStatus == ReviewStatus.Confirmed)
                    {
                        existing.NeedsReReview = false;
                        existing.StaleReason = string.Empty;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    continue;
                }

                var sourceRef = new[]
                {
                    new
                    {
                        documentId = fact.SourceDocumentId,
                        fileName = docName,
                        locator = locStr,
                        evidence = text.Length > 200 ? text[..200] : text
                    }
                };

                var newLink = new TraceLink
                {
                    WorkspaceId = workspaceId,
                    InvestigationId = investigationId,
                    FromType = cand.FromType,
                    FromId = cand.FromId,
                    ToType = cand.ToType,
                    ToId = cand.ToId,
                    LinkKind = cand.LinkKind,
                    State = LinkState.Candidate,
                    Quantity = cand.Quantity ?? fact.Quantity,
                    QuantityUnit = cand.QuantityUnit ?? fact.QuantityUnit ?? string.Empty,
                    EventDate = cand.EventDate ?? fact.EventDate,
                    EvidenceText = $"Extracted from {docName} ({locStr}): {cand.Explanation}",
                    SourceReferencesJson = JsonSerializer.Serialize(sourceRef),
                    SupportingFactIdsJson = JsonSerializer.Serialize(new[] { fact.Id }),
                    ConfidenceNote = "Generated from source document evidence; human review required before confirming traversal.",
                    DetectionMethod = fact.ExtractionMethod,
                    Origin = "generated-from-evidence",
                    StableKey = stableKey,
                    Reason = cand.Explanation,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.TraceLinks.Add(newLink);
                existingByStableKey[stableKey] = newLink;
                generatedCount++;

                // Ensure corresponding canonical entities exist in registry so traversal won't throw missing entity.
                // Deduped per run: the AnyAsync existence check cannot see entities added earlier in the same
                // unsaved transaction, so duplicated codes within one generation run must be skipped here.
                if (ensuredEntities.Add($"{cand.FromType}|{cand.FromId}"))
                    await EnsureEntityExistsAsync(workspaceId, cand.FromType, cand.FromId, docName, ct);
                if (ensuredEntities.Add($"{cand.ToType}|{cand.ToId}"))
                    await EnsureEntityExistsAsync(workspaceId, cand.ToType, cand.ToId, docName, ct);
            }
        }

        if (generatedCount > 0 || skippedUngrounded > 0)
        {
            await db.SaveChangesAsync(ct);
            await audit.AddAsync(workspaceId, "relationships_generated", "system",
                $"Generated {generatedCount} relationship candidate(s) across {docs.Count} evidence document(s); {skippedUngrounded} ungrounded claim(s) excluded for missing verified source location.",
                "TraceLink", investigationId?.ToString() ?? "workspace",
                JsonSerializer.Serialize(new { generatedCount, existingCount, skippedUngrounded }), ct);
        }

        var allProposals = await ListAsync(workspaceId, investigationId, ct);
        return new GenerateRelationshipsResultDto
        {
            GeneratedCount = generatedCount,
            ExistingCount = existingCount,
            ExistingLinkCount = existingLinkKeys.Count,
            ExistingFactKeyHitCount = existingCount,
            SkippedUngroundedCount = skippedUngrounded,
            Proposals = allProposals
        };
    }

    public async Task<RelationshipProposalDto> ConfirmAsync(Guid workspaceId, Guid linkId, ReviewDecisionDto decision, CancellationToken ct = default)
    {
        var link = await db.TraceLinks.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == linkId, ct)
            ?? throw new KeyNotFoundException("Relationship proposal not found.");

        link.State = LinkState.Confirmed;
        link.ReviewedBy = string.IsNullOrWhiteSpace(decision.Reviewer) ? "reviewer" : decision.Reviewer;
        link.ReviewedAt = DateTime.UtcNow;
        link.ReviewReason = decision.Notes ?? string.Empty;
        link.NeedsReReview = false;
        link.StaleReason = string.Empty;
        link.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await audit.AddAsync(workspaceId, "relationship_confirmed", link.ReviewedBy,
            $"Confirmed relationship '{link.FromType} {link.FromId}' -> '{link.ToType} {link.ToId}' ({link.LinkKind}). Relationship confirmation does not imply review of every supporting fact.",
            "TraceLink", link.Id.ToString(),
            JsonSerializer.Serialize(new { link.FromId, link.ToId, link.LinkKind, decision.Notes }), ct);

        var confirmedStatuses = await LoadSupportingFactStatusesAsync(workspaceId, new[] { link }, ct);
        return Map(link, confirmedStatuses);
    }

    public async Task<RelationshipProposalDto> RejectAsync(Guid workspaceId, Guid linkId, ReviewDecisionDto decision, CancellationToken ct = default)
    {
        var link = await db.TraceLinks.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == linkId, ct)
            ?? throw new KeyNotFoundException("Relationship proposal not found.");

        link.State = LinkState.Rejected;
        link.ReviewedBy = string.IsNullOrWhiteSpace(decision.Reviewer) ? "reviewer" : decision.Reviewer;
        link.ReviewedAt = DateTime.UtcNow;
        link.ReviewReason = decision.Notes ?? string.Empty;
        link.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await audit.AddAsync(workspaceId, "relationship_rejected", link.ReviewedBy,
            $"Rejected relationship '{link.FromType} {link.FromId}' -> '{link.ToType} {link.ToId}' ({link.LinkKind}).",
            "TraceLink", link.Id.ToString(),
            JsonSerializer.Serialize(new { link.FromId, link.ToId, link.LinkKind, decision.Notes }), ct);

        await RemoveOrphanedAutoRegisteredEntitiesAsync(workspaceId, link, ct);
        var rejectedStatuses = await LoadSupportingFactStatusesAsync(workspaceId, new[] { link }, ct);
        return Map(link, rejectedStatuses);
    }

    private sealed record ExtractedRelationCandidate(
        string FromType,
        string FromId,
        string ToType,
        string ToId,
        string LinkKind,
        decimal? Quantity,
        string? QuantityUnit,
        DateTime? EventDate,
        string Explanation);

    private List<ExtractedRelationCandidate> ExtractCandidateRelations(
        ExtractedFact fact,
        string line,
        string docName,
        Func<string, string> resolveCanonical)
    {
        var results = new List<ExtractedRelationCandidate>();
        var clean = line.Trim();
        if (string.IsNullOrWhiteSpace(clean)) return results;

        var rawLotMatch = RawLotRegex.Match(clean);
        var batchMatch = BatchRegex.Match(clean);
        var finishedLotMatch = FinishedLotRegex.Match(clean);

        // Explicit structured parser or AI-relation claims
        var q = fact.Quantity;
        var qu = fact.QuantityUnit;
        if (!q.HasValue)
        {
            var qMatch = Regex.Match(clean, @"(?<![\d-])(\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)\s*(?:,\s*)?(kg|g|units?|pcs|l|litres?)\b", RegexOptions.IgnoreCase);
            if (qMatch.Success)
            {
                q = IdentifierNormalizer.ParseQuantity(qMatch.Groups[1].Value);
                qu = qMatch.Groups[2].Value.ToLowerInvariant();
            }
        }

        if (!string.IsNullOrWhiteSpace(fact.SubjectType) && !string.IsNullOrWhiteSpace(fact.ObjectType)
            && !string.IsNullOrWhiteSpace(fact.SubjectValue) && !string.IsNullOrWhiteSpace(fact.ObjectValue))
        {
            var fromType = MapEntityType(fact.SubjectType);
            var toType = MapEntityType(fact.ObjectType);
            var fromId = resolveCanonical(fact.SubjectValue);
            var toId = resolveCanonical(fact.ObjectValue);
            var kind = !string.IsNullOrWhiteSpace(fact.Predicate) ? fact.Predicate : InferLinkKind(fromType, toType);
            results.Add(new ExtractedRelationCandidate(fromType, fromId, toType, toId, kind, q, qu, fact.EventDate,
                $"{fromType} '{fromId}' linked to {toType} '{toId}' via structured claim"));
            return results;
        }

        // 1. Raw Lot -> Production Batch (consumed_input / raw_to_batch)
        if (rawLotMatch.Success && batchMatch.Success)
        {
            var rawLot = resolveCanonical(rawLotMatch.Groups[1].Value);
            var batch = resolveCanonical(batchMatch.Groups[1].Value);
            results.Add(new ExtractedRelationCandidate("raw-lot", rawLot, "production-batch", batch, "raw_to_batch",
                q, qu, fact.EventDate,
                $"Batch sheet / ledger links raw material lot '{rawLot}' as input to production batch '{batch}'."));
        }

        // 2. Production Batch -> Finished Lot (batch_to_finished / produced)
        if (batchMatch.Success && finishedLotMatch.Success)
        {
            var batch = resolveCanonical(batchMatch.Groups[1].Value);
            var finishedLot = resolveCanonical(finishedLotMatch.Groups[1].Value);
            results.Add(new ExtractedRelationCandidate("production-batch", batch, "finished-lot", finishedLot, "batch_to_finished",
                q, qu, fact.EventDate,
                $"Packaging / production log links batch '{batch}' to finished lot '{finishedLot}'."));
        }

        // 3. Finished Lot -> Customer (finished_to_dispatch)
        if (finishedLotMatch.Success && (fact.FactType == FactType.Dispatch || fact.FactType == FactType.Invoice || docName.Contains("dispatch", StringComparison.OrdinalIgnoreCase) || clean.Contains("dispatch", StringComparison.OrdinalIgnoreCase) || clean.Contains("INV-", StringComparison.OrdinalIgnoreCase)))
        {
            var finishedLot = resolveCanonical(finishedLotMatch.Groups[1].Value);
            var customer = ExtractCustomerFromLine(clean);
            if (!string.IsNullOrWhiteSpace(customer))
            {
                results.Add(new ExtractedRelationCandidate("finished-lot", finishedLot, "customer", customer, "finished_to_dispatch",
                    q, qu, fact.EventDate,
                    $"Dispatch record links finished product lot '{finishedLot}' to customer '{customer}'."));
            }
        }

        // 4. Supplier Delivery -> Raw Material Lot (supplier_delivery)
        if (rawLotMatch.Success && (fact.FactType == FactType.SupplierDelivery || fact.FactType == FactType.GoodsReceipt || docName.Contains("delivery", StringComparison.OrdinalIgnoreCase) || docName.Contains("grn", StringComparison.OrdinalIgnoreCase) || clean.Contains("DN-", StringComparison.OrdinalIgnoreCase) || clean.Contains("GRN-", StringComparison.OrdinalIgnoreCase)))
        {
            var rawLot = resolveCanonical(rawLotMatch.Groups[1].Value);
            var supplier = ExtractSupplierFromLine(clean);
            if (!string.IsNullOrWhiteSpace(supplier))
            {
                results.Add(new ExtractedRelationCandidate("supplier", supplier, "raw-lot", rawLot, "supplier_delivery",
                    q, qu, fact.EventDate,
                    $"Delivery document links supplier '{supplier}' to raw material lot '{rawLot}'."));
            }
        }

        // 5. Batch -> Batch (rework_input)
        if (clean.Contains("rework", StringComparison.OrdinalIgnoreCase))
        {
            var matches = BatchRegex.Matches(clean);
            if (matches.Count >= 2)
            {
                var sourceBatch = resolveCanonical(matches[0].Groups[1].Value);
                var targetBatch = resolveCanonical(matches[1].Groups[1].Value);
                if (!string.Equals(sourceBatch, targetBatch, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new ExtractedRelationCandidate("production-batch", sourceBatch, "production-batch", targetBatch, "rework_input",
                        q, qu, fact.EventDate,
                        $"Rework record links source batch '{sourceBatch}' into rework batch '{targetBatch}'."));
                }
            }
        }

        return results;
    }

    private static string ExtractCustomerFromLine(string line)
    {
        var known = new[]
        {
            "Tuckshop Arendse (Benoni)",
            "Groot Pretoria Foods (Pty) Ltd",
            "Cafe Kapabela",
            "Mall Mart Centurion",
            "Dollar Tree Stores Inc.",
            "Weis Markets Inc.",
            "Schnuck Markets Inc.",
            "Amazon.com Services LLC"
        };
        foreach (var k in known)
        {
            if (line.Contains(k, StringComparison.OrdinalIgnoreCase)) return k;
        }

        var parts = line.Split([',', '\t', '|', ';']).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        foreach (var p in parts)
        {
            if (p.EndsWith("Inc.", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith("LLC", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith("Ltd", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith("Corp", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Stores", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Markets", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Tuckshop", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Wholesale", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Foods", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Retail", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Cafe ", StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }
        foreach (var p in parts)
        {
            if (!FinishedLotRegex.IsMatch(p) && !p.StartsWith("DSP-", StringComparison.OrdinalIgnoreCase) && !p.Equals("kg", StringComparison.OrdinalIgnoreCase) && !decimal.TryParse(p, out _) && p.Length >= 4 && !p.Contains("finished", StringComparison.OrdinalIgnoreCase) && !p.Contains("customer", StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }
        return string.Empty;
    }

    private static string ExtractSupplierFromLine(string line)
    {
        var known = new[]
        {
            "Negasmart S.A. (Ecuador)",
            "Negasmart S.A.",
            "Carlos Aguilera Processing",
            "Northern Wheat Supplies",
            "Sri Lanka Direct Spice Mill"
        };
        foreach (var k in known)
        {
            if (line.Contains(k, StringComparison.OrdinalIgnoreCase)) return k;
        }
        var parts = line.Split([',', '\t', '|', ';']).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        foreach (var p in parts)
        {
            if (p.Contains("Supplies", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("S.A.", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Processing", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Imports", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Farms", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Trading", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Growers", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Mill", StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }
        foreach (var p in parts)
        {
            if (!RawLotRegex.IsMatch(p) && !p.StartsWith("DEL-", StringComparison.OrdinalIgnoreCase) && !p.Equals("kg", StringComparison.OrdinalIgnoreCase) && !decimal.TryParse(p, out _) && p.Length >= 4 && !p.Contains("supplier", StringComparison.OrdinalIgnoreCase) && !p.Contains("delivery", StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }
        return string.Empty;
    }

    private static string MapEntityType(string type) => type.ToLowerInvariant().Trim() switch
    {
        "supplier" => "supplier",
        "raw_lot" or "raw-lot" or "rawmateriallot" => "raw-lot",
        "production_batch" or "production-batch" or "productionbatch" or "batch" => "production-batch",
        "finished_lot" or "finished-lot" or "finishedlot" => "finished-lot",
        "customer" => "customer",
        _ => type.ToLowerInvariant().Trim()
    };

    private static string InferLinkKind(string fromType, string toType) => (fromType, toType) switch
    {
        ("supplier", "raw-lot") => "supplier_delivery",
        ("raw-lot", "production-batch") => "raw_to_batch",
        ("production-batch", "production-batch") => "rework_input",
        ("production-batch", "finished-lot") => "batch_to_finished",
        ("finished-lot", "customer") => "finished_to_dispatch",
        _ => $"{fromType}_to_{toType}"
    };

    private async Task EnsureEntityExistsAsync(Guid workspaceId, string type, string id, string notes, CancellationToken ct)
    {
        switch (type.ToLowerInvariant())
        {
            case "raw-lot":
                if (!await db.RawMaterialLots.AnyAsync(x => x.WorkspaceId == workspaceId && x.Code == id, ct))
                {
                    db.RawMaterialLots.Add(new RawMaterialLot
                    {
                        WorkspaceId = workspaceId,
                        Code = id,
                        ProductName = $"Raw material {id}",
                        Status = "active",
                        Notes = $"Auto-registered from evidence in {notes}"
                    });
                }
                break;
            case "production-batch":
                if (!await db.ProductionBatches.AnyAsync(x => x.WorkspaceId == workspaceId && x.Code == id, ct))
                {
                    db.ProductionBatches.Add(new ProductionBatch
                    {
                        WorkspaceId = workspaceId,
                        Code = id,
                        ProductName = $"Batch {id}",
                        Line = "1",
                        Notes = $"Auto-registered from evidence in {notes}"
                    });
                }
                break;
            case "finished-lot":
                if (!await db.FinishedLots.AnyAsync(x => x.WorkspaceId == workspaceId && x.Code == id, ct))
                {
                    db.FinishedLots.Add(new FinishedLot
                    {
                        WorkspaceId = workspaceId,
                        Code = id,
                        ProductName = $"Finished lot {id}",
                        Status = "available",
                        Notes = $"Auto-registered from evidence in {notes}"
                    });
                }
                break;
            case "supplier":
                if (!await db.Suppliers.AnyAsync(x => x.WorkspaceId == workspaceId && x.Name == id, ct))
                {
                    db.Suppliers.Add(new Supplier
                    {
                        WorkspaceId = workspaceId,
                        Name = id,
                        Alias = id,
                        Location = "Registered from evidence"
                    });
                }
                break;
            case "customer":
                if (!await db.Customers.AnyAsync(x => x.WorkspaceId == workspaceId && x.Name == id, ct))
                {
                    db.Customers.Add(new Customer
                    {
                        WorkspaceId = workspaceId,
                        Name = id,
                        Type = "Customer",
                        Location = "Registered from evidence"
                    });
                }
                break;
        }
    }

    private async Task<Dictionary<Guid, ReviewStatus>> LoadSupportingFactStatusesAsync(
        Guid workspaceId,
        IEnumerable<TraceLink> links,
        CancellationToken ct)
    {
        var ids = links.SelectMany(ParseSupportingFactIds).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, ReviewStatus>();
        return await db.ExtractedFacts
            .Where(x => x.WorkspaceId == workspaceId && ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ReviewStatus, ct);
    }

    private static List<Guid> ParseSupportingFactIds(TraceLink link)
    {
        if (string.IsNullOrWhiteSpace(link.SupportingFactIdsJson)) return [];
        try { return JsonSerializer.Deserialize<List<Guid>>(link.SupportingFactIdsJson) ?? []; }
        catch { return []; }
    }

    private async Task RemoveOrphanedAutoRegisteredEntitiesAsync(Guid workspaceId, TraceLink rejected, CancellationToken ct)
    {
        foreach (var (type, id) in new[] { (rejected.FromType, rejected.FromId), (rejected.ToType, rejected.ToId) })
        {
            if (string.IsNullOrWhiteSpace(id)) continue;

            var referencedElsewhere = await db.TraceLinks.AnyAsync(x =>
                x.WorkspaceId == workspaceId &&
                x.Id != rejected.Id &&
                x.State != LinkState.Rejected &&
                ((x.FromType == type && x.FromId == id) || (x.ToType == type && x.ToId == id)), ct);
            if (referencedElsewhere) continue;

            switch (type.ToLowerInvariant())
            {
                case "customer":
                    var customer = await db.Customers.FirstOrDefaultAsync(x =>
                        x.WorkspaceId == workspaceId && x.Name == id && x.Location == "Registered from evidence", ct);
                    if (customer != null) db.Customers.Remove(customer);
                    break;
                case "supplier":
                    var supplier = await db.Suppliers.FirstOrDefaultAsync(x =>
                        x.WorkspaceId == workspaceId && x.Name == id && x.Location == "Registered from evidence", ct);
                    if (supplier != null) db.Suppliers.Remove(supplier);
                    break;
                case "raw-lot":
                    var raw = await db.RawMaterialLots.FirstOrDefaultAsync(x =>
                        x.WorkspaceId == workspaceId && x.Code == id && x.Notes.StartsWith("Auto-registered from evidence"), ct);
                    if (raw != null) db.RawMaterialLots.Remove(raw);
                    break;
                case "production-batch":
                    var batch = await db.ProductionBatches.FirstOrDefaultAsync(x =>
                        x.WorkspaceId == workspaceId && x.Code == id && x.Notes.StartsWith("Auto-registered from evidence"), ct);
                    if (batch != null) db.ProductionBatches.Remove(batch);
                    break;
                case "finished-lot":
                    var finished = await db.FinishedLots.FirstOrDefaultAsync(x =>
                        x.WorkspaceId == workspaceId && x.Code == id && x.Notes.StartsWith("Auto-registered from evidence"), ct);
                    if (finished != null) db.FinishedLots.Remove(finished);
                    break;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static RelationshipProposalDto Map(TraceLink l, Dictionary<Guid, ReviewStatus>? factStatuses = null)
    {
        List<Guid> supportingFactIds = ParseSupportingFactIds(l);
        factStatuses ??= new Dictionary<Guid, ReviewStatus>();
        var unreviewedSupportingFactCount = supportingFactIds.Count(id =>
            !factStatuses.TryGetValue(id, out var status) || status is ReviewStatus.Unreviewed or ReviewStatus.Rejected);

        return new RelationshipProposalDto
        {
            Id = l.Id,
            WorkspaceId = l.WorkspaceId,
            InvestigationId = l.InvestigationId,
            FromType = l.FromType,
            FromId = l.FromId,
            ToType = l.ToType,
            ToId = l.ToId,
            LinkKind = l.LinkKind,
            State = l.State,
            Quantity = l.Quantity,
            QuantityUnit = l.QuantityUnit,
            EventDate = l.EventDate,
            EvidenceText = l.EvidenceText,
            SourceReferences = l.SourceReferencesJson,
            ConfidenceNote = l.ConfidenceNote,
            DetectionMethod = l.DetectionMethod,
            Origin = l.Origin,
            Reason = l.Reason,
            ReviewedBy = l.ReviewedBy,
            ReviewedAt = l.ReviewedAt,
            ReviewReason = l.ReviewReason,
            NeedsReReview = l.NeedsReReview,
            Superseded = l.Superseded,
            StaleReason = l.StaleReason,
            SupportingFactIds = supportingFactIds,
            SupportingFactsReviewed = supportingFactIds.Count > 0 && unreviewedSupportingFactCount == 0,
            UnreviewedSupportingFactCount = unreviewedSupportingFactCount,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        };
    }
}
