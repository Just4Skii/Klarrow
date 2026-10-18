using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class TraceService(RecallGraphDbContext db, AuditService audit, GapEngine gaps)
{
    public async Task<List<LotOptionDto>> ListLotsAsync(Guid workspaceId, string? query, CancellationToken ct = default)
    {
        var raws = await db.RawMaterialLots.Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct);
        var finished = await db.FinishedLots.Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct);
        var batches = await db.ProductionBatches.Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct);
        
        var investigations = await db.Investigations
            .Where(x => x.WorkspaceId == workspaceId && !string.IsNullOrWhiteSpace(x.SuspectLotId))
            .ToListAsync(ct);

        var suspectLotCodes = investigations
            .Select(x => x.SuspectLotId.Trim())
            .ToList();

        var evidenceLotCodes = (await db.ExtractedFacts.Where(x => x.WorkspaceId == workspaceId).Select(x => x.ClaimSubject).ToListAsync(ct))
            .Concat(await db.ExtractedFacts.Where(x => x.WorkspaceId == workspaceId).Select(x => x.NormalizedValue).ToListAsync(ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var allRawCodes = raws.Select(x => x.Code).Concat(evidenceLotCodes).Distinct().ToList();
        var allFinishedCodes = finished.Select(x => x.Code).ToList();
        var result = new List<LotOptionDto>();

        foreach (var r in raws)
        {
            var isSuspect = suspectLotCodes.Any(s => string.Equals(s, r.Code, StringComparison.OrdinalIgnoreCase));
            var near = allRawCodes.Where(c => IdentifierNormalizer.IsNearMatch(c, r.Code)).ToList();
            result.Add(new LotOptionDto { Kind = "raw-lot", Id = r.Code, Code = r.Code, Product = r.ProductName, IsSuspect = isSuspect, NearMatches = near });
        }
        foreach (var f in finished)
        {
            var isSuspect = suspectLotCodes.Any(s => string.Equals(s, f.Code, StringComparison.OrdinalIgnoreCase));
            var near = allFinishedCodes.Where(c => IdentifierNormalizer.IsNearMatch(c, f.Code)).ToList();
            result.Add(new LotOptionDto { Kind = "finished-lot", Id = f.Code, Code = f.Code, Product = f.ProductName, IsSuspect = isSuspect, NearMatches = near });
        }
        foreach (var b in batches)
        {
            var isSuspect = suspectLotCodes.Any(s => string.Equals(s, b.Code, StringComparison.OrdinalIgnoreCase));
            result.Add(new LotOptionDto { Kind = "production-batch", Id = b.Code, Code = b.Code, Product = b.ProductName, IsSuspect = isSuspect });
        }

        foreach (var inv in investigations)
        {
            var sCode = inv.SuspectLotId.Trim();
            if (!result.Any(r => string.Equals(r.Code, sCode, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(new LotOptionDto
                {
                    Kind = string.IsNullOrWhiteSpace(inv.SuspectLotType) ? "raw-lot" : inv.SuspectLotType,
                    Id = sCode,
                    Code = sCode,
                    Product = $"{inv.Name} (Suspect Lot)",
                    IsSuspect = true
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            result = result.Where(x => x.Code.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Product.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            var rawCodes = raws.Select(x => x.Code).Concat(finished.Select(x => x.Code));
            var nearMatches = rawCodes.Where(c => IdentifierNormalizer.IsNearMatch(c, q)).Distinct().ToList();
            if (nearMatches.Count > 0)
            {
                foreach (var x in result) x.NearMatches = x.NearMatches.Union(nearMatches, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
        return result;
    }

    public async Task<TraceResultDto> RunAsync(Guid workspaceId, TraceRunRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedId)) throw new InvalidOperationException("Selected lot is required.");
        var direction = request.Direction?.ToLowerInvariant() switch
        {
            "backward" => TraceDirection.Backward,
            "forward" => TraceDirection.Forward,
            _ => TraceDirection.Both
        };

        Investigation? targetInvestigation = null;
        if (request.InvestigationId.HasValue)
        {
            targetInvestigation = await db.Investigations.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == request.InvestigationId.Value, ct)
                ?? throw new KeyNotFoundException("Investigation not found.");
        }

        var ws = await db.Workspaces.FirstOrDefaultAsync(x => x.Id == workspaceId, ct);

        // Check aliases to support querying by alias
        var aliases = await db.EntityAliases.Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct);
        var aliasMap = aliases.ToDictionary(a => a.AliasValue.ToUpperInvariant(), a => a.CanonicalValue, StringComparer.OrdinalIgnoreCase);
        var canonicalSelectedId = aliasMap.TryGetValue(request.SelectedId.Trim().ToUpperInvariant(), out var canon) ? canon : request.SelectedId.Trim();

        var run = new TraceRun
        {
            WorkspaceId = workspaceId,
            InvestigationId = request.InvestigationId,
            SelectedType = request.SelectedType,
            SelectedId = request.SelectedId.Trim(),
            Direction = direction,
            TimingNote = request.TimingNote,
            StartedAt = DateTime.UtcNow
        };

        var links = await db.TraceLinks
            .Where(x => x.WorkspaceId == workspaceId && (request.InvestigationId == null || x.InvestigationId == request.InvestigationId))
            .ToListAsync(ct);

        // Validate existence (check either original code or resolved canonical)
        if (request.SelectedType == "raw-lot" && !await db.RawMaterialLots.AnyAsync(x => x.WorkspaceId == workspaceId && (x.Code == request.SelectedId || x.Code == canonicalSelectedId), ct))
            throw new KeyNotFoundException("Raw material lot not found.");
        if (request.SelectedType == "finished-lot" && !await db.FinishedLots.AnyAsync(x => x.WorkspaceId == workspaceId && (x.Code == request.SelectedId || x.Code == canonicalSelectedId), ct))
            throw new KeyNotFoundException("Finished lot not found.");
        if (request.SelectedType == "production-batch" && !await db.ProductionBatches.AnyAsync(x => x.WorkspaceId == workspaceId && (x.Code == request.SelectedId || x.Code == canonicalSelectedId), ct))
            throw new KeyNotFoundException("Production batch not found.");

        var rootKind = request.SelectedType;
        var rootId = canonicalSelectedId;
        var canForwardRoot = direction is TraceDirection.Forward or TraceDirection.Both;
        var canBackwardRoot = direction is TraceDirection.Backward or TraceDirection.Both;

        // Adjacency maps for Confirmed-only traversal and Full traversal
        var forwardAdjAll = new Dictionary<(string, string), List<TraceLink>>();
        var backwardAdjAll = new Dictionary<(string, string), List<TraceLink>>();
        var forwardAdjConfirmed = new Dictionary<(string, string), List<TraceLink>>();
        var backwardAdjConfirmed = new Dictionary<(string, string), List<TraceLink>>();

        foreach (var l in links)
        {
            if (l.State == LinkState.Rejected) continue;

            var from = (l.FromType, l.FromId);
            var to = (l.ToType, l.ToId);

            if (!forwardAdjAll.TryGetValue(from, out var fAll)) { fAll = []; forwardAdjAll[from] = fAll; }
            fAll.Add(l);
            if (!backwardAdjAll.TryGetValue(to, out var bAll)) { bAll = []; backwardAdjAll[to] = bAll; }
            bAll.Add(l);

            if (l.State == LinkState.Confirmed && !l.NeedsReReview && !l.Superseded)
            {
                if (!forwardAdjConfirmed.TryGetValue(from, out var fConf)) { fConf = []; forwardAdjConfirmed[from] = fConf; }
                fConf.Add(l);
                if (!backwardAdjConfirmed.TryGetValue(to, out var bConf)) { bConf = []; backwardAdjConfirmed[to] = bConf; }
                bConf.Add(l);
            }
        }

        // 1. Confirmed-only traversal
        var confirmedVisitedNodes = new HashSet<(string Kind, string Id)>();
        var confirmedEdges = new List<TraceLink>();
        var confirmedEdgeIds = new HashSet<Guid>();
        var confirmedQueue = new Queue<((string Kind, string Id) Node, bool CanForward, bool CanBackward)>();
        confirmedVisitedNodes.Add((rootKind, rootId));
        confirmedQueue.Enqueue(((rootKind, rootId), canForwardRoot, canBackwardRoot));

        while (confirmedQueue.Count > 0)
        {
            var (node, canForwardHere, canBackwardHere) = confirmedQueue.Dequeue();
            var candidates = new List<TraceLink>();
            if (canForwardHere && forwardAdjConfirmed.TryGetValue(node, out var fws)) candidates.AddRange(fws);
            if (canBackwardHere && backwardAdjConfirmed.TryGetValue(node, out var bws)) candidates.AddRange(bws);

            foreach (var link in candidates)
            {
                var other = link.FromType == node.Kind && link.FromId == node.Id ? (link.ToType, link.ToId) : (link.FromType, link.FromId);
                if (confirmedEdgeIds.Add(link.Id)) confirmedEdges.Add(link);
                if (confirmedVisitedNodes.Add(other))
                {
                    var forwardChild = canForwardHere && link.FromType == node.Kind && link.FromId == node.Id;
                    var backwardChild = canBackwardHere && !(link.FromType == node.Kind && link.FromId == node.Id);
                    confirmedQueue.Enqueue((other, forwardChild, backwardChild));
                }
            }
        }

        // 2. Full traversal (Confirmed + Candidate + Inferred + Gap collection)
        var allVisitedNodes = new HashSet<(string Kind, string Id)>();
        var allEdges = new List<TraceLink>();
        var allEdgeIds = new HashSet<Guid>();
        var traceGaps = new List<TraceGapDto>();
        var gapLinkIds = new HashSet<Guid>();
        var allQueue = new Queue<((string Kind, string Id) Node, bool CanForward, bool CanBackward, List<TraceLink> Path)>();
        allVisitedNodes.Add((rootKind, rootId));
        allQueue.Enqueue(((rootKind, rootId), canForwardRoot, canBackwardRoot, new List<TraceLink>()));

        var possiblePathDtos = new List<PossiblePathDto>();

        while (allQueue.Count > 0)
        {
            var (node, canForwardHere, canBackwardHere, currentPath) = allQueue.Dequeue();
            var candidates = new List<TraceLink>();
            if (canForwardHere && forwardAdjAll.TryGetValue(node, out var fws)) candidates.AddRange(fws);
            if (canBackwardHere && backwardAdjAll.TryGetValue(node, out var bws)) candidates.AddRange(bws);

            foreach (var link in candidates)
            {
                if (link.State == LinkState.Gap)
                {
                    if (gapLinkIds.Add(link.Id))
                    {
                        traceGaps.Add(new TraceGapDto
                        {
                            Title = string.IsNullOrWhiteSpace(link.LinkKind) ? "Missing evidence" : link.LinkKind,
                            Description = link.EvidenceText,
                            Recommendation = "Obtain and review the missing source document, then rerun the trace.",
                            SourceDocumentId = null
                        });
                    }
                    continue;
                }

                var other = link.FromType == node.Kind && link.FromId == node.Id ? (link.ToType, link.ToId) : (link.FromType, link.FromId);
                if (allEdgeIds.Add(link.Id)) allEdges.Add(link);

                var extendedPath = new List<TraceLink>(currentPath) { link };

                var isNewNode = allVisitedNodes.Add(other);
                if (isNewNode)
                {
                    var forwardChild = canForwardHere && link.FromType == node.Kind && link.FromId == node.Id;
                    var backwardChild = canBackwardHere && !(link.FromType == node.Kind && link.FromId == node.Id);
                    allQueue.Enqueue((other, forwardChild, backwardChild, extendedPath));
                }

                // If target node is a finished lot or customer and was reached with uncertain links, record possible path
                if (other.Item1 is "finished-lot" or "customer" && extendedPath.Any(p => p.State != LinkState.Confirmed || p.NeedsReReview))
                {
                    var uncertainEdges = extendedPath.Where(p => p.State != LinkState.Confirmed || p.NeedsReReview).ToList();
                    possiblePathDtos.Add(new PossiblePathDto
                    {
                        EndKind = other.Item1,
                        EndId = other.Item2,
                        Summary = $"Path to {other.Item1} '{other.Item2}' depends on {uncertainEdges.Count} unreviewed or candidate link(s).",
                        Steps = extendedPath.Select(e => new PossiblePathStepDto
                        {
                            EdgeId = e.Id,
                            FromType = e.FromType,
                            FromId = e.FromId,
                            ToType = e.ToType,
                            ToId = e.ToId,
                            LinkKind = e.LinkKind,
                            State = e.State,
                            Reason = e.Reason,
                            NeedsReReview = e.NeedsReReview
                        }).ToList(),
                        UncertainEdgeIds = uncertainEdges.Select(e => e.Id).ToList()
                    });
                }
            }
        }

        // Mass balance computed strictly on Confirmed links
        var mb = ComputeMassBalance(request, confirmedEdges, allEdges);

        var nodes = new List<TraceNodeDto> { new() { Id = rootId, Kind = rootKind, Label = rootId, Detail = "Selected lot" } };
        var nodeSet = new HashSet<(string, string)> { (rootKind, rootId) };
        foreach (var e in allEdges)
        {
            foreach (var tuple in new[] { (e.FromType, e.FromId), (e.ToType, e.ToId) })
            {
                if (nodeSet.Add(tuple))
                {
                    nodes.Add(new TraceNodeDto
                    {
                        Id = tuple.Item2,
                        Kind = tuple.Item1,
                        Label = tuple.Item2,
                        Detail = tuple.Item1.Replace("-", " "),
                        IncomingState = e.State
                    });
                }
            }
        }
        foreach (var g in traceGaps)
        {
            var gid = "GAP-" + g.Title;
            if (nodeSet.Add(("Gap", gid))) nodes.Add(new TraceNodeDto { Id = gid, Kind = "Gap", Label = "Evidence gap", Detail = g.Title });
        }

        // Scope separation: Confirmed scope vs Possible scope
        var affectedFinished = confirmedVisitedNodes.Where(n => n.Kind == "finished-lot").Select(n => n.Id).Distinct().ToList();
        var affectedCustomers = confirmedVisitedNodes.Where(n => n.Kind == "customer").Select(n => n.Id).Distinct().ToList();

        var allFinished = allVisitedNodes.Where(n => n.Kind == "finished-lot").Select(n => n.Id).Distinct().ToList();
        var allCustomers = allVisitedNodes.Where(n => n.Kind == "customer").Select(n => n.Id).Distinct().ToList();

        var possibleFinished = allFinished.Except(affectedFinished).Distinct().ToList();
        var possibleCustomers = allCustomers.Except(affectedCustomers).Distinct().ToList();

        var stateCounts = allEdges.GroupBy(e => e.State).ToDictionary(g => g.Key.ToString(), g => g.Count());
        foreach (var s in Enum.GetNames<LinkState>()) if (!stateCounts.ContainsKey(s)) stateCounts[s] = 0;

        var undecidedIds = allEdges.Where(e => e.State == LinkState.Candidate || e.NeedsReReview).Select(e => e.Id).ToList();

        var caveats = new List<string>();
        if (possibleFinished.Count > 0 || possibleCustomers.Count > 0)
        {
            caveats.Add($"{possibleFinished.Count} finished lot(s) and {possibleCustomers.Count} customer(s) are in possible scope via candidate or re-review links and are excluded from confirmed scope.");
        }
        if (undecidedIds.Count > 0)
        {
            caveats.Add($"{undecidedIds.Count} relationship proposal(s) traversed are in candidate status and have not been confirmed by an operator.");
        }
        if (allEdges.Any(e => e.NeedsReReview))
        {
            caveats.Add("One or more supporting facts were modified or rejected after link confirmation; affected links require re-review.");
        }

        var unresolved = traceGaps.Select(g => g.Title).Distinct().ToList();
        var summary = BuildSummary(request, affectedFinished, affectedCustomers, possibleFinished, possibleCustomers, traceGaps, stateCounts);

        var findings = await gaps.RefreshAsync(workspaceId, request.InvestigationId, ct);

        var workspaceMode = ws?.IsDemo == true ? "demo" : "pilot";
        var syntheticEvidenceCount = allEdges.Count(e => string.Equals(e.Origin, "seeded-demo", StringComparison.OrdinalIgnoreCase));
        var disclaimer = ExportFormatter.BuildProvenanceNotice(workspaceMode, syntheticEvidenceCount);

        var result = new TraceResultDto
        {
            RunId = run.Id,
            InvestigationId = run.InvestigationId,
            StartedAt = run.StartedAt,
            CompletedAt = DateTime.UtcNow,
            SelectedType = request.SelectedType,
            SelectedId = request.SelectedId,
            Direction = request.Direction ?? "both",
            Nodes = nodes,
            Edges = allEdges.Select(MapEdge).ToList(),
            Gaps = traceGaps,
            Findings = findings,
            AffectedFinishedLots = affectedFinished,
            AffectedCustomers = affectedCustomers,
            UnresolvedLots = unresolved,
            StateCounts = stateCounts,
            MassBalance = mb,
            Summary = summary,
            DemoDisclaimer = disclaimer,
            TimingNote = request.TimingNote,
            SnapshotVersion = 2,
            WorkspaceMode = workspaceMode,
            Disclaimer = disclaimer,
            ContainsSyntheticEvidence = syntheticEvidenceCount > 0,
            InvestigationName = targetInvestigation?.Name ?? string.Empty,
            InvestigationReference = targetInvestigation?.Reference ?? string.Empty,
            ConfirmedScopeEstablished = affectedFinished.Count > 0 || affectedCustomers.Count > 0,
            PossibleFinishedLots = possibleFinished,
            PossibleCustomers = possibleCustomers,
            PossiblePaths = possiblePathDtos,
            UndecidedRelationshipIds = undecidedIds,
            Caveats = caveats
        };

        run.CompletedAt = result.CompletedAt;
        run.ResultJson = JsonSerializer.Serialize(result);
        run.GapsJson = JsonSerializer.Serialize(traceGaps);
        run.MassBalanceJson = JsonSerializer.Serialize(mb);
        run.Status = traceGaps.Count > 0 || findings.Count > 0 ? "completed-with-gaps" : "completed";
        db.TraceRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await audit.AddAsync(workspaceId, "trace_run", request.Reviewer,
            $"Executed {request.Direction ?? "both"}-direction trace for {request.SelectedId} ({affectedFinished.Count} confirmed finished lots, {affectedCustomers.Count} confirmed customers, {possibleFinished.Count} possible finished lots).",
            "TraceRun", run.Id.ToString(),
            JsonSerializer.Serialize(new { result.AffectedFinishedLots, result.AffectedCustomers, result.PossibleFinishedLots, result.StateCounts, result.MassBalance.Status }), ct);

        return result;
    }

    public async Task<TraceResultDto?> GetRunAsync(Guid workspaceId, Guid runId, CancellationToken ct = default)
    {
        var run = await db.TraceRuns.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == runId, ct);
        if (run == null) return null;
        return JsonSerializer.Deserialize<TraceResultDto>(run.ResultJson);
    }

    public async Task<List<TraceRunSummaryDto>> ListRunsAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var runs = await db.TraceRuns
            .Where(x => x.WorkspaceId == workspaceId)
            .OrderByDescending(x => x.CompletedAt)
            .ToListAsync(ct);

        var summaries = new List<TraceRunSummaryDto>();
        foreach (var x in runs)
        {
            var gapCount = 0;
            var mbStatus = string.Empty;

            if (!string.IsNullOrWhiteSpace(x.GapsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(x.GapsJson);
                    gapCount = doc.RootElement.GetArrayLength();
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(x.MassBalanceJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(x.MassBalanceJson);
                    if (doc.RootElement.TryGetProperty("status", out var s1))
                        mbStatus = s1.GetString() ?? string.Empty;
                    else if (doc.RootElement.TryGetProperty("Status", out var s2))
                        mbStatus = s2.GetString() ?? string.Empty;
                }
                catch { }
            }

            summaries.Add(new TraceRunSummaryDto
            {
                Id = x.Id,
                CompletedAt = x.CompletedAt,
                SelectedType = x.SelectedType,
                SelectedId = x.SelectedId,
                Direction = x.Direction.ToString(),
                Status = x.Status,
                GapCount = gapCount,
                MassBalanceStatus = mbStatus
            });
        }
        return summaries;
    }

    private static TraceEdgeDto MapEdge(TraceLink l)
    {
        var supportingIds = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(l.SupportingFactIdsJson))
        {
            try
            {
                supportingIds = JsonSerializer.Deserialize<List<Guid>>(l.SupportingFactIdsJson) ?? new();
            }
            catch { }
        }

        return new TraceEdgeDto
        {
            Id = l.Id,
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
            SupportingFactIds = supportingIds
        };
    }

    private static MassBalanceDto ComputeMassBalance(TraceRunRequestDto request, List<TraceLink> confirmedEdges, List<TraceLink> allEdges)
    {
        var limitations = new List<string>();
        var inputLinks = confirmedEdges.Where(e => e.LinkKind.Contains("receipt", StringComparison.OrdinalIgnoreCase) || e.LinkKind.Contains("delivery", StringComparison.OrdinalIgnoreCase)).ToList();
        var outputLinks = confirmedEdges.Where(e => e.LinkKind.Contains("dispatch", StringComparison.OrdinalIgnoreCase) || e.LinkKind.Contains("finished", StringComparison.OrdinalIgnoreCase)).ToList();
        var wasteLinks = confirmedEdges.Where(e => e.LinkKind.Contains("waste", StringComparison.OrdinalIgnoreCase)).ToList();

        var candidateCount = allEdges.Count(e => e.State != LinkState.Confirmed || e.NeedsReReview);
        if (candidateCount > 0)
        {
            limitations.Add($"{candidateCount} candidate or unreviewed trace link(s) were excluded from mass balance calculations.");
        }

        var inputTotal = inputLinks.Select(e => e.Quantity).Where(q => q.HasValue).Any() ? inputLinks.Where(e => e.Quantity.HasValue).Sum(e => e.Quantity!.Value) : (decimal?)null;
        var outputTotal = outputLinks.Select(e => e.Quantity).Where(q => q.HasValue).Any() ? outputLinks.Where(e => e.Quantity.HasValue).Sum(e => e.Quantity!.Value) : (decimal?)null;
        var waste = wasteLinks.Select(e => e.Quantity).Where(q => q.HasValue).Any() ? wasteLinks.Where(e => e.Quantity.HasValue).Sum(e => e.Quantity!.Value) : (decimal?)null;
        var inputUnit = inputLinks.Select(e => e.QuantityUnit).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().Count() == 1 ? inputLinks.Select(e => e.QuantityUnit).First(u => !string.IsNullOrWhiteSpace(u)) : (inputLinks.Count > 0 ? (inputLinks[0].QuantityUnit ?? string.Empty) : string.Empty);
        var outputUnit = outputLinks.Select(e => e.QuantityUnit).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().Count() == 1 ? outputLinks.Select(e => e.QuantityUnit).First(u => !string.IsNullOrWhiteSpace(u)) : (outputLinks.Count > 0 ? (outputLinks[0].QuantityUnit ?? string.Empty) : string.Empty);
        
        var status = "insufficient";
        string notes;

        if (inputTotal.HasValue && outputTotal.HasValue && !string.IsNullOrWhiteSpace(inputUnit) && inputUnit == outputUnit)
        {
            var tolerance = 0.01m * (inputTotal.Value == 0 ? 1 : inputTotal.Value);
            if (Math.Abs(inputTotal.Value - (outputTotal.Value + (waste ?? 0m))) <= tolerance)
            {
                status = "reconciled";
            }
            else
            {
                status = "discrepancy";
                limitations.Add("Total output quantity does not equal total input quantity within standard 1% tolerance.");
            }
            var inStr = inputTotal.Value.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
            var outStr = outputTotal.Value.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
            var wasteStr = (waste ?? 0m).ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
            notes = $"Input: {inStr} {inputUnit} · Output: {outStr} {outputUnit} · Waste: {wasteStr} {outputUnit}.";
        }
        else
        {
            status = "insufficient";
            if (inputLinks.Count == 0 && outputLinks.Count == 0)
            {
                notes = "No quantity-bearing confirmed trace links were recorded in this path.";
                limitations.Add("No quantity records exist on confirmed links in this path.");
            }
            else if (inputUnit != outputUnit)
            {
                notes = $"Unit conversion required: input is recorded in '{inputUnit}' while output is recorded in '{outputUnit}'.";
                limitations.Add($"Input unit '{inputUnit}' differs from output unit '{outputUnit}' without verified conversion ratio.");
            }
            else
            {
                notes = "Incomplete input or output quantities across the traversed confirmed links.";
                limitations.Add("Missing quantity on one or more confirmed trace stages.");
            }
        }

        return new MassBalanceDto
        {
            Status = status,
            InputTotal = inputTotal,
            InputUnit = inputUnit,
            OutputTotal = outputTotal,
            OutputUnit = outputUnit,
            Waste = waste,
            Notes = notes,
            Components = new Dictionary<string, decimal?> { ["input"] = inputTotal, ["output"] = outputTotal, ["waste"] = waste },
            Limitations = limitations
        };
    }

    private static string BuildSummary(
        TraceRunRequestDto request,
        List<string> affectedFinished,
        List<string> affectedCustomers,
        List<string> possibleFinished,
        List<string> possibleCustomers,
        List<TraceGapDto> gaps,
        Dictionary<string, int> stateCounts)
    {
        var typeLabel = request.SelectedType switch
        {
            "raw-lot" => "Raw Material Lot",
            "finished-lot" => "Finished Product Lot",
            "production-batch" => "Production Batch",
            _ => request.SelectedType
        };
        var dirLabel = (request.Direction ?? "both").ToLowerInvariant() switch
        {
            "forward" => "Forward trace",
            "backward" => "Backward trace",
            _ => "Bidirectional trace"
        };

        var confirmedStr = $"{affectedFinished.Count} confirmed finished product lot(s) and {affectedCustomers.Count} confirmed customer destination(s)";
        var possibleStr = (possibleFinished.Count > 0 || possibleCustomers.Count > 0)
            ? $"; {possibleFinished.Count} finished lot(s) and {possibleCustomers.Count} customer(s) identified in possible scope pending review"
            : "";

        return $"{dirLabel} for {typeLabel} {request.SelectedId}: {confirmedStr}{possibleStr}. Link verification breakdown: {stateCounts["Confirmed"]} confirmed, {stateCounts["Candidate"]} pending review, {stateCounts["Inferred"]} inferred (not operator-confirmed), and {gaps.Count} unresolved evidence gap(s).";
    }
}
