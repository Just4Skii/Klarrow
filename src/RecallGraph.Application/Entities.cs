using System.Text.Json;

namespace RecallGraph.Application;

public enum ReviewStatus
{
    Unreviewed,
    Confirmed,
    Edited,
    Rejected
}

public enum LinkState
{
    Confirmed,
    Candidate,
    Inferred,
    Gap,
    Rejected
}

public enum TraceDirection
{
    Backward,
    Forward,
    Both
}

public enum FactType
{
    SupplierDelivery,
    GoodsReceipt,
    StockLedger,
    ProductionBatch,
    ReworkEntry,
    FinishedLot,
    Dispatch,
    QualityTest,
    Invoice,
    Note,
    Other
}

public enum EntityType
{
    Supplier,
    RawMaterialLot,
    ProductionBatch,
    FinishedLot,
    Customer,
    Location
}

public enum KnowledgeSource
{
    SeededFixture,
    ImportedFile,
    ManualEntry,
    DemoParser
}

public class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDemo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SourceDocument> SourceDocuments { get; set; } = new();
}

public class Investigation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string SuspectLotType { get; set; } = string.Empty;
    public string SuspectLotId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string CreatedBy { get; set; } = "operator";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SourceLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceDocumentId { get; set; }
    public int? PageNumber { get; set; }
    public string? SheetName { get; set; }
    public int? RowNumber { get; set; }
    public string? CellReference { get; set; }
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }
    public string? RawCoordinate { get; set; }
}

public class SourceDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid? InvestigationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string DerivedTextPath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "unknown";
    public string IssuingEntity { get; set; } = string.Empty;
    public string DocumentReference { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = "pending";
    public string ParserName { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public string ProcessingError { get; set; } = string.Empty;
    public bool IsOriginal { get; set; } = true;
    public Guid? SupersededByDocumentId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TransactionDate { get; set; }
    public KnowledgeSource SourceKind { get; set; } = KnowledgeSource.ImportedFile;
    public List<SourceLocation> Locations { get; set; } = new();
    public List<ExtractedFact> Facts { get; set; } = new();
}

public class ExtractedFact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid SourceDocumentId { get; set; }
    public Guid? SourceLocationId { get; set; }
    public FactType FactType { get; set; } = FactType.Other;
    public string RawValue { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty;
    public string EvidenceText { get; set; } = string.Empty;
    public string ClaimType { get; set; } = string.Empty;
    public string ClaimSubject { get; set; } = string.Empty;
    public string ClaimObject { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectValue { get; set; } = string.Empty;
    public string Predicate { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectValue { get; set; } = string.Empty;
    public string? QuantityText { get; set; }
    public decimal? Quantity { get; set; }
    public string? QuantityUnit { get; set; }
    public DateTime? EventDate { get; set; }
    public DateTime? TransactionDate { get; set; }
    public string ValidationOutcome { get; set; } = "unvalidated";
    public string ExtractionMethod { get; set; } = "deterministic-parser";
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Unreviewed;
    public string Reviewer { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public string ReviewReason { get; set; } = string.Empty;
    public string ConfidenceNote { get; set; } = string.Empty;
    public double? Confidence { get; set; }
    public string ProviderMetadata { get; set; } = string.Empty;
    /// <summary>Verbatim text the claim cites as its source (row/line text or a model-supplied quote).</summary>
    public string SourceQuote { get; set; } = string.Empty;
    /// <summary>How the quote was tied to the source: structured-row, exact, normalized, ambiguous or none.</summary>
    public string SourceMatchMethod { get; set; } = string.Empty;
    public Guid? ExtractionRunId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class FactReviewEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid ExtractedFactId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PreviousValuesJson { get; set; } = "{}";
    public string NewValuesJson { get; set; } = "{}";
    public string Reviewer { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class RawMaterialLot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTime? ReceivedDate { get; set; }
    public decimal? Quantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class ProductionBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime? ProductionDate { get; set; }
    public string Line { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal? InputQuantity { get; set; }
    public string InputUnit { get; set; } = string.Empty;
    public decimal? OutputQuantity { get; set; }
    public string OutputUnit { get; set; } = string.Empty;
    public decimal? Waste { get; set; }
    public string WasteUnit { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class FinishedLot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? ProductionBatchId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public DateTime? PackDate { get; set; }
    public string Status { get; set; } = "available";
    public string Notes { get; set; } = string.Empty;
}

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class TraceLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid? InvestigationId { get; set; }
    public string FromType { get; set; } = string.Empty;
    public string FromId { get; set; } = string.Empty;
    public string ToType { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string LinkKind { get; set; } = string.Empty;
    public LinkState State { get; set; } = LinkState.Candidate;
    public decimal? Quantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public DateTime? EventDate { get; set; }
    public string EvidenceText { get; set; } = string.Empty;
    public string SourceReferencesJson { get; set; } = "[]";
    public string ConfidenceNote { get; set; } = string.Empty;
    public string DetectionMethod { get; set; } = string.Empty;
    /// <summary>seeded-demo, generated-from-evidence, or legacy-manual (rows created before provenance tracking).</summary>
    public string Origin { get; set; } = "legacy-manual";
    public string SupportingFactIdsJson { get; set; } = "[]";
    public string Reason { get; set; } = string.Empty;
    public string ConflictNote { get; set; } = string.Empty;
    public string StableKey { get; set; } = string.Empty;
    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public string ReviewReason { get; set; } = string.Empty;
    public bool NeedsReReview { get; set; }
    /// <summary>True when the supporting evidence was withdrawn or replaced; a superseded link is never traversed.</summary>
    public bool Superseded { get; set; }
    public string StaleReason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class TraceRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid? InvestigationId { get; set; }
    public string SelectedType { get; set; } = string.Empty;
    public string SelectedId { get; set; } = string.Empty;
    public TraceDirection Direction { get; set; } = TraceDirection.Both;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "completed";
    public string ResultJson { get; set; } = "{}";
    public string GapsJson { get; set; } = "[]";
    public string MassBalanceJson { get; set; } = "{}";
    public string? TimingNote { get; set; }
}

public class EntityAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string CanonicalValue { get; set; } = string.Empty;
    public string AliasValue { get; set; } = string.Empty;
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourceLocationId { get; set; }
    public Guid? ClaimId { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MatchProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string StableKey { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string LeftValue { get; set; } = string.Empty;
    public string RightValue { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string ScoreKind { get; set; } = "heuristic";
    public string SignalsJson { get; set; } = "[]";
    public string State { get; set; } = "Candidate";
    public string DecidedBy { get; set; } = string.Empty;
    public DateTime? DecidedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class GapFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid? InvestigationId { get; set; }
    public Guid? TraceRunId { get; set; }
    public string StableKey { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string Title { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string RaisedByCheck { get; set; } = string.Empty;
    public string RelatedEntityType { get; set; } = string.Empty;
    public string RelatedEntityId { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "[]";
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Actor { get; set; } = "reviewer";
    public string Description { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AiExtractionRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Provider { get; set; } = "none";
    public string ModelId { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = "1.0";
    public string SchemaVersion { get; set; } = "1.0";
    public string Status { get; set; } = "planned";
    public string RequestSummary { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int DocumentCount { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
