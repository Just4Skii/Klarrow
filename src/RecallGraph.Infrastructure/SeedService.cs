using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class SeedService(RecallGraphDbContext db, IOptions<StorageOptions> storageOptions, WorkspaceService workspaceService, DocumentService documentService, AuditService audit)
{
    private readonly StorageOptions opts = storageOptions.Value;

    public async Task EnsureSeededAsync(bool demoEnabled = true, CancellationToken ct = default)
    {
        if (await db.Workspaces.AnyAsync(ct)) return;
        if (!demoEnabled)
        {
            await workspaceService.GetOrCreatePilotAsync(ct);
            return;
        }
        await SeedAsync(false, ct);
    }

    public async Task<SeedResultDto> SeedAsync(bool reset, CancellationToken ct = default)
    {
        if (reset)
        {
            db.ChangeTracker.Clear();
            db.AuditEvents.RemoveRange(db.AuditEvents);
            db.AiExtractionRuns.RemoveRange(db.AiExtractionRuns);
            db.TraceRuns.RemoveRange(db.TraceRuns);
            db.TraceLinks.RemoveRange(db.TraceLinks);
            db.ExtractedFacts.RemoveRange(db.ExtractedFacts);
            db.SourceLocations.RemoveRange(db.SourceLocations);
            db.SourceDocuments.RemoveRange(db.SourceDocuments);
            db.FinishedLots.RemoveRange(db.FinishedLots);
            db.ProductionBatches.RemoveRange(db.ProductionBatches);
            db.RawMaterialLots.RemoveRange(db.RawMaterialLots);
            db.Customers.RemoveRange(db.Customers);
            db.Suppliers.RemoveRange(db.Suppliers);
            db.Locations.RemoveRange(db.Locations);
            db.Investigations.RemoveRange(db.Investigations);
            db.Workspaces.RemoveRange(db.Workspaces);
            await db.SaveChangesAsync(ct);

            var uploads = Path.GetFullPath(opts.UploadsPath);
            if (Directory.Exists(uploads)) foreach (var f in Directory.GetFiles(uploads)) System.IO.File.Delete(f);
        }

        var workspace = await workspaceService.GetOrCreateDemoAsync(ct);

        var demoInvestigation = await db.Investigations.FirstOrDefaultAsync(x => x.WorkspaceId == workspace.Id, ct);
        if (demoInvestigation == null)
        {
            demoInvestigation = new Investigation
            {
                WorkspaceId = workspace.Id,
                Name = "RM-0427 synthetic recall scenario",
                Reference = "DEMO-2026-09",
                SuspectLotType = "raw-lot",
                SuspectLotId = "RM-0427",
                Description = "Synthetic demonstration scenario. Hypothetical contamination only; not a real event.",
                Status = "Open",
                CreatedBy = "system"
            };
            db.Investigations.Add(demoInvestigation);
            await db.SaveChangesAsync(ct);
        }

        var evidencePath = ResolvePath(opts.SeedEvidencePath);
        if (Directory.Exists(evidencePath))
        {
            foreach (var file in Directory.GetFiles(evidencePath).OrderBy(x => x))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(file, ct);
                var ext = Path.GetExtension(file).ToLowerInvariant();
                var mime = ext switch
                {
                    ".pdf" => "application/pdf",
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ".csv" => "text/csv",
                    ".txt" => "text/plain",
                    ".png" or ".jpg" or ".jpeg" or ".bmp" => "image/png",
                    _ => "application/octet-stream"
                };
                await documentService.UploadAsync(workspace.Id, Path.GetFileName(file), mime, bytes, true, demoInvestigation!.Id, ct);
            }
        }

        // suppliers
        var supplier = new Supplier { WorkspaceId = workspace.Id, Name = "Northern Wheat Supplies (Pty) Ltd", Alias = "NWS", Location = "Pretoria, GP" };
        db.Suppliers.Add(supplier);

        // raw lots
        db.RawMaterialLots.AddRange(
            new RawMaterialLot { WorkspaceId = workspace.Id, Code = "RM-0427", SupplierId = supplier.Id, ProductName = "Wheat flour, fine", ReceivedDate = new DateTime(2026, 9, 10), Quantity = 1200, QuantityUnit = "kg", Notes = "Suspect lot for mock recall (hypothetical contamination)." },
            new RawMaterialLot { WorkspaceId = workspace.Id, Code = "RM-0399", SupplierId = supplier.Id, ProductName = "Wheat flour, fine", ReceivedDate = new DateTime(2026, 9, 3), Quantity = 900, QuantityUnit = "kg", Notes = "Earlier unaffected lot with similar packaging." }
        );

        // production batches
        var batchA = new ProductionBatch { WorkspaceId = workspace.Id, Code = "PB-2026-0912-A", ProductionDate = new DateTime(2026, 9, 12), Line = "Bakery Line 1", ProductName = "Cheddar oat snack 120g", InputQuantity = 975, InputUnit = "kg", OutputQuantity = 2400, OutputUnit = "units", Waste = 12, WasteUnit = "kg" };
        var batchR = new ProductionBatch { WorkspaceId = workspace.Id, Code = "PB-2026-0913-R", ProductionDate = new DateTime(2026, 9, 13), Line = "Rework line", ProductName = "Cheddar oat snack 120g (rework)", InputQuantity = 40, InputUnit = "kg", OutputQuantity = 1500, OutputUnit = "units", Waste = 2, WasteUnit = "kg" };
        var batchB = new ProductionBatch { WorkspaceId = workspace.Id, Code = "PB-2026-0905-A", ProductionDate = new DateTime(2026, 9, 5), Line = "Bakery Line 1", ProductName = "Cheddar oat snack 120g", InputQuantity = 700, InputUnit = "kg", OutputQuantity = 1800, OutputUnit = "units", Waste = 8, WasteUnit = "kg" };
        db.ProductionBatches.AddRange(batchA, batchR, batchB);

        // finished lots
        var fl514 = new FinishedLot { WorkspaceId = workspace.Id, Code = "FL-0514", ProductionBatchId = batchA.Id, ProductName = "Cheddar oat snack 120g", Quantity = 2400, QuantityUnit = "units", PackDate = new DateTime(2026, 9, 12), Status = "dispatched", Notes = "Affected finished lot." };
        var fl515 = new FinishedLot { WorkspaceId = workspace.Id, Code = "FL-0515", ProductionBatchId = batchR.Id, ProductName = "Cheddar oat snack 120g", Quantity = 1500, QuantityUnit = "units", PackDate = new DateTime(2026, 9, 13), Status = "dispatched", Notes = "Affected rework finished lot." };
        var fl516 = new FinishedLot { WorkspaceId = workspace.Id, Code = "FL-0516", ProductionBatchId = batchB.Id, ProductName = "Cheddar oat snack 120g", Quantity = 1800, QuantityUnit = "units", PackDate = new DateTime(2026, 9, 5), Status = "dispatched", Notes = "Unaffected lot with deceptively similar code." };
        db.FinishedLots.AddRange(fl514, fl515, fl516);

        // customers
        var c1 = new Customer { WorkspaceId = workspace.Id, Name = "Tuckshop Arendse (Benoni)", Type = "retailer", Location = "Benoni, GP" };
        var c2 = new Customer { WorkspaceId = workspace.Id, Name = "Groot Pretoria Foods (Pty) Ltd", Type = "wholesaler", Location = "Pretoria, GP" };
        var c3 = new Customer { WorkspaceId = workspace.Id, Name = "Cafe Kapabela", Type = "foodservice", Location = "Centurion, GP" };
        var c4 = new Customer { WorkspaceId = workspace.Id, Name = "Mall Mart Centurion", Type = "retailer", Location = "Centurion, GP" };
        db.Customers.AddRange(c1, c2, c3, c4);

        await db.SaveChangesAsync(ct);

        var factsWithDocs = await (from f in db.ExtractedFacts.Where(x => x.WorkspaceId == workspace.Id)
                                   join d in db.SourceDocuments on f.SourceDocumentId equals d.Id
                                   select new { f.Id, f.RawValue, d.FileName }).ToListAsync(ct);

        Guid? fact(string needle, string? fileHint = null) => factsWithDocs
            .Where(x => x.RawValue.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .Where(x => fileHint == null || x.FileName.Contains(fileHint, StringComparison.OrdinalIgnoreCase))
            .Select(x => (Guid?)x.Id)
            .FirstOrDefault();

        string refs(string fileHint, params string[] needles) => JsonSerializer.Serialize(needles
            .Select(n => fact(n, fileHint))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList());

        var links = new List<TraceLink>();

        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "supplier", FromId = supplier.Name, ToType = "raw-lot", ToId = "RM-0427", LinkKind = "supplier_delivery", State = LinkState.Confirmed, Quantity = 1200, QuantityUnit = "kg", EventDate = new DateTime(2026, 9, 10), EvidenceText = "Supplier delivery note and GRN support 1,200 kg received.", SourceReferencesJson = refs("01_supplier", "RM-0427"), ConfidenceNote = "Confirmed against delivery note and goods-received record.", DetectionMethod = "SeededCanonicalGraph" });
        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "supplier", FromId = supplier.Name, ToType = "raw-lot", ToId = "RM-0399", LinkKind = "supplier_delivery", State = LinkState.Confirmed, Quantity = 900, QuantityUnit = "kg", EventDate = new DateTime(2026, 9, 3), EvidenceText = "Earlier delivery of unaffected lot.", SourceReferencesJson = refs("02_goods", "RM-0399"), DetectionMethod = "SeededCanonicalGraph" });

        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "raw-lot", FromId = "RM-0427", ToType = "production-batch", ToId = "PB-2026-0912-A", LinkKind = "raw_to_batch", State = LinkState.Confirmed, Quantity = 975, QuantityUnit = "kg", EventDate = new DateTime(2026, 9, 12), EvidenceText = "Production batch sheet consumes RM-0427; quantity conflicts with 1,200 kg receipt.", SourceReferencesJson = refs("04_production", "RM-0427"), ConfidenceNote = "Quantity discrepancy remains open.", DetectionMethod = "SeededCanonicalGraph" });
        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "production-batch", ToType = "finished-lot", FromId = "PB-2026-0912-A", ToId = "FL-0514", LinkKind = "batch_to_finished", State = LinkState.Confirmed, Quantity = 2400, QuantityUnit = "units", EventDate = new DateTime(2026, 9, 12), EvidenceText = "Packaging record links FL-0514 to batch PB-2026-0912-A.", SourceReferencesJson = refs("07_packaging", "FL-0514"), DetectionMethod = "SeededCanonicalGraph" });

        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "production-batch", FromId = "PB-2026-0912-A", ToType = "production-batch", ToId = "PB-2026-0913-R", LinkKind = "rework_input", State = LinkState.Confirmed, Quantity = 40, QuantityUnit = "kg", EventDate = new DateTime(2026, 9, 13), EvidenceText = "Controlled rework entry moves 40 kg into rework batch.", SourceReferencesJson = refs("06_controlled", "PB-2026-0913"), DetectionMethod = "SeededCanonicalGraph" });
        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "production-batch", FromId = "PB-2026-0913-R", ToType = "finished-lot", ToId = "FL-0515", LinkKind = "rework_to_finished", State = LinkState.Confirmed, Quantity = 1500, QuantityUnit = "units", EventDate = new DateTime(2026, 9, 13), EvidenceText = "Rework finished lot FL-0515.", SourceReferencesJson = refs("07_packaging", "FL-0515"), DetectionMethod = "SeededCanonicalGraph" });

        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "finished-lot", FromId = "FL-0514", ToType = "customer", ToId = c1.Name, LinkKind = "finished_to_dispatch", State = LinkState.Confirmed, Quantity = 1500, QuantityUnit = "units", EventDate = new DateTime(2026, 9, 14), EvidenceText = "Dispatch note for 1,500 units to Tuckshop Arendse.", SourceReferencesJson = refs("08_dispatch", "FL-0514"), DetectionMethod = "SeededCanonicalGraph" });
        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "finished-lot", FromId = "FL-0514", ToType = "customer", ToId = c2.Name, LinkKind = "finished_to_dispatch", State = LinkState.Confirmed, Quantity = 900, QuantityUnit = "units", EventDate = new DateTime(2026, 9, 14), EvidenceText = "Dispatch note for 900 units to Groot Pretoria Foods.", SourceReferencesJson = refs("09_dispatch", "FL-0514"), DetectionMethod = "SeededCanonicalGraph" });
        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "finished-lot", FromId = "FL-0515", ToType = "customer", ToId = c3.Name, LinkKind = "finished_to_dispatch", State = LinkState.Confirmed, Quantity = 1000, QuantityUnit = "units", EventDate = new DateTime(2026, 9, 15), EvidenceText = "Dispatch note for 1,000 units to Cafe Kapabela.", SourceReferencesJson = refs("10_dispatch", "FL-0515"), DetectionMethod = "SeededCanonicalGraph" });
        // gap
        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "finished-lot", FromId = "FL-0515", ToType = "customer", ToId = "UNKNOWN DESTINATION (missing dispatch page)", LinkKind = "missing_dispatch", State = LinkState.Gap, Quantity = 500, QuantityUnit = "units", EventDate = new DateTime(2026, 9, 15), EvidenceText = "500 units of FL-0515 remain unallocated because a dispatch page is missing.", SourceReferencesJson = "[]", ConfidenceNote = "Gap, not evidence of no affected customer.", DetectionMethod = "SeededCanonicalGraph" });

        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "finished-lot", FromId = "FL-0516", ToType = "customer", ToId = c4.Name, LinkKind = "finished_to_dispatch", State = LinkState.Confirmed, Quantity = 1800, QuantityUnit = "units", EventDate = new DateTime(2026, 9, 6), EvidenceText = "Unaffected lot dispatched to Mall Mart.", SourceReferencesJson = refs("14_dispatch", "FL-0516"), DetectionMethod = "SeededCanonicalGraph" });
        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "raw-lot", FromId = "RM-0399", ToType = "production-batch", ToId = "PB-2026-0905-A", LinkKind = "raw_to_batch", State = LinkState.Confirmed, Quantity = 700, QuantityUnit = "kg", EventDate = new DateTime(2026, 9, 5), EvidenceText = "Earlier unaffected batch links to RM-0399.", SourceReferencesJson = refs("17_previous", "RM-0399"), DetectionMethod = "SeededCanonicalGraph" });
        links.Add(new TraceLink { WorkspaceId = workspace.Id, FromType = "production-batch", FromId = "PB-2026-0905-A", ToType = "finished-lot", ToId = "FL-0516", LinkKind = "batch_to_finished", State = LinkState.Confirmed, Quantity = 1800, QuantityUnit = "units", EventDate = new DateTime(2026, 9, 5), EvidenceText = "Unaffected lot packaging record.", SourceReferencesJson = refs("07_packaging", "FL-0516"), DetectionMethod = "SeededCanonicalGraph" });

        foreach (var link in links) { link.InvestigationId = demoInvestigation!.Id; link.Origin = "seeded-demo"; link.DetectionMethod = string.IsNullOrWhiteSpace(link.DetectionMethod) ? "seeded-fixture" : link.DetectionMethod; }
        db.TraceLinks.AddRange(links);
        await db.SaveChangesAsync(ct);

        await audit.AddAsync(workspace.Id, "seed_loaded", "system", $"Loaded the synthetic demonstration fixture into the workspace: {await db.SourceDocuments.CountAsync(x => x.WorkspaceId == workspace.Id, ct)} documents and {links.Count} trace links.", "Workspace", workspace.Id.ToString(), "{}", ct);

        return new SeedResultDto
        {
            Reset = reset,
            WorkspaceId = workspace.Id.ToString(),
            DocumentCount = await db.SourceDocuments.CountAsync(x => x.WorkspaceId == workspace.Id, ct),
            FactCount = await db.ExtractedFacts.CountAsync(x => x.WorkspaceId == workspace.Id, ct),
            LinkCount = links.Count,
            EvidencePath = evidencePath
        };
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path) && Directory.Exists(path)) return path;
        var starts = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in starts)
        {
            var dir = new DirectoryInfo(start);
            for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, path);
                if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        }
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
    }
}
