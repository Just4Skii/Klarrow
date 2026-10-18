using System.Text;
using System.Text.RegularExpressions;
using RecallGraph.Application;
using UglyToad.PdfPig;

namespace RecallGraph.Infrastructure;

public sealed record ParseResult(List<ExtractedFact> Facts, List<SourceLocation> Locations, string? Error, string ExtractedText = "");

public interface IDocumentParser
{
    bool CanHandle(SourceDocument document, byte[] bytes);
    Task<ParseResult> ParseAsync(SourceDocument document, byte[] bytes, CancellationToken ct = default);
}

public static class FactTypeGuesser
{
    public static FactType Guess(string fileName, string line)
    {
        var f = (fileName + " " + line).ToLowerInvariant();
        if (f.Contains("dispatch")) return FactType.Dispatch;
        if (f.Contains("rework")) return FactType.ReworkEntry;
        if (f.Contains("batch") || f.Contains("production")) return FactType.ProductionBatch;
        if (f.Contains("received") || f.Contains("grn")) return FactType.GoodsReceipt;
        if (f.Contains("delivery")) return FactType.SupplierDelivery;
        if (f.Contains("stock") || f.Contains("ledger")) return FactType.StockLedger;
        if (f.Contains("finished") || f.Contains("pack")) return FactType.FinishedLot;
        if (f.Contains("quality") || f.Contains("lab")) return FactType.QualityTest;
        if (f.Contains("invoice")) return FactType.Invoice;
        if (f.Contains("note") || f.Contains("email")) return FactType.Note;
        return FactType.Other;
    }
}

public sealed class TextDocumentParser : IDocumentParser
{
    public bool CanHandle(SourceDocument d, byte[] bytes) => d.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
        || d.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
        || d.FileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)
        || d.FileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
        || d.FileName.EndsWith(".eml", StringComparison.OrdinalIgnoreCase);

    public Task<ParseResult> ParseAsync(SourceDocument d, byte[] bytes, CancellationToken ct = default)
    {
        var facts = new List<ExtractedFact>();
        var locations = new List<SourceLocation>();
        string text;
        try { text = Encoding.UTF8.GetString(bytes); }
        catch { return Task.FromResult(new ParseResult(facts, locations, "File is not valid UTF-8 text.", "")); }
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var location = new SourceLocation
            {
                Id = Guid.NewGuid(),
                SourceDocumentId = d.Id,
                LineStart = i + 1,
                LineEnd = i + 1,
                RawCoordinate = $"line {i + 1}"
            };
            locations.Add(location);
            if (line.Length > 2000) line = line[..2000];
            var lot = Regex.Match(line, @"\b([A-Za-z]{1,3}[- ]?[0OIL0-9]{3,8})\b");
            var qty = Regex.Match(line, @"(?<![\d-])(\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)\s*(?:,\s*)?(kg|g|units?|pcs|l|litres?)\b", RegexOptions.IgnoreCase);
            facts.Add(new ExtractedFact
            {
                WorkspaceId = d.WorkspaceId,
                SourceDocumentId = d.Id,
                SourceLocationId = location.Id,
                FactType = FactTypeGuesser.Guess(d.FileName, line),
                ClaimType = "source line",
                ClaimSubject = lot.Success ? lot.Groups[1].Value : d.FileName,
                ClaimObject = string.Empty,
                RawValue = line,
                NormalizedValue = IdentifierNormalizer.NormalizeLot(lot.Success ? lot.Groups[1].Value : string.Empty),
                EvidenceText = line,
                Quantity = qty.Success ? IdentifierNormalizer.ParseQuantity(qty.Groups[1].Value) : null,
                QuantityUnit = qty.Success ? qty.Groups[2].Value.ToLowerInvariant() : null,
                EventDate = ExtractDate(line),
                ConfidenceNote = "Extracted by structured document parser; human review required.",
                Confidence = 0.70,
                ProviderMetadata = "StructuredParser"
            });
        }
        return Task.FromResult(new ParseResult(facts, locations, lines.Length == 0 ? "No text lines." : null, text));
    }

    private static DateTime? ExtractDate(string line)
    {
        var m = Regex.Match(line, @"\b(\d{4}-\d{2}-\d{2}|\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\b");
        return m.Success ? IdentifierNormalizer.ParseDate(m.Value) : null;
    }
}

public sealed class PdfDocumentParser : IDocumentParser
{
    public bool CanHandle(SourceDocument d, byte[] bytes) => d.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<ParseResult> ParseAsync(SourceDocument d, byte[] bytes, CancellationToken ct = default)
    {
        var facts = new List<ExtractedFact>();
        var locations = new List<SourceLocation>();
        var extracted = new System.Text.StringBuilder();
        try
        {
            using var pdf = PdfDocument.Open(bytes);
            if (pdf.NumberOfPages == 0) return Task.FromResult(new ParseResult(facts, locations, "PDF has no pages.", ""));
            foreach (var page in pdf.GetPages())
            {
                var text = page.Text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text)) extracted.AppendLine(text);
                var location = new SourceLocation { Id = Guid.NewGuid(), SourceDocumentId = d.Id, PageNumber = page.Number, RawCoordinate = $"page {page.Number}" };
                locations.Add(location);
                if (string.IsNullOrWhiteSpace(text))
                {
                    var scanFact = new ExtractedFact
                    {
                        WorkspaceId = d.WorkspaceId,
                        SourceDocumentId = d.Id,
                        SourceLocationId = location.Id,
                        FactType = FactType.Other,
                        ClaimType = "scan unreadable",
                        ClaimSubject = d.FileName,
                        RawValue = "[no embedded text on this page]",
                        NormalizedValue = string.Empty,
                        EvidenceText = "Document retained - content not readable. No embedded text was extracted. OCR is not available, so the extraction model cannot analyse this document in the current configuration. You can retain it for manual review or provide a readable text version.",
                        ConfidenceNote = "Unreadable scan page; no extracted facts.",
                        Confidence = 0.10,
                        ProviderMetadata = "StructuredParser"
                    };
                    facts.Add(scanFact);
                    continue;
                }
                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var qty = Regex.Match(line, @"(?<![\d-])(\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)\s*(kg|g|units?|pcs|l|litres?)\b", RegexOptions.IgnoreCase);
                    var lot = Regex.Match(line, @"\b([A-Za-z]{1,3}[- ]?[0OIL0-9]{3,8})\b");
                    facts.Add(new ExtractedFact
                    {
                        WorkspaceId = d.WorkspaceId,
                        SourceDocumentId = d.Id,
                        SourceLocationId = location.Id,
                        FactType = FactTypeGuesser.Guess(d.FileName, line),
                        ClaimType = "pdf line",
                        ClaimSubject = lot.Success ? lot.Groups[1].Value : $"page {page.Number}",
                        RawValue = line,
                        NormalizedValue = IdentifierNormalizer.NormalizeLot(lot.Success ? lot.Groups[1].Value : string.Empty),
                        EvidenceText = line,
                        Quantity = qty.Success ? IdentifierNormalizer.ParseQuantity(qty.Groups[1].Value) : null,
                        QuantityUnit = qty.Success ? qty.Groups[2].Value.ToLowerInvariant() : null,
                        EventDate = TextDocumentParserExtensions.FromPdfDate(line),
                        ConfidenceNote = "Extracted from PDF document text; human review required.",
                        Confidence = 0.75,
                        ProviderMetadata = "StructuredParser"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ParseResult(facts, locations, $"PDF read failed: {ex.Message}. Scan retained.", ""));
        }
        return Task.FromResult(new ParseResult(facts, locations, facts.Count == 0 ? "PDF text unavailable or unreadable." : null, extracted.ToString()));
    }
}

public sealed class ExcelDocumentParser : IDocumentParser
{
    public bool CanHandle(SourceDocument d, byte[] bytes) => d.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || d.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);

    public Task<ParseResult> ParseAsync(SourceDocument d, byte[] bytes, CancellationToken ct = default)
    {
        var facts = new List<ExtractedFact>();
        var locations = new List<SourceLocation>();
        var extracted = new System.Text.StringBuilder();
        try
        {
            using var stream = new System.IO.MemoryStream(bytes, writable: false);
            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            foreach (var ws in workbook.Worksheets)
            {
                foreach (var row in ws.RowsUsed())
                {
                    var cells = row.Cells().Select(c => c.GetString().Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    if (cells.Count == 0) continue;
                    var text = string.Join(" | ", cells);
                    extracted.AppendLine(text);
                    var location = new SourceLocation { Id = Guid.NewGuid(), SourceDocumentId = d.Id, SheetName = ws.Name, RowNumber = row.RowNumber(), RawCoordinate = $"{ws.Name}!row {row.RowNumber()}" };
                    locations.Add(location);
                    var lot = Regex.Match(text, @"\b([A-Za-z]{1,3}[- ]?[0OIL0-9]{3,8})\b");
                    var qty = Regex.Match(text, @"(?<![\d-])(\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)\s*(kg|g|units?|pcs|l|litres?)\b", RegexOptions.IgnoreCase);
                    facts.Add(new ExtractedFact
                    {
                        WorkspaceId = d.WorkspaceId,
                        SourceDocumentId = d.Id,
                        SourceLocationId = location.Id,
                        FactType = FactTypeGuesser.Guess(d.FileName, text),
                        ClaimType = "spreadsheet row",
                        ClaimSubject = lot.Success ? lot.Groups[1].Value : ws.Name,
                        RawValue = text,
                        NormalizedValue = IdentifierNormalizer.NormalizeLot(lot.Success ? lot.Groups[1].Value : string.Empty),
                        EvidenceText = text,
                        Quantity = qty.Success ? IdentifierNormalizer.ParseQuantity(qty.Groups[1].Value) : null,
                        QuantityUnit = qty.Success ? qty.Groups[2].Value.ToLowerInvariant() : null,
                        EventDate = IdentifierNormalizer.ParseDate(text),
                        ConfidenceNote = "Extracted from spreadsheet row; human review required.",
                        Confidence = 0.75,
                        ProviderMetadata = "StructuredParser"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ParseResult(facts, locations, $"Excel read failed: {ex.Message}. File retained.", ""));
        }
        return Task.FromResult(new ParseResult(facts, locations, facts.Count == 0 ? "No rows." : null, extracted.ToString()));
    }
}

public sealed class ImageDocumentParser : IDocumentParser
{
    public bool CanHandle(SourceDocument d, byte[] bytes) => d.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
        || d.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || d.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || d.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || d.FileName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
        || d.FileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

    public Task<ParseResult> ParseAsync(SourceDocument d, byte[] bytes, CancellationToken ct = default)
    {
        var fact = new ExtractedFact
        {
            WorkspaceId = d.WorkspaceId,
            SourceDocumentId = d.Id,
            FactType = FactType.Other,
            ClaimType = "image retained",
            ClaimSubject = d.FileName,
            RawValue = "[binary image; no embedded text]",
            NormalizedValue = string.Empty,
            EvidenceText = "Document retained - content not readable. No embedded text was extracted. OCR is not available, so the extraction model cannot analyse this document in the current configuration. You can retain it for manual review or provide a readable text version.",
            ConfidenceNote = "No parser text available for image.",
            Confidence = 0.05,
            ProviderMetadata = "ImageRetentionParser"
        };
        return Task.FromResult(new ParseResult(new List<ExtractedFact> { fact }, new List<SourceLocation>(), "Image retained; OCR is not available in this configuration.", ""));
    }
}

public static class TextDocumentParserExtensions
{
    public static DateTime? FromPdfDate(string line) => IdentifierNormalizer.ParseDate(line);
}
