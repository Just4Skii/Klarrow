namespace RecallGraph.Application;

public class WorkspaceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDemo { get; set; }
    /// <summary>Workspace mode from persisted runtime state: "demo" (seeded synthetic workspace) or "pilot" (operator records).</summary>
    public string Mode { get; set; } = "pilot";
    /// <summary>Documents whose provenance is the seeded synthetic fixture.</summary>
    public int SyntheticDocumentCount { get; set; }
    /// <summary>True when this workspace holds seeded synthetic evidence (demo workspace or mixed pilot workspace).</summary>
    public bool HasSyntheticData { get; set; }
    /// <summary>Investigation the UI should open by default, chosen from persisted provenance (seeded evidence first, else most recently updated).</summary>
    public Guid? PrimaryInvestigationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DocumentCount { get; set; }
    public int FactCount { get; set; }
    public int UnreviewedFactCount { get; set; }
    public int TraceRunCount { get; set; }
    public int OpenGapCount { get; set; }
}

public class SourceLineDto
{
    public int LineNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class SourceContentDto
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = string.Empty;
    public string ParserName { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public string Locator { get; set; } = string.Empty;
    public string Status { get; set; } = "unavailable";
    public string Reason { get; set; } = string.Empty;
    public bool LocationResolved { get; set; }
    public int? HighlightLine { get; set; }
    public List<SourceLineDto> Lines { get; set; } = new();
}

public class SourceLocationDto
{
    public Guid Id { get; set; }
    public int? PageNumber { get; set; }
    public string? SheetName { get; set; }
    public int? RowNumber { get; set; }
    public string? CellReference { get; set; }
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }
    public string? RawCoordinate { get; set; }
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public Guid? InvestigationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string IssuingEntity { get; set; } = string.Empty;
    public string DocumentReference { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = string.Empty;
    public string ParserName { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public string ProcessingError { get; set; } = string.Empty;
    public bool IsOriginal { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? TransactionDate { get; set; }
    public KnowledgeSource SourceKind { get; set; }
    public int FactCount { get; set; }
    public string PreviewText { get; set; } = string.Empty;
    public List<SourceLocationDto> Locations { get; set; } = new();
}

public class FactDto
{
    public Guid Id { get; set; }
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentName { get; set; } = string.Empty;
    public string SourceLocation { get; set; } = string.Empty;
    public Guid? SourceLocationId { get; set; }
    public FactType FactType { get; set; }
    public string ClaimType { get; set; } = string.Empty;
    public string ClaimSubject { get; set; } = string.Empty;
    public string ClaimObject { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectValue { get; set; } = string.Empty;
    public string Predicate { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectValue { get; set; } = string.Empty;
    public string ValidationOutcome { get; set; } = string.Empty;
    public string ExtractionMethod { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string ReviewReason { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty;
    public string EvidenceText { get; set; } = string.Empty;
    public string SourceQuote { get; set; } = string.Empty;
    public string SourceMatchMethod { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? QuantityUnit { get; set; }
    public DateTime? EventDate { get; set; }
    public ReviewStatus ReviewStatus { get; set; }
    public string Reviewer { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public string ConfidenceNote { get; set; } = string.Empty;
    public double? Confidence { get; set; }
    public string ProviderMetadata { get; set; } = string.Empty;
    /// <summary>Provenance of the source document this claim came from (seeded fixture, imported file, manual entry or demo parser).</summary>
    public KnowledgeSource SourceKind { get; set; } = KnowledgeSource.ImportedFile;
}

public class FactHistoryDto
{
    public Guid Id { get; set; }
    public Guid ExtractedFactId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PreviousValuesJson { get; set; } = "{}";
    public string NewValuesJson { get; set; } = "{}";
    public string Reviewer { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class FactPatchDto
{
    public string? RawValue { get; set; }
    public string? NormalizedValue { get; set; }
    public string? ClaimSubject { get; set; }
    public string? ClaimObject { get; set; }
    public string? ClaimType { get; set; }
    public decimal? Quantity { get; set; }
    public string? QuantityUnit { get; set; }
    public DateTime? EventDate { get; set; }
    public string? Reason { get; set; }
    public string Reviewer { get; set; } = "reviewer";
}

public class ReviewDecisionDto
{
    public string Reviewer { get; set; } = "reviewer";
    public string? Notes { get; set; }
}

public class LotOptionDto
{
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public bool IsSuspect { get; set; }
    public List<string> NearMatches { get; set; } = new();
}

public class TraceRunRequestDto
{
    public Guid? InvestigationId { get; set; }
    public string SelectedType { get; set; } = "raw-lot";
    public string SelectedId { get; set; } = string.Empty;
    public string Direction { get; set; } = "both";
    public string Reviewer { get; set; } = "reviewer";
    public string? TimingNote { get; set; }
}

public class InvestigationDto
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string SuspectLotType { get; set; } = string.Empty;
    public string SuspectLotId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int TraceRunCount { get; set; }
}

public class InvestigationDeleteResultDto
{
    public bool Deleted { get; set; }
    public Guid InvestigationId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int DocumentsDeleted { get; set; }
    public int FactsDeleted { get; set; }
    public int ReviewEventsDeleted { get; set; }
    public int TraceLinksDeleted { get; set; }
    public int TraceRunsDeleted { get; set; }
    public int FindingsDeleted { get; set; }
    public int FilesDeleted { get; set; }
    /// <summary>Known boundaries of the closest safe deletion this prototype can perform.</summary>
    public List<string> Limitations { get; set; } = new();
}

public class SaveInvestigationDto
{
    public string Name { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string SuspectLotType { get; set; } = string.Empty;
    public string SuspectLotId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string Actor { get; set; } = "operator";
}

public class TraceNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public LinkState? IncomingState { get; set; }
}

public class TraceEdgeDto
{
    public Guid Id { get; set; }
    public string FromType { get; set; } = string.Empty;
    public string FromId { get; set; } = string.Empty;
    public string ToType { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string LinkKind { get; set; } = string.Empty;
    public LinkState State { get; set; }
    public decimal? Quantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public DateTime? EventDate { get; set; }
    public string EvidenceText { get; set; } = string.Empty;
    public string SourceReferences { get; set; } = "[]";
    public string ConfidenceNote { get; set; } = string.Empty;
    public string DetectionMethod { get; set; } = string.Empty;
    public string Origin { get; set; } = "legacy-manual";
    public string Reason { get; set; } = string.Empty;
    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public string ReviewReason { get; set; } = string.Empty;
    public bool NeedsReReview { get; set; }
    public bool Superseded { get; set; }
    public string StaleReason { get; set; } = string.Empty;
    public List<Guid> SupportingFactIds { get; set; } = new();
}

public class RelationshipProposalDto
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? InvestigationId { get; set; }
    public string FromType { get; set; } = string.Empty;
    public string FromId { get; set; } = string.Empty;
    public string ToType { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string LinkKind { get; set; } = string.Empty;
    public LinkState State { get; set; }
    public decimal? Quantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public DateTime? EventDate { get; set; }
    public string EvidenceText { get; set; } = string.Empty;
    public string SourceReferences { get; set; } = "[]";
    public string ConfidenceNote { get; set; } = string.Empty;
    public string DetectionMethod { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public string ReviewReason { get; set; } = string.Empty;
    public bool NeedsReReview { get; set; }
    public bool Superseded { get; set; }
    public string StaleReason { get; set; } = string.Empty;
    public List<Guid> SupportingFactIds { get; set; } = new();
    /// <summary>True only when every supporting fact has a human review decision. Relationship review is separate from fact review.</summary>
    public bool SupportingFactsReviewed { get; set; }
    /// <summary>Number of supporting facts still unreviewed. Confirming a relationship does not review them.</summary>
    public int UnreviewedSupportingFactCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GenerateRelationshipsRequestDto
{
    public Guid? InvestigationId { get; set; }
}

public class GenerateRelationshipsResultDto
{
    public int GeneratedCount { get; set; }
    /// <summary>Legacy compatibility field: number of fact-to-candidate-key hits, not distinct links.</summary>
    public int ExistingCount { get; set; }
    public int ExistingLinkCount { get; set; }
    public int ExistingFactKeyHitCount { get; set; }
    public int SkippedUngroundedCount { get; set; }
    public List<RelationshipProposalDto> Proposals { get; set; } = new();
}

public class TraceGapDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public Guid? SourceDocumentId { get; set; }
}

public class MassBalanceDto
{
    public string Status { get; set; } = "insufficient";
    public decimal? InputTotal { get; set; }
    public string InputUnit { get; set; } = string.Empty;
    public decimal? OutputTotal { get; set; }
    public string OutputUnit { get; set; } = string.Empty;
    public decimal? Waste { get; set; }
    public string Notes { get; set; } = string.Empty;
    public Dictionary<string, decimal?> Components { get; set; } = new();
    /// <summary>Why the balance cannot be treated as a conclusion (empty only when it was computed from complete confirmed evidence).</summary>
    public List<string> Limitations { get; set; } = new();
}

public class PossiblePathStepDto
{
    public Guid EdgeId { get; set; }
    public string FromType { get; set; } = string.Empty;
    public string FromId { get; set; } = string.Empty;
    public string ToType { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string LinkKind { get; set; } = string.Empty;
    public LinkState State { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool NeedsReReview { get; set; }
}

public class PossiblePathDto
{
    public string EndKind { get; set; } = string.Empty;
    public string EndId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<PossiblePathStepDto> Steps { get; set; } = new();
    public List<Guid> UncertainEdgeIds { get; set; } = new();
}

public class TraceResultDto
{
    public Guid RunId { get; set; }
    public Guid? InvestigationId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string SelectedType { get; set; } = string.Empty;
    public string SelectedId { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public List<TraceNodeDto> Nodes { get; set; } = new();
    public List<TraceEdgeDto> Edges { get; set; } = new();
    public List<TraceGapDto> Gaps { get; set; } = new();
    public List<GapFindingDto> Findings { get; set; } = new();
    public List<AuditEventDto> AuditTrail { get; set; } = new();
    /// <summary>Finished lots reached through Confirmed relationships only. Possible exposure is listed separately.</summary>
    public List<string> AffectedFinishedLots { get; set; } = new();
    /// <summary>Customers reached through Confirmed relationships only.</summary>
    public List<string> AffectedCustomers { get; set; } = new();
    public List<string> UnresolvedLots { get; set; } = new();
    public Dictionary<string, int> StateCounts { get; set; } = new();
    public MassBalanceDto MassBalance { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    /// <summary>Legacy pre-v2 disclaimer field kept for reading stored snapshots; new runs populate <see cref="Disclaimer"/>.</summary>
    public string DemoDisclaimer { get; set; } = string.Empty;
    public string? TimingNote { get; set; }

    /// <summary>1 = legacy snapshot (scope certainty not recorded); 2 = confirmed and possible scope recorded separately.</summary>
    public int SnapshotVersion { get; set; } = 1;
    public string WorkspaceMode { get; set; } = string.Empty;
    public string Disclaimer { get; set; } = string.Empty;
    /// <summary>True when at least one traversed link originates from the seeded synthetic fixture.</summary>
    public bool ContainsSyntheticEvidence { get; set; }
    public string InvestigationName { get; set; } = string.Empty;
    public string InvestigationReference { get; set; } = string.Empty;
    public bool ConfirmedScopeEstablished { get; set; }
    public List<string> PossibleFinishedLots { get; set; } = new();
    public List<string> PossibleCustomers { get; set; } = new();
    public List<PossiblePathDto> PossiblePaths { get; set; } = new();
    public List<Guid> UndecidedRelationshipIds { get; set; } = new();
    public List<string> Caveats { get; set; } = new();
}

public class TraceRunSummaryDto
{
    public Guid Id { get; set; }
    public DateTime CompletedAt { get; set; }
    public string SelectedType { get; set; } = string.Empty;
    public string SelectedId { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int GapCount { get; set; }
    public string MassBalanceStatus { get; set; } = string.Empty;
}

public class MatchProposalDto
{
    public Guid Id { get; set; }
    public string StableKey { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string LeftValue { get; set; } = string.Empty;
    public string RightValue { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string ScoreKind { get; set; } = string.Empty;
    public string SignalsJson { get; set; } = "[]";
    public string State { get; set; } = string.Empty;
    public string DecidedBy { get; set; } = string.Empty;
    public DateTime? DecidedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EntityAliasDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string CanonicalValue { get; set; } = string.Empty;
    public string AliasValue { get; set; } = string.Empty;
    public Guid? SourceDocumentId { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MatchDecisionDto
{
    public string Actor { get; set; } = "operator";
    public string? Reason { get; set; }
}

public class GapFindingDto
{
    public Guid Id { get; set; }
    public string StableKey { get; set; } = string.Empty;
    public Guid? InvestigationId { get; set; }
    public Guid? TraceRunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string RaisedByCheck { get; set; } = string.Empty;
    public string RelatedEntityType { get; set; } = string.Empty;
    public string RelatedEntityId { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "[]";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AuditEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public class AiProviderStatusDto
{
    public bool IsConfigured { get; set; }
    public string Mode { get; set; } = "unconfigured";
    public string Provider { get; set; } = "none";
    public string ModelId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SetupInstructions { get; set; } = string.Empty;
    public DateTime? LastSuccessfulRunAt { get; set; }
    public string LastSuccessfulModelId { get; set; } = string.Empty;
    public bool HasLiveExtractedFacts { get; set; }
}

public class ImportResultDto
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = "imported";
    public bool IsDuplicate { get; set; }
    public int FactCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SeedResultDto
{
    public bool Reset { get; set; }
    public string WorkspaceId { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public int FactCount { get; set; }
    public int LinkCount { get; set; }
    public string EvidencePath { get; set; } = string.Empty;
}

public class OllamaHealthDto
{
    public bool Configured { get; set; }
    public bool Reachable { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public List<string> InstalledModels { get; set; } = new();
    public string Error { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class LlamaSmokeResultDto
{
    public bool Configured { get; set; }
    public bool Success { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public int FactsReturned { get; set; }
    public string? Error { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class ExtractionRunResultDto
{
    public Guid ExtractionRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsDemo { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public int DocumentsProcessed { get; set; }
    public int FactsCreated { get; set; }
    public string? Error { get; set; }
}

public class CapabilityStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ExecutionMode { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public bool RealInferenceExecuted { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string LicenseRegister { get; set; } = string.Empty;
    public string Setup { get; set; } = string.Empty;
}
