using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class DocumentService(RecallGraphDbContext db, IOptions<StorageOptions> storageOptions, AuditService audit)
{
    private readonly StorageOptions opts = storageOptions.Value;

    public async Task<List<DocumentDto>> ListAsync(Guid workspaceId, Guid? investigationId = null, CancellationToken ct = default)
    {
        return await db.SourceDocuments
            .Where(x => x.WorkspaceId == workspaceId && (investigationId == null || x.InvestigationId == investigationId))
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => new DocumentDto
            {
                Id = x.Id,
                InvestigationId = x.InvestigationId,
                FileName = x.FileName,
                SizeBytes = x.SizeBytes,
                MimeType = x.MimeType,
                DocumentType = x.DocumentType,
                IssuingEntity = x.IssuingEntity,
                DocumentReference = x.DocumentReference,
                ProcessingStatus = x.ProcessingStatus,
                ParserName = x.ParserName,
                ParserVersion = x.ParserVersion,
                ProcessingError = x.ProcessingError,
                IsOriginal = x.IsOriginal,
                Sha256 = x.Sha256,
                UploadedAt = x.UploadedAt,
                TransactionDate = x.TransactionDate,
                SourceKind = x.SourceKind,
                FactCount = x.Facts.Count,
                PreviewText = "",
                Locations = x.Locations.Select(l => new SourceLocationDto { Id = l.Id, PageNumber = l.PageNumber, SheetName = l.SheetName, RowNumber = l.RowNumber, CellReference = l.CellReference, LineStart = l.LineStart, LineEnd = l.LineEnd, RawCoordinate = l.RawCoordinate }).ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<DocumentDto?> GetAsync(Guid workspaceId, Guid id, CancellationToken ct = default)
    {
        var d = await db.SourceDocuments.Include(x => x.Facts).Include(x => x.Locations).FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct);
        if (d == null) return null;
        var preview = GetPreviewText(d.StoredPath, d.MimeType);
        return new DocumentDto
        {
            Id = d.Id,
            InvestigationId = d.InvestigationId,
            FileName = d.FileName,
            SizeBytes = d.SizeBytes,
            MimeType = d.MimeType,
            DocumentType = d.DocumentType,
            IssuingEntity = d.IssuingEntity,
            DocumentReference = d.DocumentReference,
            ProcessingStatus = d.ProcessingStatus,
            ParserName = d.ParserName,
            ParserVersion = d.ParserVersion,
            ProcessingError = d.ProcessingError,
            IsOriginal = d.IsOriginal,
            Sha256 = d.Sha256,
            UploadedAt = d.UploadedAt,
            TransactionDate = d.TransactionDate,
            SourceKind = d.SourceKind,
            FactCount = d.Facts.Count,
            PreviewText = preview,
            Locations = d.Locations.Select(l => new SourceLocationDto { Id = l.Id, PageNumber = l.PageNumber, SheetName = l.SheetName, RowNumber = l.RowNumber, CellReference = l.CellReference, LineStart = l.LineStart, LineEnd = l.LineEnd, RawCoordinate = l.RawCoordinate }).ToList()
        };
    }

    public async Task<ImportResultDto> UploadAsync(Guid workspaceId, string originalFileName, string contentType, byte[] bytes, bool seedMode, Guid? investigationId, CancellationToken ct = default)
    {
        ValidateFileName(originalFileName);
        if (bytes.Length == 0) throw new InvalidOperationException("Uploaded file is empty.");
        if (bytes.Length > opts.MaxUploadBytes) throw new InvalidOperationException($"Uploaded file exceeds maximum size of {opts.MaxUploadBytes} bytes.");
        var safeExt = Path.GetExtension(originalFileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".csv", ".txt", ".pdf", ".xlsx", ".xls", ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tsv", ".log", ".eml" };
        if (!allowed.Contains(safeExt)) throw new InvalidOperationException("Unsupported file type.");

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!seedMode)
        {
            var duplicate = await db.SourceDocuments.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Sha256 == hash, ct);
            if (duplicate != null)
            {
                await audit.AddAsync(workspaceId, "document_duplicate_skipped", "reviewer", $"Duplicate content matched existing document {duplicate.FileName}; no new document created.", "SourceDocument", duplicate.Id.ToString(), $"{{\"sha256\":\"{hash}\"}}", ct);
                return new ImportResultDto
                {
                    DocumentId = duplicate.Id,
                    FileName = duplicate.FileName,
                    Status = "duplicate",
                    IsDuplicate = true,
                    FactCount = 0,
                    Message = $"Duplicate content already imported as {duplicate.FileName}. No new document was created."
                };
            }
        }

        var uploads = Path.GetFullPath(opts.UploadsPath);
        Directory.CreateDirectory(uploads);
        var storedName = $"{Guid.NewGuid():N}{safeExt}";
        var storedPath = Path.Combine(uploads, storedName);
        await System.IO.File.WriteAllBytesAsync(storedPath, bytes, ct);

        var doc = new SourceDocument
        {
            WorkspaceId = workspaceId,
            InvestigationId = investigationId,
            FileName = Path.GetFileName(originalFileName),
            StoredPath = storedPath,
            Sha256 = hash,
            SizeBytes = bytes.Length,
            MimeType = string.IsNullOrWhiteSpace(contentType) ? GuessMime(safeExt) : contentType,
            SourceKind = seedMode ? KnowledgeSource.SeededFixture : KnowledgeSource.ImportedFile
        };
        db.SourceDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        var parser = PickParser(doc, bytes);
        var result = await parser.ParseAsync(doc, bytes, ct);
        foreach (var location in result.Locations) { location.SourceDocumentId = doc.Id; db.SourceLocations.Add(location); }
        foreach (var fact in result.Facts) { fact.SourceDocumentId = doc.Id; fact.WorkspaceId = workspaceId; db.ExtractedFacts.Add(fact); }
        doc.DocumentType = GuessDocumentType(doc.FileName);
        doc.ParserName = parser.GetType().Name;
        doc.ParserVersion = "1.0";
        if (!string.IsNullOrWhiteSpace(result.ExtractedText))
        {
            var derivedDir = Path.Combine(Path.GetFullPath(opts.UploadsPath), "derived");
            Directory.CreateDirectory(derivedDir);
            var derivedPath = Path.Combine(derivedDir, $"{doc.Id:N}.txt");
            await System.IO.File.WriteAllTextAsync(derivedPath, result.ExtractedText, ct);
            doc.DerivedTextPath = derivedPath;
        }
        var isImage = doc.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var hasNoText = string.IsNullOrWhiteSpace(result.ExtractedText);
        doc.ProcessingStatus = isImage || (hasNoText && result.Facts.Any(f => f.ClaimType == "scan unreadable"))
            ? "retained_not_ocr_processed"
            : result.Error == null ? "parsed" : "parse_failed";
        doc.ProcessingError = result.Error ?? string.Empty;
        await db.SaveChangesAsync(ct);

        if (!seedMode) await audit.AddAsync(workspaceId, "document_imported", "reviewer", $"Imported {doc.FileName} ({bytes.Length} bytes).", "SourceDocument", doc.Id.ToString(), $"{{\"sha256\":\"{hash}\"}}", ct);

        return new ImportResultDto
        {
            DocumentId = doc.Id,
            FileName = doc.FileName,
            Status = doc.ProcessingStatus,
            FactCount = result.Facts.Count,
            Message = result.Error ?? $"{result.Facts.Count} candidate facts extracted."
        };
    }

    public async Task<SourceContentDto?> GetSourceContentAsync(Guid workspaceId, Guid documentId, Guid? investigationId, string? locator, int context, CancellationToken ct = default)
    {
        var doc = await db.SourceDocuments.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == documentId, ct);
        if (doc == null) return null;
        if (investigationId.HasValue && doc.InvestigationId != investigationId.Value) return null;

        var dto = new SourceContentDto
        {
            DocumentId = doc.Id,
            FileName = doc.FileName,
            ProcessingStatus = doc.ProcessingStatus,
            ParserName = doc.ParserName,
            ParserVersion = doc.ParserVersion,
            Locator = locator ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(doc.ProcessingError) && doc.ProcessingStatus == "parse_failed")
        {
            dto.Status = "processing_failed";
            dto.Reason = "This document could not be parsed, so there is no extracted text to preview. The original file is retained.";
            return dto;
        }
        if (doc.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || doc.ProcessingStatus == "retained_not_ocr_processed")
        {
            dto.Status = "ocr_not_available";
            dto.Reason = "The file is stored but not read. Text recognition for images and scans is not available yet.";
            return dto;
        }

        var derivedRoot = Path.GetFullPath(Path.Combine(opts.UploadsPath, "derived"));
        string? resolvedPath = null;
        var expectedPath = Path.Combine(derivedRoot, $"{doc.Id:N}.txt");
        if (File.Exists(expectedPath) && IsWithinRoot(derivedRoot, expectedPath)) resolvedPath = expectedPath;
        if (resolvedPath == null && !string.IsNullOrWhiteSpace(doc.DerivedTextPath))
        {
            var stored = SafeFullPath(doc.DerivedTextPath);
            if (stored != null && IsWithinRoot(derivedRoot, stored) && File.Exists(stored)) resolvedPath = stored;
        }
        if (resolvedPath == null)
        {
            dto.Status = "content_missing";
            dto.Reason = "Derived text is not available for this document. The original file is retained.";
            return dto;
        }
        if (new FileInfo(resolvedPath).Length > 2_000_000)
        {
            dto.Status = "too_large";
            dto.Reason = "The extracted text for this document is too large to preview safely.";
            return dto;
        }

        if (string.IsNullOrWhiteSpace(locator))
        {
            dto.Status = "locator_required";
            dto.Reason = "A line locator is required to preview extracted text.";
            return dto;
        }

        var match = System.Text.RegularExpressions.Regex.Match(locator, @"(?:lines?|L)\s*(\d+)(?:\s*[\--]\s*(\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            dto.Status = "unsupported_locator";
            dto.Reason = "Only line locators (for example 'lines 41-41') can be previewed from extracted text. Page, sheet and row locators need a format-specific preview that is not available in this build.";
            return dto;
        }

        var allLines = await System.IO.File.ReadAllLinesAsync(resolvedPath, ct);
        var startLine = int.Parse(match.Groups[1].Value);
        if (startLine < 1 || startLine > allLines.Length)
        {
            dto.Status = "invalid_locator";
            dto.Reason = $"Line {startLine} is outside the extracted text, which has {allLines.Length} lines.";
            return dto;
        }
        var window = Math.Clamp(context, 0, 50);
        var first = Math.Max(1, startLine - window);
        var last = Math.Min(allLines.Length, startLine + window);
        for (var i = first; i <= last; i++)
        {
            var text = allLines[i - 1];
            if (text.Length > 500) text = text[..500];
            dto.Lines.Add(new SourceLineDto { LineNumber = i, Text = text });
        }
        dto.Status = "resolved";
        dto.LocationResolved = true;
        dto.HighlightLine = startLine;
        return dto;
    }

    private static string? SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return null; }
    }

    private static bool IsWithinRoot(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new InvalidOperationException("File name is required.");
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidOperationException("Invalid file name.");
        if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\")) throw new InvalidOperationException("Path traversal is not allowed.");
    }

    private IDocumentParser PickParser(SourceDocument d, byte[] bytes)
    {
        var candidate = (IDocumentParser)new PdfDocumentParser();
        if (candidate.CanHandle(d, bytes)) return candidate;
        candidate = new ExcelDocumentParser();
        if (candidate.CanHandle(d, bytes)) return candidate;
        candidate = new ImageDocumentParser();
        if (candidate.CanHandle(d, bytes)) return candidate;
        return new TextDocumentParser();
    }

    private static string GuessMime(string ext) => ext switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".bmp" => "image/bmp",
        ".xlsx" or ".xls" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "text/plain"
    };

    private static string GuessDocumentType(string fileName)
    {
        var lowered = fileName.ToLowerInvariant();
        if (lowered.Contains("delivery") || lowered.Contains("dn")) return "supplier_delivery";
        if (lowered.Contains("grn") || lowered.Contains("received")) return "goods_receipt";
        if (lowered.Contains("stock") || lowered.Contains("ledger")) return "stock_ledger";
        if (lowered.Contains("batch") || lowered.Contains("production")) return "production_batch";
        if (lowered.Contains("rework")) return "rework_entry";
        if (lowered.Contains("dispatch")) return "dispatch";
        if (lowered.Contains("finished") || lowered.Contains("pack")) return "finished_lot";
        if (lowered.Contains("quality") || lowered.Contains("lab")) return "quality_test";
        return "unclassified";
    }

    private static string GetPreviewText(string path, string mime)
    {
        if (!System.IO.File.Exists(path)) return "[missing file]";
        if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return "[binary image]";
        var info = new FileInfo(path);
        if (info.Length > 256 * 1024) return "[file too large for inline preview]";
        try
        {
            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages()) { if (sb.Length > 4000) break; sb.AppendLine(page.Text); }
                return sb.ToString();
            }
            return System.IO.File.ReadAllText(path);
        }
        catch { return "[unable to preview]"; }
    }
}
