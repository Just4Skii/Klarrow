using System.Text;
using Microsoft.EntityFrameworkCore;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class ExtractionService(RecallGraphDbContext db, IAiExtractionProvider provider, DocumentService documents, AuditService audit, StorageOptions storageOptions)
{
    public async Task<AiProviderStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var status = provider.Status;
        var last = await db.AiExtractionRuns.Where(x => x.Status == "completed").OrderByDescending(x => x.CompletedAt).FirstOrDefaultAsync(ct);
        status.LastSuccessfulRunAt = last?.CompletedAt;
        status.LastSuccessfulModelId = last?.ModelId ?? string.Empty;
        status.HasLiveExtractedFacts = await db.ExtractedFacts.AnyAsync(x => x.ExtractionMethod == "ai-extraction", ct);
        if (status.IsConfigured)
        {
            status.Message = status.HasLiveExtractedFacts
                ? "Live model extraction has produced candidate facts. Each fact carries its own grounding status; human review is required before confirmation."
                : "Endpoint configured. No live extraction has completed yet; preloaded fixture records are not output of the configured model.";
        }
        return status;
    }

    public async Task<ExtractionRunResultDto> RunAsync(Guid workspaceId, List<Guid>? documentIds, string reviewer, CancellationToken ct = default)
    {
        var docs = documentIds == null || documentIds.Count == 0
            ? await db.SourceDocuments.Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct)
            : await db.SourceDocuments.Where(x => x.WorkspaceId == workspaceId && documentIds.Contains(x.Id)).ToListAsync(ct);

        var run = new AiExtractionRun
        {
            WorkspaceId = workspaceId,
            Provider = provider.Status.Provider,
            ModelId = provider.Status.ModelId,
            Status = "running"
        };
        db.AiExtractionRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var factsCreated = 0;
        string? error = null;
        var fixtureFallback = !provider.Status.IsConfigured;
        try
        {
            foreach (var doc in docs)
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(doc.StoredPath, ct);
                var imageBase64 = doc.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? Convert.ToBase64String(bytes) : null;
                string text = string.Empty;
                if (!doc.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    try { text = doc.MimeType == "application/pdf" ? ExtractPdfText(bytes) : System.IO.File.ReadAllText(doc.StoredPath); } catch { text = string.Empty; }
                }
                if (text.Length > 6000) text = text[..6000];

                var result = await provider.ExtractAsync(text, imageBase64, doc.MimeType.StartsWith("image/") ? doc.MimeType : null, doc.FileName, ct);
                if (!result.IsValid)
                {
                    error = result.Error;
                    run.Status = fixtureFallback ? "completed-fixture" : "failed";
                    if (!fixtureFallback) break;
                }

                if (!provider.Status.IsConfigured)
                {
                    fixtureFallback = true;
                    break;
                }

                var documentLocations = await db.SourceLocations.Where(x => x.SourceDocumentId == doc.Id).ToListAsync(ct);
                var existingFacts = await db.ExtractedFacts.Where(x => x.SourceDocumentId == doc.Id && x.SourceLocationId != null).ToListAsync(ct);
                foreach (var candidate in result.Facts)
                {
                    if (string.IsNullOrWhiteSpace(candidate.RawValue)) continue;
                    var quoteToFind = !string.IsNullOrWhiteSpace(candidate.EvidenceText)
                        ? candidate.EvidenceText
                        : candidate.RawValue;

                    Guid? locationId = null;
                    int? foundLine = null;
                    string matchMethod = "unmatched";
                    string validationOutcome = GroundingSemantics.UnverifiedSource;
                    string locationPrecision = "unverified against derived text; human grounding required";

                    var lines = string.IsNullOrWhiteSpace(text)
                        ? Array.Empty<string>()
                        : text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    Guid? LocateSpan(int startLine, int endLine)
                    {
                        if (startLine < 1) startLine = 1;
                        if (endLine < startLine) endLine = startLine;
                        var existingLoc = documentLocations.FirstOrDefault(l => l.LineStart == startLine && l.LineEnd == endLine);
                        if (existingLoc != null) return existingLoc.Id;
                        var newLoc = new SourceLocation
                        {
                            SourceDocumentId = doc.Id,
                            LineStart = startLine,
                            LineEnd = endLine,
                            RawCoordinate = startLine == endLine ? $"line {startLine}" : $"lines {startLine}-{endLine}"
                        };
                        db.SourceLocations.Add(newLoc);
                        documentLocations.Add(newLoc);
                        return newLoc.Id;
                    }

                    int? FindQuoteLine()
                    {
                        for (var i = 0; i < lines.Length; i++)
                        {
                            if (GroundingSemantics.QuoteAppearsInLine(quoteToFind, lines[i])) return i + 1;
                        }
                        return null;
                    }

                    int? FindValueLine()
                    {
                        for (var i = 0; i < lines.Length; i++)
                        {
                            if (GroundingSemantics.ValueAppearsInQuote(candidate.RawValue, lines[i])) return i + 1;
                        }
                        return null;
                    }

                    var quoteLine = FindQuoteLine();
                    var valueLine = FindValueLine();

                    // The only verified state: the cited quote exists at a real locator AND the raw value
                    // appears inside that exact quote. A matching quote elsewhere on the line is not enough.
                    if (quoteLine.HasValue && GroundingSemantics.ValueAppearsInQuote(candidate.RawValue, quoteToFind))
                    {
                        locationId = LocateSpan(quoteLine.Value, quoteLine.Value);
                        foundLine = quoteLine.Value;
                        matchMethod = "exact_quote";
                        validationOutcome = GroundingSemantics.QuoteVerified;
                        locationPrecision = $"value appears inside the quoted text at line {quoteLine.Value}";
                    }
                    else if (quoteLine.HasValue && valueLine.HasValue && Math.Abs(valueLine.Value - quoteLine.Value) == 1)
                    {
                        // Honest separate state: the quote is real but the value is on the adjacent line.
                        // Store the broader span so the UI/source drawer shows both lines.
                        var start = Math.Min(valueLine.Value, quoteLine.Value);
                        var end = Math.Max(valueLine.Value, quoteLine.Value);
                        locationId = LocateSpan(start, end);
                        foundLine = quoteLine.Value;
                        matchMethod = "adjacent_line_context";
                        validationOutcome = GroundingSemantics.AdjacentLineContext;
                        locationPrecision = $"quote located at line {quoteLine.Value}; value '{candidate.RawValue}' is on adjacent line {valueLine.Value}, not inside the quote";
                    }
                    else if (quoteLine.HasValue)
                    {
                        locationId = LocateSpan(quoteLine.Value, quoteLine.Value);
                        foundLine = quoteLine.Value;
                        matchMethod = "quote_verbatim_value_not_in_quote";
                        validationOutcome = GroundingSemantics.IdentifierUnverified;
                        locationPrecision = $"quote located at line {quoteLine.Value}, but value '{candidate.RawValue}' is not inside the quoted span";
                    }
                    else if (valueLine.HasValue)
                    {
                        locationId = LocateSpan(valueLine.Value, valueLine.Value);
                        foundLine = valueLine.Value;
                        matchMethod = "identifier_in_line";
                        validationOutcome = GroundingSemantics.IdentifierUnverified;
                        locationPrecision = $"identifier '{candidate.RawValue}' matched line {valueLine.Value}, but the quoted text was not located verbatim - human grounding required";
                    }
                    else
                    {
                        // No invented line-1 fallback. A deterministic fact may give us a real location,
                        // but it still cannot make an unlocated model quote quote-verified.
                        var matchedFact = existingFacts.FirstOrDefault(f => f.EvidenceText.Contains(candidate.RawValue, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(f.SourceQuote) && f.SourceQuote.Contains(candidate.RawValue, StringComparison.OrdinalIgnoreCase)));
                        if (matchedFact?.SourceLocationId != null)
                        {
                            locationId = matchedFact.SourceLocationId;
                            foundLine = documentLocations.FirstOrDefault(l => l.Id == locationId)?.LineStart;
                            matchMethod = "matched_to_deterministic_fact";
                            validationOutcome = GroundingSemantics.IdentifierUnverified;
                            locationPrecision = $"identifier '{candidate.RawValue}' matched a deterministic evidence row, but the quote was not located verbatim - human grounding required";
                        }
                        else
                        {
                            validationOutcome = GroundingSemantics.UnverifiedSource;
                            locationPrecision = "quote and value were not located in the derived source text; no locator assigned";
                        }
                    }

                    var fact = new ExtractedFact
                    {
                        WorkspaceId = workspaceId,
                        SourceDocumentId = doc.Id,
                        SourceLocationId = locationId,
                        FactType = MapFactType(candidate.FactType),
                        ClaimType = candidate.ClaimType ?? "llm extracted",
                        ClaimSubject = candidate.ClaimSubject ?? candidate.RawValue,
                        ClaimObject = candidate.ClaimObject ?? string.Empty,
                        RawValue = candidate.RawValue,
                        NormalizedValue = candidate.NormalizedValue ?? string.Empty,
                        EvidenceText = candidate.EvidenceText ?? candidate.RawValue,
                        SourceQuote = quoteToFind,
                        SourceMatchMethod = matchMethod,
                        Quantity = candidate.Quantity,
                        QuantityUnit = candidate.QuantityUnit,
                        EventDate = candidate.EventDate,
                        ReviewStatus = ReviewStatus.Unreviewed,
                        ConfidenceNote = $"{candidate.ConfidenceNote ?? "LLM-extracted; review required."} ({locationPrecision})",
                        Confidence = candidate.Confidence ?? 0.6,
                        ProviderMetadata = $"{result.Provider}/{result.ModelId}; schema v1.0",
                        ExtractionMethod = "ai-extraction",
                        Provider = result.Provider,
                        ModelId = result.ModelId,
                        PromptVersion = "recallgraph-llama-extraction-v1",
                        ValidationOutcome = validationOutcome,
                        SubjectType = candidate.IsRelation ? "relation-candidate" : "fact",
                        ExtractionRunId = run.Id
                    };
                    db.ExtractedFacts.Add(fact);
                    factsCreated++;
                }
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            run.Status = "failed";
        }

        if (fixtureFallback || !provider.Status.IsConfigured)
        {
            run.Status = "completed-fixture";
            run.Provider = "FixtureParser";
            run.ModelId = string.Empty;
        }
        else if (run.Status == "running")
        {
            run.Status = error == null ? "completed" : "completed-with-errors";
        }
        run.CompletedAt = DateTime.UtcNow;
        run.Error = error;
        run.DocumentCount = docs.Count;
        await db.SaveChangesAsync(ct);
        var modelLabel = string.IsNullOrWhiteSpace(run.ModelId) ? run.Provider : run.ModelId;
        await audit.AddAsync(workspaceId, "extraction_run", reviewer, $"AI extraction run ({modelLabel}): {run.Status}, {factsCreated} candidate claim(s) extracted across {docs.Count} document(s).", "AiExtractionRun", run.Id.ToString(), $"{{\"fixtureFallback\":{fixtureFallback.ToString().ToLowerInvariant()},\"factsCreated\":{factsCreated}}}", ct);

        return new ExtractionRunResultDto
        {
            ExtractionRunId = run.Id,
            Status = run.Status,
            IsDemo = fixtureFallback || !provider.Status.IsConfigured,
            Provider = run.Provider,
            ModelId = run.ModelId,
            DocumentsProcessed = docs.Count,
            FactsCreated = factsCreated,
            Error = error
        };
    }

    private static FactType MapFactType(string? value) => (value?.ToLowerInvariant()) switch
    {
        "supplier_delivery" => FactType.SupplierDelivery,
        "goods_receipt" => FactType.GoodsReceipt,
        "stock_ledger" => FactType.StockLedger,
        "production_batch" => FactType.ProductionBatch,
        "rework_entry" => FactType.ReworkEntry,
        "finished_lot" => FactType.FinishedLot,
        "dispatch" => FactType.Dispatch,
        "quality_test" => FactType.QualityTest,
        "invoice" => FactType.Invoice,
        "note" => FactType.Note,
        _ => FactType.Other
    };

    private static string ExtractPdfText(byte[] bytes)
    {
        var sb = new StringBuilder();
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        foreach (var page in pdf.GetPages()) { sb.AppendLine(page.Text); if (sb.Length > 6000) break; }
        return sb.ToString();
    }
}
