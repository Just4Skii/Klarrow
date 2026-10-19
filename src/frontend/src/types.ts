export type ReviewStatus = "Unreviewed" | "Confirmed" | "Edited" | "Rejected";
export type KnowledgeSource = "SeededFixture" | "ImportedFile" | "ManualEntry" | "DemoParser";
export type WorkspaceMode = "demo" | "pilot";
export type LinkState = "Confirmed" | "Candidate" | "Inferred" | "Gap";

export interface WorkspaceDto {
  id: string;
  name: string;
  scenario: string;
  description: string;
  isDemo: boolean;
  mode: WorkspaceMode;
  syntheticDocumentCount: number;
  hasSyntheticData: boolean;
  primaryInvestigationId?: string;
  createdAt: string;
  documentCount: number;
  factCount: number;
  unreviewedFactCount: number;
  traceRunCount: number;
  openGapCount: number;
}

export interface SourceLineDto {
  lineNumber: number;
  text: string;
}

export interface SourceContentDto {
  documentId: string;
  fileName: string;
  processingStatus: string;
  parserName: string;
  parserVersion: string;
  locator: string;
  status: string;
  reason: string;
  locationResolved: boolean;
  highlightLine?: number;
  lines: SourceLineDto[];
}

export interface SourceLocationDto {
  id: string;
  pageNumber?: number;
  sheetName?: string;
  rowNumber?: number;
  cellReference?: string;
  lineStart?: number;
  lineEnd?: number;
  rawCoordinate?: string;
}

export interface DocumentDto {
  id: string;
  fileName: string;
  sizeBytes: number;
  mimeType: string;
  documentType: string;
  issuingEntity: string;
  documentReference: string;
  processingStatus: string;
  parserName: string;
  parserVersion: string;
  processingError: string;
  isOriginal: boolean;
  sha256: string;
  uploadedAt: string;
  transactionDate?: string;
  sourceKind: KnowledgeSource;
  factCount: number;
  previewText: string;
  locations: SourceLocationDto[];
}

export interface FactDto {
  id: string;
  sourceDocumentId: string;
  sourceDocumentName: string;
  sourceLocation: string;
  sourceLocationId?: string;
  factType: string;
  claimType: string;
  claimSubject: string;
  claimObject: string;
  rawValue: string;
  normalizedValue: string;
  evidenceText: string;
  quantity?: number;
  quantityUnit?: string;
  eventDate?: string;
  reviewStatus: ReviewStatus;
  reviewer: string;
  reviewedAt?: string;
  confidenceNote: string;
  confidence?: number;
  providerMetadata: string;
  extractionMethod: string;
  validationOutcome: string;
  provider: string;
  modelId: string;
  promptVersion: string;
  reviewReason: string;
  sourceKind: KnowledgeSource;
}

export interface FactHistoryDto {
  id: string;
  extractedFactId: string;
  action: string;
  previousValuesJson: string;
  newValuesJson: string;
  reviewer: string;
  reason: string;
  createdAt: string;
}

export interface LotOptionDto {
  kind: string;
  id: string;
  code: string;
  product: string;
  isSuspect: boolean;
  nearMatches: string[];
}

export interface TraceEdgeDto {
  id: string;
  fromType: string;
  fromId: string;
  toType: string;
  toId: string;
  linkKind: string;
  state: LinkState;
  quantity?: number;
  quantityUnit: string;
  eventDate?: string;
  evidenceText: string;
  sourceReferences: string;
  confidenceNote: string;
  detectionMethod: string;
}

export interface TraceGapDto {
  title: string;
  description: string;
  recommendation: string;
  sourceDocumentId?: string;
}

export interface MassBalanceDto {
  status: string;
  inputTotal?: number;
  inputUnit: string;
  outputTotal?: number;
  outputUnit: string;
  waste?: number;
  notes: string;
  components: Record<string, number | null>;
}

export interface MatchProposalDto {
  id: string;
  stableKey: string;
  entityType: string;
  leftValue: string;
  rightValue: string;
  score: number;
  scoreKind: string;
  signalsJson: string;
  state: string;
  decidedBy: string;
  decidedAt?: string;
  reason: string;
  createdAt: string;
  updatedAt: string;
}

export interface EntityAliasDto {
  id: string;
  entityType: string;
  canonicalValue: string;
  aliasValue: string;
  sourceDocumentId?: string;
  confirmedBy: string;
  createdAt: string;
}

export interface RelationshipProposalDto {
  id: string;
  workspaceId: string;
  investigationId?: string;
  fromType: string;
  fromId: string;
  toType: string;
  toId: string;
  linkKind: string;
  state: string;
  quantity?: number;
  quantityUnit: string;
  eventDate?: string;
  evidenceText: string;
  sourceReferences: string;
  confidenceNote: string;
  detectionMethod: string;
  origin: string;
  reason: string;
  reviewedBy: string;
  reviewedAt?: string;
  reviewReason: string;
  needsReReview: boolean;
  superseded: boolean;
  staleReason: string;
  supportingFactIds: string[];
  supportingFactsReviewed: boolean;
  unreviewedSupportingFactCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface GapFindingDto {
  id: string;
  stableKey: string;
  investigationId?: string;
  traceRunId?: string;
  kind: string;
  severity: string;
  title: string;
  explanation: string;
  scope: string;
  raisedByCheck: string;
  relatedEntityType: string;
  relatedEntityId: string;
  evidenceJson: string;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface InvestigationDto {
  id: string;
  workspaceId: string;
  name: string;
  reference: string;
  suspectLotType: string;
  suspectLotId: string;
  description: string;
  status: string;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  traceRunCount: number;
}

export interface TraceResultDto {
  runId: string;
  investigationId?: string;
  startedAt: string;
  completedAt: string;
  selectedType: string;
  selectedId: string;
  direction: string;
  nodes: { id: string; kind: string; label: string; detail: string; incomingState?: LinkState }[];
  edges: TraceEdgeDto[];
  gaps: TraceGapDto[];
  findings: GapFindingDto[];
  auditTrail: AuditEventDto[];
  affectedFinishedLots: string[];
  affectedCustomers: string[];
  unresolvedLots: string[];
  stateCounts: Record<string, number>;
  massBalance: MassBalanceDto;
  summary: string;
  demoDisclaimer: string;
  workspaceMode: WorkspaceMode;
  disclaimer: string;
  containsSyntheticEvidence: boolean;
  timingNote?: string;
}

export interface AuditEventDto {
  id: string;
  eventType: string;
  actor: string;
  description: string;
  targetType: string;
  targetId: string;
  createdAt: string;
  payloadJson: string;
}

export interface OllamaHealthDto {
  configured: boolean;
  reachable: boolean;
  baseUrl: string;
  modelId: string;
  installedModels: string[];
  error: string;
  checkedAt: string;
}

export interface LlamaSmokeResultDto {
  configured: boolean;
  success: boolean;
  provider: string;
  modelId: string;
  factsReturned: number;
  error?: string;
  checkedAt: string;
}

export interface AiProviderStatusDto {
  isConfigured: boolean;
  mode: string;
  provider: string;
  modelId: string;
  endpoint: string;
  message: string;
  setupInstructions: string;
  lastSuccessfulRunAt?: string;
  lastSuccessfulModelId: string;
  hasLiveExtractedFacts: boolean;
}

export interface ImportResultDto {
  documentId: string;
  fileName: string;
  status: string;
  factCount: number;
  message: string;
}

export interface SeedResultDto {
  reset: boolean;
  workspaceId: string;
  documentCount: number;
  factCount: number;
  linkCount: number;
  evidencePath: string;
}

export interface CapabilityStatusDto {
  id: string;
  name: string;
  version: string;
  executionMode: string;
  availability: string;
  realInferenceExecuted: boolean;
  detail: string;
  licenseRegister: string;
  setup: string;
}

export interface AccessControlDto {
  mode: string;
  notice: string;
}

export interface HealthDto {
  status: string;
  time: string;
  service: string;
  accessControl: AccessControlDto;
}

export interface InvestigationDeleteResultDto {
  deleted: boolean;
  investigationId: string;
  reference: string;
  documentsDeleted: number;
  factsDeleted: number;
  reviewEventsDeleted: number;
  traceLinksDeleted: number;
  traceRunsDeleted: number;
  findingsDeleted: number;
  filesDeleted: number;
  limitations: string[];
}
