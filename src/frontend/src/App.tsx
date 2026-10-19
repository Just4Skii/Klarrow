import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { api, publicDemoMode } from "./api";
import type {
  AiProviderStatusDto,
  AuditEventDto,
  CapabilityStatusDto,
  DocumentDto,
  EntityAliasDto,
  FactDto,
  FactHistoryDto,
  GapFindingDto,
  HealthDto,
  InvestigationDto,
  LlamaSmokeResultDto,
  LotOptionDto,
  MatchProposalDto,
  OllamaHealthDto,
  RelationshipProposalDto,
  ReviewStatus,
  SourceContentDto,
  TraceEdgeDto,
  TraceResultDto,
  WorkspaceDto
} from "./types";
import Icon, { type IconName } from "./components/Icon";
import {
  CopyButton,
  EmptyState,
  LoadingPanel,
  MetricTile,
  QuantityDisplay,
  SeverityTag,
  SourceLocatorChip,
  StateBadge,
  StatusBadge,
  WarningBanner,
  formatLocatorLabel,
  useFocusTrap,
  type Severity,
  type Tone,
  type TraceState
} from "./components/primitives";
import {
  DeskClaimEmpty,
  DeskStateBadge,
  HistoryList,
  LotDiffBadge,
  MatchComparison,
  ProvenanceRibbon,
  ReviewActionGroup,
  ShortcutHelpBody
} from "./components/review";
import {
  buildFactPatch,
  draftFromFact,
  entityTypeLabel,
  extractionLabel,
  formatAuditDescription,
  formatAuditEventType,
  formatAuditTargetType,
  formatCheckName,
  formatClaimType,
  formatCleanValue,
  formatFactType,
  formatFindingKind,
  formatLinkKind,
  formatShortDate,
  formatShortId,
  formatValidationOutcome,
  hasSourceLocator,
  reasonRequiredForConfirm,
  type EditDraft
} from "./reviewLogic";

// Public-demo build flag (see api.ts publicDemoMode): hide upload affordances when set.
// The API also enforces upload rejection server-side when Demo__PublicMode is enabled.

type Tab =
  | "home"
  | "investigations"
  | "system"
  | "evidence"
  | "claims"
  | "review"
  | "matches"
  | "trace"
  | "findings"
  | "audit"
  | "export";

const GLOBAL_NAV: { id: Tab; label: string; icon: IconName }[] = [
  { id: "home", label: "Command Center", icon: "home" },
  { id: "investigations", label: "Investigations", icon: "file" },
  { id: "system", label: "System & AI", icon: "info" }
];

const INVESTIGATION_NAV: { id: Tab; label: string; icon: IconName }[] = [
  { id: "home", label: "Overview", icon: "home" },
  { id: "evidence", label: "Evidence", icon: "inbox" },
  { id: "claims", label: "Claims", icon: "list" },
  { id: "review", label: "Review Desk", icon: "check-circle" },
  { id: "matches", label: "Matches", icon: "branch" },
  { id: "trace", label: "Traceability", icon: "trace" },
  { id: "findings", label: "Findings", icon: "alert-diamond" },
  { id: "audit", label: "Audit Trail", icon: "audit" },
  { id: "export", label: "Export Pack", icon: "external" }
];

const ACCEPTED_FORMATS = ".pdf,.csv,.txt,.tsv,.log,.eml,.xlsx,.xls,.png,.jpg,.jpeg,.gif,.bmp";
const MAX_UPLOAD_MB = 10;
const DIRECTION_LABEL: Record<string, string> = {
  backward: "Backward (Upstream Suppliers)",
  forward: "Forward (Downstream Customers)",
  both: "Bidirectional (Full Chain)"
};

function formatDocumentType(docType: string | null | undefined): string {
  const raw = (docType || "").trim().toLowerCase();
  switch (raw) {
    case "supplier_delivery": return "Supplier Delivery";
    case "goods_receipt": return "Goods Receipt Note";
    case "stock_ledger": return "Stock Ledger";
    case "production_batch": return "Production Batch";
    case "rework_entry": return "Rework Log";
    case "dispatch": return "Customer Dispatch";
    case "finished_lot": return "Finished Lot Record";
    case "quality_test": return "Laboratory Analysis";
    case "unclassified":
    case "":
      return "General Evidence";
    default:
      return raw.replace(/[_-]+/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
  }
}

function formatParserLabel(parserName?: string, parserVersion?: string): string {
  if (!parserName) return "Standard Parser";
  const cleaned = parserName
    .replace(/DocumentParser$/i, " Parser")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .trim();
  return parserVersion ? `${cleaned} v${parserVersion}` : cleaned;
}

function documentStatusLabel(status: string): { label: string; tone: Tone; hint: string } {
  if (status === "parsed") {
    return {
      label: "Processed",
      tone: "positive",
      hint: "Structured text and candidate claims were extracted. Original file is preserved with its SHA-256 record."
    };
  }
  if (status === "retained_not_ocr_processed") {
    return {
      label: "Retained (No OCR)",
      tone: "warning",
      hint: "Binary image or scan retained as visual evidence; optical character recognition is not enabled."
    };
  }
  if (status === "parse_failed") {
    return {
      label: "Parse Error",
      tone: "critical",
      hint: "The parser encountered an error reading this file. Original file is retained."
    };
  }
  if (status === "pending") {
    return {
      label: "Pending",
      tone: "neutral",
      hint: "Queued for document parsing."
    };
  }
  return {
    label: status ? status.replace(/[_-]+/g, " ") : "Unknown",
    tone: "neutral",
    hint: "Document processing status."
  };
}

function parseEvidenceJson(json: string): Record<string, unknown> | null {
  try {
    const value: unknown = JSON.parse(json);
    return value && typeof value === "object" && !Array.isArray(value) ? (value as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

function formatScopeLabel(scope: string | null | undefined): string {
  if (!scope) return "Workspace";
  return scope
    .replace(/^raw-lot\s+/i, "Raw Lot ")
    .replace(/^finished-lot\s+/i, "Finished Lot ")
    .replace(/^production-batch\s+/i, "Batch ")
    .replace(/^workspace$/i, "All Investigation Records");
}

function massBalanceView(status: string): { tone: Tone; label: string } {
  if (status === "reconciled") return { tone: "positive", label: "Balanced within tolerance" };
  if (status === "discrepancy") return { tone: "critical", label: "Quantity discrepancy" };
  return { tone: "warning", label: "Unit conversion / additional records needed" };
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDateTime(value?: string): string {
  if (!value) return "Not recorded";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  if (/T00:00:00/.test(value)) {
    return formatShortDate(value);
  }
  return date.toLocaleString("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function isoTitle(value?: string): string {
  if (!value) return "Not recorded";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toISOString();
}

function elapsedLabel(start?: string, end?: string): string {
  if (!start || !end) return "Instant";
  const ms = new Date(end).getTime() - new Date(start).getTime();
  if (Number.isNaN(ms) || ms < 0) return "Instant";
  return ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(2)} s`;
}

function modelSummary(ai: AiProviderStatusDto | null, health: OllamaHealthDto | null, smoke: LlamaSmokeResultDto | null): { label: string; tone: Tone } {
  if (!ai?.isConfigured) return { label: "Offline / Unconfigured", tone: "neutral" };
  if (smoke?.success) return { label: "Live Model Verified", tone: "positive" };
  if (health && !health.reachable) return { label: "Endpoint Unreachable", tone: "critical" };
  if (health?.reachable && health.installedModels.length === 0) return { label: "Model Not Installed", tone: "warning" };
  if (health?.reachable) return { label: "Service Online · Ready", tone: "positive" };
  return { label: "Configured", tone: "info" };
}

function providerView(ai: AiProviderStatusDto | null) {
  if (!ai) return { tone: "neutral" as Tone, label: "Checking", detail: "Reading AI extraction service status…" };
  if (ai.isConfigured) {
    return {
      tone: ai.hasLiveExtractedFacts ? ("positive" as Tone) : ("info" as Tone),
      label: ai.hasLiveExtractedFacts ? "Live AI Active" : "Endpoint Configured",
      detail: ai.hasLiveExtractedFacts
        ? `Live model inference (${ai.lastSuccessfulModelId || ai.modelId}) has generated schema-validated candidate claims${ai.lastSuccessfulRunAt ? ` · Last run ${formatDateTime(ai.lastSuccessfulRunAt)}` : ""}. All AI claims require human verification.`
        : "Language model endpoint is configured and ready to extract structured claims from evidence documents."
    };
  }
  return {
    tone: "neutral" as Tone,
    label: "Parser Only",
    detail: "Using deterministic document parsers. Configure an OpenAI-compatible or Ollama endpoint for live LLM extraction."
  };
}

function edgeTraceState(state: string): TraceState {
  if (state === "Confirmed") return "confirmed";
  if (state === "Candidate") return "candidate";
  if (state === "Inferred") return "inferred";
  if (state === "Gap") return "missing";
  if (state === "Rejected") return "rejected";
  if (state === "Conflict") return "conflict";
  return "unknown";
}

function edgeDash(state: string): string {
  if (state === "Gap") return "3 5";
  if (state === "Candidate") return "7 4";
  if (state === "Inferred") return "2 4";
  return "0";
}

function linkSentence(edge: TraceEdgeDto): string {
  switch (edge.linkKind) {
    case "supplier_delivery":
      return `Supplier ${edge.fromId} delivered raw material lot ${edge.toId}.`;
    case "raw_to_batch":
      return `Raw material lot ${edge.fromId} was issued into production batch ${edge.toId}.`;
    case "batch_to_finished":
      return `Production batch ${edge.fromId} was packaged into finished product lot ${edge.toId}.`;
    case "rework":
    case "rework_input":
      return `Material from ${edge.fromId} was routed into rework batch ${edge.toId}.`;
    case "rework_to_finished":
      return `Finished lot ${edge.toId} incorporated rework material from ${edge.fromId}.`;
    case "customer_dispatch":
    case "finished_to_dispatch":
      return `Finished product lot ${edge.fromId} was dispatched to ${edge.toId}.`;
    case "missing_downstream_dispatch":
    case "missing_dispatch":
      return `Downstream dispatch destination for ${edge.fromId} is unresolved (${edge.toId}).`;
    default:
      return `${edge.fromId} is linked to ${edge.toId} via ${formatLinkKind(edge.linkKind).toLowerCase()}.`;
  }
}

function TraceGraphView({
  trace,
  edges,
  onSelectEdge
}: {
  trace: TraceResultDto;
  edges: TraceEdgeDto[];
  onSelectEdge: (edge: TraceEdgeDto) => void;
}) {
  const NODE_CAP = 40;
  const rootId = trace.selectedId;
  const nodeMetaMap = useMemo(() => {
    const map: Record<string, string> = {};
    for (const n of trace.nodes) {
      map[n.id] = entityTypeLabel(n.kind);
    }
    for (const e of edges) {
      if (!map[e.fromId]) map[e.fromId] = entityTypeLabel(e.fromType);
      if (!map[e.toId]) map[e.toId] = entityTypeLabel(e.toType);
    }
    return map;
  }, [trace.nodes, edges]);

  const allNodeIds = Array.from(new Set<string>([rootId, ...edges.flatMap((e) => [e.fromId, e.toId])]));
  const capped = allNodeIds.length > NODE_CAP;
  const nodeIds = capped ? allNodeIds.slice(0, NODE_CAP) : allNodeIds;

  // Order columns logically along the supply chain when possible
  const supplyChainRank = (id: string): number | null => {
    const type = (nodeMetaMap[id] || "").toLowerCase();
    if (type.includes("supplier")) return 0;
    if (type.includes("raw material")) return 1;
    if (type.includes("batch")) return 2;
    if (type.includes("finished")) return 3;
    if (type.includes("customer") || type.includes("destination") || type.includes("gap")) return 4;
    return null;
  };

  const adjacency: Record<string, string[]> = {};
  for (const e of edges) {
    (adjacency[e.fromId] ??= []).push(e.toId);
    (adjacency[e.toId] ??= []).push(e.fromId);
  }
  const bfsLevel: Record<string, number> = { [rootId]: 0 };
  const queue: string[] = [rootId];
  while (queue.length > 0) {
    const current = queue.shift()!;
    for (const next of adjacency[current] ?? []) {
      if (bfsLevel[next] === undefined) {
        bfsLevel[next] = (bfsLevel[current] ?? 0) + 1;
        queue.push(next);
      }
    }
  }

  const level: Record<string, number> = {};
  for (const id of nodeIds) {
    const scRank = supplyChainRank(id);
    level[id] = scRank !== null ? scRank : (bfsLevel[id] ?? 2);
  }

  const uniqueLevels = Array.from(new Set(Object.values(level))).sort((a, b) => a - b);
  const byLevel: Record<number, string[]> = {};
  for (const id of nodeIds) {
    const colIndex = uniqueLevels.indexOf(level[id]);
    (byLevel[colIndex] ??= []).push(id);
  }
  const levels = Object.keys(byLevel).map(Number).sort((a, b) => a - b);

  const nodeW = 190;
  const nodeH = 54;
  const colWidth = 250;
  const rowHeight = 76;
  const width = Math.max(420, levels.length * colWidth + 40);
  const height = Math.max(180, Math.max(...levels.map((l) => byLevel[l].length)) * rowHeight + 48);
  const position: Record<string, { x: number; y: number }> = {};
  for (const l of levels) {
    const colNodes = byLevel[l];
    const colTotalHeight = colNodes.length * rowHeight;
    const startY = Math.max(24, (height - colTotalHeight) / 2 + 10);
    colNodes.forEach((id, index) => {
      position[id] = { x: 24 + l * colWidth, y: startY + index * rowHeight };
    });
  }

  return (
    <div className="graph-wrap">
      {capped && (
        <p className="cell-meta" role="note">
          Showing {nodeIds.length} of {allNodeIds.length} nodes. Switch to Paths or Table view for the complete list.
        </p>
      )}
      <svg
        className="trace-graph"
        viewBox={`0 0 ${width} ${height}`}
        width="100%"
        height={Math.min(height, 540)}
        role="img"
        aria-label={`Supply chain trace graph with ${nodeIds.length} nodes and ${edges.length} links`}
      >
        <title>Supply chain graph for {rootId}</title>
        {edges
          .filter((e) => nodeIds.includes(e.fromId) && nodeIds.includes(e.toId))
          .map((e) => {
            const a = position[e.fromId];
            const b = position[e.toId];
            if (!a || !b) return null;
            const leftToRight = a.x <= b.x;
            const x1 = leftToRight ? a.x + nodeW : a.x;
            const y1 = a.y + nodeH / 2;
            const x2 = leftToRight ? b.x : b.x + nodeW;
            const y2 = b.y + nodeH / 2;
            const dx = Math.max(40, Math.abs(x2 - x1) * 0.45);
            const c1x = leftToRight ? x1 + dx : x1 - dx;
            const c2x = leftToRight ? x2 - dx : x2 + dx;
            const pathData = `M ${x1} ${y1} C ${c1x} ${y1}, ${c2x} ${y2}, ${x2} ${y2}`;
            return (
              <g key={e.id}>
                <path
                  d={pathData}
                  fill="none"
                  stroke="currentColor"
                  strokeWidth={2.5}
                  strokeDasharray={edgeDash(e.state)}
                  className={`graph-edge graph-${edgeTraceState(e.state)}`}
                  tabIndex={0}
                  role="button"
                  aria-label={`${linkSentence(e)} State ${e.state}.`}
                  onClick={() => onSelectEdge(e)}
                  onKeyDown={(ev) => {
                    if (ev.key === "Enter" || ev.key === " ") {
                      ev.preventDefault();
                      onSelectEdge(e);
                    }
                  }}
                >
                  <title>
                    {linkSentence(e)} ({e.state}) - Click to inspect evidence
                  </title>
                </path>
              </g>
            );
          })}
        {nodeIds.map((id) => {
          const p = position[id];
          const isRoot = id === rootId;
          const kindLabel = nodeMetaMap[id] || "Entity";
          return (
            <g key={id} transform={`translate(${p.x}, ${p.y})`}>
              <rect width={nodeW} height={nodeH} rx={8} className={`graph-node${isRoot ? " graph-root" : ""}`} />
              <text x={12} y={22} className="graph-node-type">
                {kindLabel.length > 26 ? `${kindLabel.slice(0, 25)}…` : kindLabel}
              </text>
              <text x={12} y={40} className="graph-node-label">
                {id.length > 22 ? `${id.slice(0, 21)}…` : id}
              </text>
            </g>
          );
        })}
      </svg>
      <p className="cell-meta">
        Interactive supply-chain graph · Click any connecting line to open the Link Evidence Inspector.
      </p>
      <ul className="visually-hidden">
        {edges.map((e) => (
          <li key={e.id}>
            <button type="button" onClick={() => onSelectEdge(e)}>
              {linkSentence(e)} State {e.state}.
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

const VALID_TABS: Tab[] = [
  "home",
  "evidence",
  "claims",
  "review",
  "matches",
  "trace",
  "findings",
  "audit",
  "export",
  "investigations",
  "system"
];
const CLAIM_STATUSES: Array<"All" | ReviewStatus> = ["All", "Unreviewed", "Confirmed", "Edited", "Rejected"];

function readHashRoute(): Record<string, string> {
  const raw = window.location.hash.replace(/^#\/?/, "");
  const queryIndex = raw.indexOf("?");
  const path = queryIndex < 0 ? raw : raw.slice(0, queryIndex);
  const out: Record<string, string> = { path };
  new URLSearchParams(queryIndex < 0 ? "" : raw.slice(queryIndex + 1)).forEach((v, k) => {
    out[k] = v;
  });
  return out;
}

function writeHashRoute(tab: Tab, params: Record<string, string>) {
  const query = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v) query.set(k, v);
  const hash = `#/${tab}${query.toString() ? `?${query.toString()}` : ""}`;
  if (window.location.hash !== hash) window.history.replaceState(null, "", hash);
}

export default function App() {
  const [tab, setTab] = useState<Tab>("home");
  const [highlightId, setHighlightId] = useState<string | null>(null);
  const routeAppliedRef = useRef(false);
  const [workspace, setWorkspace] = useState<WorkspaceDto | null>(null);
  const [documents, setDocuments] = useState<DocumentDto[]>([]);
  const [documentPreviews, setDocumentPreviews] = useState<Record<string, string>>({});
  const [facts, setFacts] = useState<FactDto[]>([]);
  const [lots, setLots] = useState<LotOptionDto[]>([]);
  const [trace, setTrace] = useState<TraceResultDto | null>(null);
  const [runs, setRuns] = useState<{ id: string; selectedType: string; selectedId: string; completedAt: string }[]>([]);
  const [audits, setAudits] = useState<AuditEventDto[]>([]);
  const [ai, setAi] = useState<AiProviderStatusDto | null>(null);
  const [capabilities, setCapabilities] = useState<CapabilityStatusDto[]>([]);
  const [investigations, setInvestigations] = useState<InvestigationDto[]>([]);
  const [matches, setMatches] = useState<MatchProposalDto[]>([]);
  const [aliases, setAliases] = useState<EntityAliasDto[]>([]);
  const [relationshipLinks, setRelationshipLinks] = useState<RelationshipProposalDto[]>([]);
  const [linkBusyId, setLinkBusyId] = useState<string | null>(null);
  const [linksBusy, setLinksBusy] = useState(false);
  const [findings, setFindings] = useState<GapFindingDto[]>([]);
  const [aiHealth, setAiHealth] = useState<OllamaHealthDto | null>(null);
  const [smoke, setSmoke] = useState<LlamaSmokeResultDto | null>(null);
  const [smokeRunning, setSmokeRunning] = useState(false);
  const [claimAction, setClaimAction] = useState<{ id: string; action: "confirm" | "reject" } | null>(null);
  const [claimReason, setClaimReason] = useState("");
  const [claimPage, setClaimPage] = useState(1);
  const [claimPageSize, setClaimPageSize] = useState(25);
  const [matchView, setMatchView] = useState<"proposals" | "aliases">("proposals");
  const [matchBusyId, setMatchBusyId] = useState<string | null>(null);
  const [deskStatus, setDeskStatus] = useState<"All" | ReviewStatus>("All");
  const [deskSelectedId, setDeskSelectedId] = useState<string | null>(null);
  const [deskAction, setDeskAction] = useState<"confirm" | "edit" | "reject" | null>(null);
  const [deskReason, setDeskReason] = useState("");
  const [deskEdits, setDeskEdits] = useState<EditDraft | null>(null);
  const [deskError, setDeskError] = useState<string | null>(null);
  const [deskBusy, setDeskBusy] = useState(false);
  const [deskHistory, setDeskHistory] = useState<{ loading: boolean; error: string | null; events: FactHistoryDto[] }>({
    loading: false,
    error: null,
    events: []
  });
  const [deskHistoryOpen, setDeskHistoryOpen] = useState(false);
  const [deskHistoryVersion, setDeskHistoryVersion] = useState(0);
  const [deskHelpOpen, setDeskHelpOpen] = useState(false);
  const [deskDecidedCount, setDeskDecidedCount] = useState(0);
  const [deskResult, setDeskResult] = useState<string | null>(null);
  const [deskSlideSeq, setDeskSlideSeq] = useState(0);
  const [deskSheetOpen, setDeskSheetOpen] = useState(false);
  const [deskSource, setDeskSource] = useState<{ loading: boolean; error: string | null; data: SourceContentDto | null }>({
    loading: false,
    error: null,
    data: null
  });
  const [sourceView, setSourceView] = useState<{ open: boolean; loading: boolean; error: string | null; data: SourceContentDto | null }>({
    open: false,
    loading: false,
    error: null,
    data: null
  });
  const [traceView, setTraceView] = useState<"paths" | "graph" | "table">("paths");
  const [expandedEdgeId, setExpandedEdgeId] = useState<string | null>(null);
  const [selectedEdge, setSelectedEdge] = useState<TraceEdgeDto | null>(null);
  const [traceFromHistory, setTraceFromHistory] = useState(false);
  const [stateFilter, setStateFilter] = useState<string[]>([
    "Confirmed",
    "Candidate",
    "Inferred",
    "Rejected",
    "Conflict",
    "Missing",
    "Failed"
  ]);
  const [online, setOnline] = useState(typeof navigator === "undefined" ? true : navigator.onLine);
  const [dataSaver, setDataSaver] = useState(() => typeof localStorage !== "undefined" && localStorage.getItem("recallgraph.dataSaver") === "1");
  const [apiKeyDraft, setApiKeyDraft] = useState("");
  const [selectedInvestigation, setSelectedInvestigation] = useState("");
  const [newInvestigationName, setNewInvestigationName] = useState("");
  const [newInvestigationReference, setNewInvestigationReference] = useState("");
  const [newInvestigationSuspectLot, setNewInvestigationSuspectLot] = useState("");
  const [docQuery, setDocQuery] = useState("");
  const [factQuery, setFactQuery] = useState("");
  const [factStatus, setFactStatus] = useState<"All" | ReviewStatus>("All");
  const [query, setQuery] = useState("");
  const [selectedLot, setSelectedLot] = useState("");
  const [direction, setDirection] = useState("both");
  const [loadingWorkspace, setLoadingWorkspace] = useState(true);
  const [runningTrace, setRunningTrace] = useState(false);
  const [busyFactId, setBusyFactId] = useState<string | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const [requiresKey, setRequiresKey] = useState(false);
  const [apiKeyInput, setApiKeyInput] = useState("");
  const [platformHealth, setPlatformHealth] = useState<HealthDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<InvestigationDto | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const lotSelectRef = useRef<HTMLSelectElement>(null);
  const openerRef = useRef<HTMLElement | null>(null);
  const [selectedFinding, setSelectedFinding] = useState<GapFindingDto | null>(null);
  const [selectedDocument, setSelectedDocument] = useState<DocumentDto | null>(null);
  const documentTrap = useFocusTrap(!!selectedDocument);
  const findingTrap = useFocusTrap(!!selectedFinding);
  const edgeTrap = useFocusTrap(!!selectedEdge);
  const sourceTrap = useFocusTrap(sourceView.open);
  const deskHelpTrap = useFocusTrap(deskHelpOpen);
  const deleteTrap = useFocusTrap(!!deleteTarget);

  const loadAll = async () => {
    try {
      const [w, d, f, l, a, s, c, inv, mp, al, gp, hh, rl] = await Promise.all([
        api.home(),
        api.documents(),
        api.facts(),
        api.lots(),
        api.audits(),
        api.aiStatus(),
        api.capabilities(),
        api.investigations(),
        api.matches(),
        api.aliases(),
        api.gaps(),
        api.aiHealth(),
        api.relationshipLinks()
      ]);
      setWorkspace(w);
      setDocuments(d);
      setFacts(f);
      setLots(l);
      setAudits(a);
      setAi(s);
      setCapabilities(c);
      setInvestigations(inv);
      setMatches(mp);
      setAliases(al);
      setFindings(gp);
      setAiHealth(hh);
      setRelationshipLinks(rl);
      const preferredInvestigation = w.primaryInvestigationId ?? inv[0]?.id;
      setSelectedInvestigation((current) => current || preferredInvestigation || "");
      const recent = await api.traceRuns();
      setRuns(recent.map((r) => ({ id: r.id, selectedType: r.selectedType, selectedId: r.selectedId, completedAt: r.completedAt })));
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      if (/unauthor/i.test(msg)) {
        setRequiresKey(true);
      } else {
        setError(msg);
      }
    } finally {
      setLoadingWorkspace(false);
    }
  };

  const saveApiKey = async () => {
    api.setKey(apiKeyInput.trim());
    setApiKeyInput("");
    setRequiresKey(false);
    setLoadingWorkspace(true);
    await loadAll();
  };

  // eslint-disable-next-line react/set-state-in-effect
  useEffect(() => {
    void loadAll();
  }, []);

  // Public diagnostics: access-control mode banner, available before authentication.
  // eslint-disable-next-line react/set-state-in-effect
  useEffect(() => {
    void (async () => {
      try {
        setPlatformHealth(await api.health());
      } catch {
        /* diagnostics only — banner stays hidden when the API is unreachable */
      }
    })();
  }, []);

  // Sync Hash Route (#/home, #/evidence, etc.) when browser hash changes externally
  useEffect(() => {
    const onHashChange = () => {
      const route = readHashRoute();
      const targetTab = (route.tab || route.path) as Tab;
      if (targetTab && (VALID_TABS as string[]).includes(targetTab)) {
        setTab(targetTab);
      }
    };
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, []);

  // Reload evidence scoped to the selected investigation when it changes.
  // eslint-disable-next-line react/set-state-in-effect
  useEffect(() => {
    let active = true;
    const scope = selectedInvestigation || undefined;
    void (async () => {
      try {
        const [docs, scopedFacts, scopedLinks] = await Promise.all([
          api.documents(scope),
          api.facts(scope),
          api.relationshipLinks(scope)
        ]);
        if (active) {
          setDocuments(docs);
          setFacts(scopedFacts);
          setRelationshipLinks(scopedLinks);
        }
      } catch (e) {
        if (active) setError(e instanceof Error ? e.message : String(e));
      }
    })();
    return () => {
      active = false;
    };
  }, [selectedInvestigation]);

  // Restore a deep link once, after the first successful load.
  // eslint-disable-next-line react/set-state-in-effect
  // eslint-disable-next-line react-hooks/exhaustive-deps
  // eslint-disable-next-line react/immutability
  useEffect(() => {
    if (loadingWorkspace || routeAppliedRef.current) return;
    routeAppliedRef.current = true;
    const route = readHashRoute();
    const initialTab = route.tab || route.path;
    if (initialTab && (VALID_TABS as string[]).includes(initialTab)) setTab(initialTab as Tab);
    if (route.inv && investigations.some((i) => i.id === route.inv)) setSelectedInvestigation(route.inv);
    if (route.claimStatus && (CLAIM_STATUSES as string[]).includes(route.claimStatus)) setFactStatus(route.claimStatus as "All" | ReviewStatus);
    if (route.claimQuery !== undefined) setFactQuery(route.claimQuery);
    if (route.claimPage) {
      const p = parseInt(route.claimPage, 10);
      if (Number.isFinite(p) && p > 0) setClaimPage(p);
    }
    if (route.evidenceQuery !== undefined) setDocQuery(route.evidenceQuery);
    const run = runs.find((r) => r.id === route.run);
    if (route.run && run) {
      void openRun(route.run);
      return;
    }
    const doc = documents.find((d) => d.id === route.doc);
    if (route.doc && doc) {
      setTab("evidence");
      setSelectedDocument(doc);
      void api.document(doc.id).then(
        (full) => setDocumentPreviews((prev) => ({ ...prev, [doc.id]: full.previewText })),
        () => setDocumentPreviews((prev) => ({ ...prev, [doc.id]: "" }))
      );
      return;
    }
    const match = matches.find((m) => m.id === route.match);
    if (route.match && match) {
      setTab("matches");
      setHighlightId(match.id);
      return;
    }
    const finding = findings.find((f) => f.id === route.finding);
    if (route.finding && finding) {
      setTab("findings");
      openFinding(finding);
      return;
    }
    const claim = facts.find((f) => f.id === route.claim);
    if (route.claim && claim) {
      setTab("claims");
      setFactStatus("All");
      setFactQuery("");
      setClaimPage(Math.floor(facts.findIndex((f) => f.id === route.claim) / claimPageSize) + 1);
      setHighlightId(claim.id);
    }
  }, [loadingWorkspace, investigations, runs, documents, matches, findings, facts, claimPageSize]);

  useEffect(() => {
    if (loadingWorkspace) return;
    writeHashRoute(tab, {
      inv: selectedInvestigation,
      run: trace?.runId ?? "",
      claimStatus: tab === "claims" ? factStatus : "",
      claimQuery: tab === "claims" ? factQuery : "",
      claimPage: tab === "claims" ? String(claimPage) : "",
      evidenceQuery: tab === "evidence" ? docQuery : "",
      doc: selectedDocument?.id ?? "",
      match: highlightId && tab === "matches" ? highlightId : "",
      finding: selectedFinding?.id ?? "",
      claim: highlightId && tab === "claims" ? highlightId : ""
    });
  }, [loadingWorkspace, tab, selectedInvestigation, trace, factStatus, factQuery, claimPage, docQuery, selectedDocument, selectedFinding, highlightId]);

  useEffect(() => {
    if (!highlightId) return;
    document.querySelector(`[data-highlight-id="${highlightId}"]`)?.scrollIntoView({ block: "center" });
  }, [highlightId, tab]);

  useEffect(() => {
    const goOnline = () => setOnline(true);
    const goOffline = () => setOnline(false);
    window.addEventListener("online", goOnline);
    window.addEventListener("offline", goOffline);
    return () => {
      window.removeEventListener("online", goOnline);
      window.removeEventListener("offline", goOffline);
    };
  }, []);

  useEffect(() => {
    document.documentElement.classList.toggle("data-saver", dataSaver);
  }, [dataSaver]);

  const runTrace = async () => {
    if (!selectedLot) {
      setError("Select a lot or batch before running a trace.");
      return;
    }
    const lot = lots.find((x) => x.id === selectedLot);
    if (!lot) return;
    setRunningTrace(true);
    setError(null);
    setMessage(null);
    try {
      const result = await api.runTrace({
        investigationId: selectedInvestigation || undefined,
        selectedType: lot.kind,
        selectedId: lot.id,
        direction
      });
      setTrace(result);
      setTraceFromHistory(false);
      setSelectedEdge(null);
      setExpandedEdgeId(null);
      setTab("trace");
      const [updatedRuns, w, a] = await Promise.all([api.traceRuns(), api.home(), api.audits()]);
      setRuns(updatedRuns.map((r) => ({ id: r.id, selectedType: r.selectedType, selectedId: r.selectedId, completedAt: r.completedAt })));
      setWorkspace(w);
      setAudits(a);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setRunningTrace(false);
    }
  };

  const openRun = async (id: string) => {
    try {
      const result = await api.traceRun(id);
      setTrace(result);
      setTraceFromHistory(true);
      setSelectedEdge(null);
      setExpandedEdgeId(null);
      setTab("trace");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const reviewFactWithReason = async (id: string, action: "confirm" | "reject", reason: string) => {
    setBusyFactId(id);
    setError(null);
    try {
      const updated = action === "confirm" ? await api.confirmFact(id, "reviewer", reason) : await api.rejectFact(id, "reviewer", reason);
      setFacts((prev) => prev.map((x) => (x.id === id ? updated : x)));
      const [w, a] = await Promise.all([api.home(), api.audits()]);
      setWorkspace(w);
      setAudits(a);
      setMessage(
        action === "confirm"
          ? `Claim "${updated.claimSubject}" confirmed and logged in the audit trail.`
          : `Claim "${updated.claimSubject}" rejected and logged in the audit trail.`
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusyFactId(null);
    }
  };

  const rememberOpener = () => {
    openerRef.current = document.activeElement as HTMLElement | null;
  };
  const restoreFocus = () => {
    openerRef.current?.focus();
    openerRef.current = null;
  };
  const openEdge = (edge: TraceEdgeDto) => {
    rememberOpener();
    setSelectedEdge(edge);
  };
  const closeEdge = () => {
    setSelectedEdge(null);
    restoreFocus();
  };
  const openFinding = (finding: GapFindingDto) => {
    rememberOpener();
    setSelectedFinding(finding);
  };
  const closeFinding = () => {
    setSelectedFinding(null);
    restoreFocus();
  };

  const loadDocumentPreview = async (doc: DocumentDto) => {
    if (documentPreviews[doc.id] !== undefined) return;
    try {
      const full = await api.document(doc.id);
      setDocumentPreviews((prev) => ({ ...prev, [doc.id]: full.previewText }));
    } catch {
      setDocumentPreviews((prev) => ({ ...prev, [doc.id]: "" }));
    }
  };

  const openSource = async (fact: FactDto) => {
    rememberOpener();
    setSourceView({ open: true, loading: true, error: null, data: null });
    try {
      const data = await api.sourceContent(fact.sourceDocumentId, fact.sourceLocation, selectedInvestigation || undefined);
      setSourceView({ open: true, loading: false, error: null, data });
    } catch (e) {
      setSourceView({ open: true, loading: false, error: e instanceof Error ? e.message : String(e), data: null });
    }
  };

  const closeSource = () => {
    setSourceView({ open: false, loading: false, error: null, data: null });
    restoreFocus();
  };

  const createInvestigation = async () => {
    if (!newInvestigationName.trim()) {
      setError("Investigation name is required.");
      return;
    }
    setError(null);
    try {
      const [suspectKind, suspectId] = newInvestigationSuspectLot.split("|", 2);
      const created = await api.createInvestigation({
        name: newInvestigationName.trim(),
        reference: newInvestigationReference.trim(),
        suspectLotType: suspectKind || "",
        suspectLotId: suspectId || "",
        description: "Operator-created recall investigation.",
        status: "Open",
        actor: "operator"
      });
      setInvestigations((prev) => [created, ...prev]);
      setSelectedInvestigation(created.id);
      setNewInvestigationName("");
      setNewInvestigationReference("");
      setNewInvestigationSuspectLot("");
      setMessage(`Investigation "${created.name}" created and recorded in the audit log.`);
      const [w, a] = await Promise.all([api.home(), api.audits()]);
      setWorkspace(w);
      setAudits(a);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const performDeleteInvestigation = async () => {
    if (!deleteTarget) return;
    setDeleteBusy(true);
    setError(null);
    try {
      const result = await api.deleteInvestigation(deleteTarget.id, "operator");
      const remaining = investigations.filter((x) => x.id !== deleteTarget.id);
      setInvestigations(remaining);
      if (selectedInvestigation === deleteTarget.id) {
        setSelectedInvestigation(remaining[0]?.id ?? "");
      }
      const limitationNote = result.limitations.length > 0 ? ` Deletion boundaries: ${result.limitations.join(" ")}` : "";
      setMessage(
        `Investigation "${result.reference || deleteTarget.name}" deleted - removed ${result.documentsDeleted} document(s), ${result.factsDeleted} claim(s), ${result.reviewEventsDeleted} review decision(s), ${result.traceRunsDeleted} trace run(s), ${result.findingsDeleted} finding(s), ${result.filesDeleted} stored file(s). Recorded in the audit trail.${limitationNote}`
      );
      setDeleteTarget(null);
      const [w, a] = await Promise.all([api.home(), api.audits()]);
      setWorkspace(w);
      setAudits(a);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setDeleteBusy(false);
    }
  };

  const refreshMatches = async () => {
    setError(null);
    try {
      const [mp, al] = await Promise.all([api.matches(true), api.aliases()]);
      setMatches(mp);
      setAliases(al);
      setMessage("Near-match identifier proposals recomputed from active evidence.");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const decideMatchWithReason = async (proposal: MatchProposalDto, action: "confirm" | "reject" | "defer", reason: string): Promise<void> => {
    setMatchBusyId(proposal.id);
    try {
      const updated = await api.decideMatch(proposal.id, action, "reviewer", reason);
      const [mp, al, w, a] = await Promise.all([api.matches(), api.aliases(), api.home(), api.audits()]);
      setMatches(mp);
      setAliases(al);
      setWorkspace(w);
      setAudits(a);
      setMessage(
        action === "confirm"
          ? `Alias confirmed: ${updated.rightValue} is now mapped to canonical lot ${updated.leftValue}.`
          : `Match proposal for ${updated.rightValue} marked as ${updated.state}.`
      );
    } finally {
      setMatchBusyId(null);
    }
  };

  const generateRelationshipLinks = async () => {
    setLinksBusy(true);
    setError(null);
    try {
      const scope = selectedInvestigation || undefined;
      const result = await api.generateRelationshipLinks(scope);
      const links = await api.relationshipLinks(scope);
      setRelationshipLinks(links);
      setMessage(
        `Relationship proposals generated: ${result.generatedCount} new link(s), ${result.existingLinkCount} existing link(s), ${result.skippedUngroundedCount} skipped as ungrounded. (${result.existingFactKeyHitCount} fact-to-link matches were already known.)`
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLinksBusy(false);
    }
  };

  const decideRelationshipLink = async (link: RelationshipProposalDto, action: "confirm" | "reject") => {
    setLinkBusyId(link.id);
    setError(null);
    try {
      const updated = await api.decideRelationshipLink(link.id, action, "reviewer", "");
      const [links, w, a] = await Promise.all([api.relationshipLinks(selectedInvestigation || undefined), api.home(), api.audits()]);
      setRelationshipLinks(links);
      setWorkspace(w);
      setAudits(a);
      setMessage(
        action === "confirm"
          ? `Link confirmed: ${updated.fromId} → ${updated.toId} (${updated.linkKind.replace(/_/g, " ")}) now counts toward trace scope.`
          : `Link rejected: ${updated.fromId} → ${updated.toId} will not be traced.`
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLinkBusyId(null);
    }
  };

  const openDeskAction = useCallback(
    (action: "confirm" | "edit" | "reject") => {
      const fact = facts.find((f) => f.id === deskSelectedId);
      if (!fact) return;
      setDeskAction(action);
      setDeskError(null);
      setDeskReason("");
      setDeskResult(null);
      setDeskEdits(action === "edit" ? draftFromFact(fact) : null);
    },
    [facts, deskSelectedId]
  );

  const closeDeskAction = useCallback(() => {
    setDeskAction(null);
    setDeskError(null);
    setDeskEdits(null);
    setDeskReason("");
  }, []);

  const moveDesk = useCallback(
    (delta: number) => {
      const queue = facts.filter((f) => deskStatus === "All" || f.reviewStatus === deskStatus);
      if (queue.length === 0) return;
      const index = queue.findIndex((f) => f.id === deskSelectedId);
      const nextIndex = Math.min(Math.max((index < 0 ? 0 : index) + delta, 0), queue.length - 1);
      const next = queue[nextIndex];
      if (next && next.id !== deskSelectedId) {
        setDeskSelectedId(next.id);
        setDeskSlideSeq((s) => s + 1);
      }
    },
    [facts, deskStatus, deskSelectedId]
  );

  const submitDeskDecision = async () => {
    const fact = facts.find((f) => f.id === deskSelectedId);
    if (!fact || !deskAction || deskBusy) return;
    const reasonRequired = deskAction === "reject" || deskAction === "edit" || (deskAction === "confirm" && reasonRequiredForConfirm(fact));
    if (reasonRequired && !deskReason.trim()) {
      setDeskError(
        deskAction === "reject"
          ? "A reason is required to reject this claim."
          : deskAction === "edit"
            ? "A reason is required to save an edit."
            : "This claim has no source locator, so a verification note is required to confirm it."
      );
      return;
    }
    let patch: Record<string, unknown> | null = null;
    if (deskAction === "edit") {
      if (!deskEdits) return;
      if (!deskEdits.claimSubject.trim() || !deskEdits.rawValue.trim()) {
        setDeskError("Subject and extracted value cannot be empty.");
        return;
      }
      const built = buildFactPatch(fact, deskEdits);
      if (built.quantityInvalid) {
        setDeskError("Quantity must be a valid number.");
        return;
      }
      if (!built.hasChanges) {
        setDeskError("No field values have changed. Adjust a value or cancel the edit.");
        return;
      }
      patch = built.patch;
    }
    setDeskBusy(true);
    setDeskError(null);
    try {
      let updated: FactDto;
      if (deskAction === "confirm") updated = await api.confirmFact(fact.id, "reviewer", deskReason);
      else if (deskAction === "reject") updated = await api.rejectFact(fact.id, "reviewer", deskReason);
      else updated = (await api.patchFact(fact.id, { ...patch, reason: deskReason.trim(), reviewer: "reviewer" })) as FactDto;
      setFacts((prev) => prev.map((f) => (f.id === updated.id ? updated : f)));
      const [w, a] = await Promise.all([api.home(), api.audits()]);
      setWorkspace(w);
      setAudits(a);
      setDeskDecidedCount((count) => count + 1);
      setDeskHistoryVersion((version) => version + 1);
      setDeskResult(
        deskAction === "confirm"
          ? `Confirmed "${updated.claimSubject}" and recorded in the audit history.`
          : deskAction === "reject"
            ? `Rejected "${updated.claimSubject}" and recorded in the audit history.`
            : `Updated "${updated.claimSubject}" and preserved previous values in the audit history.`
      );
      setDeskAction(null);
      setDeskEdits(null);
      setDeskReason("");
      moveDesk(1);
    } catch (e) {
      setDeskError(e instanceof Error ? e.message : String(e));
    } finally {
      setDeskBusy(false);
    }
  };

  const refreshFindings = async () => {
    setError(null);
    try {
      const gp = await api.gaps(true);
      setFindings(gp);
      const w = await api.home();
      setWorkspace(w);
      setMessage("Reconciliation findings recomputed from current evidence.");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const runSmoke = async () => {
    setSmokeRunning(true);
    setError(null);
    try {
      const result = await api.aiSmoke();
      setSmoke(result);
      const [status, health] = await Promise.all([api.aiStatus(), api.aiHealth()]);
      setAi(status);
      setAiHealth(health);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setSmokeRunning(false);
    }
  };

  const saveSessionKey = async () => {
    api.setKey(apiKeyDraft.trim());
    setApiKeyDraft("");
    setMessage("API key saved for this browser session.");
    await loadAll();
  };

  const clearSessionKey = async () => {
    api.setKey("");
    setMessage("API key cleared from this browser session.");
    await loadAll();
  };

  const onUpload = async (file: File) => {
    setError(null);
    setMessage(null);
    try {
      const r = await api.upload(file, selectedInvestigation || undefined);
      setMessage(`${r.fileName}: ${r.message}`);
      await loadAll();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const selectedLotData = lots.find((x) => x.id === selectedLot);
  const currentInvestigation = investigations.find((x) => x.id === selectedInvestigation);
  const scopeNote = currentInvestigation
    ? `Active Investigation Scope: ${currentInvestigation.name}${currentInvestigation.reference ? ` (${currentInvestigation.reference})` : ""}`
    : "Workspace-wide view - select an investigation to isolate evidence.";
  const showInvestigationShell = Boolean(selectedInvestigation) && tab !== "investigations" && tab !== "system";
  const nearMatchWarnings = selectedLotData?.nearMatches ?? [];
  const filteredDocuments = useMemo(
    () =>
      documents.filter(
        (d) =>
          !docQuery ||
          d.fileName.toLowerCase().includes(docQuery.toLowerCase()) ||
          formatDocumentType(d.documentType).toLowerCase().includes(docQuery.toLowerCase())
      ),
    [documents, docQuery]
  );
  const factCounts = useMemo(() => {
    const counts: Record<string, number> = { All: facts.length, Unreviewed: 0, Confirmed: 0, Edited: 0, Rejected: 0 };
    for (const f of facts) counts[f.reviewStatus] = (counts[f.reviewStatus] ?? 0) + 1;
    return counts;
  }, [facts]);
  const filteredFacts = useMemo(() => {
    const q = factQuery.toLowerCase();
    return facts.filter((f) => {
      const statusMatch = factStatus === "All" || f.reviewStatus === factStatus;
      const queryMatch =
        !q ||
        f.claimSubject.toLowerCase().includes(q) ||
        f.rawValue.toLowerCase().includes(q) ||
        f.sourceDocumentName.toLowerCase().includes(q) ||
        formatFactType(f.factType).toLowerCase().includes(q);
      return statusMatch && queryMatch;
    });
  }, [facts, factQuery, factStatus]);
  const totalClaimPages = Math.max(1, Math.ceil(filteredFacts.length / claimPageSize));
  const currentClaimPage = Math.min(claimPage, totalClaimPages);
  const pagedFacts = filteredFacts.slice((currentClaimPage - 1) * claimPageSize, currentClaimPage * claimPageSize);
  const claimRangeStart = filteredFacts.length === 0 ? 0 : (currentClaimPage - 1) * claimPageSize + 1;
  const claimRangeEnd = Math.min(filteredFacts.length, currentClaimPage * claimPageSize);
  // eslint-disable-next-line react/set-state-in-effect
  useEffect(() => {
    setClaimPage(1);
  }, [factQuery, factStatus, claimPageSize]);

  const deskQueue = useMemo(() => facts.filter((f) => deskStatus === "All" || f.reviewStatus === deskStatus), [facts, deskStatus]);
  const deskFact = facts.find((f) => f.id === deskSelectedId) ?? null;
  const deskIndex = deskFact ? deskQueue.findIndex((f) => f.id === deskFact.id) : -1;
  const deskReasonRequired =
    deskAction !== null &&
    deskFact !== null &&
    (deskAction === "reject" || deskAction === "edit" || (deskAction === "confirm" && reasonRequiredForConfirm(deskFact)));
  const deskSubmitDisabled =
    deskBusy ||
    !deskFact ||
    !deskAction ||
    (deskReasonRequired && !deskReason.trim()) ||
    (deskAction === "edit" && (!deskEdits || !deskEdits.claimSubject.trim() || !deskEdits.rawValue.trim()));

  const deskQueueList = (
    <ul className="desk-queue-list" aria-label="Claims in this queue">
      {deskQueue.map((f) => (
        <li key={f.id}>
          <button
            type="button"
            data-fact-id={f.id}
            className={`desk-queue-item${f.id === deskSelectedId ? " is-selected" : ""}`}
            aria-current={f.id === deskSelectedId ? "true" : undefined}
            onClick={() => {
              setDeskSelectedId(f.id);
              setDeskSlideSeq((s) => s + 1);
              setDeskSheetOpen(false);
            }}
          >
            <div className="desk-queue-item-top">
              <span className="desk-queue-subject" title={f.claimSubject}>
                {f.claimSubject}
              </span>
              <DeskStateBadge fact={f} />
            </div>
            <span className="cell-meta" title={f.sourceDocumentName}>
              {formatFactType(f.factType)} · {f.sourceDocumentName}
            </span>
          </button>
        </li>
      ))}
    </ul>
  );

  // eslint-disable-next-line react/set-state-in-effect
  useEffect(() => {
    if (tab !== "review") return;
    if (deskSelectedId && deskQueue.some((f) => f.id === deskSelectedId)) return;
    setDeskSelectedId(deskQueue[0]?.id ?? null);
  }, [tab, deskQueue, deskSelectedId]);

  // eslint-disable-next-line react/set-state-in-effect
  useEffect(() => {
    if (tab !== "review" || !deskSelectedId) return;
    let active = true;
    setDeskHistory((prev) => ({ ...prev, loading: true, error: null }));
    void api.factHistory(deskSelectedId).then(
      (events) => {
        if (active) setDeskHistory({ loading: false, error: null, events });
      },
      (e) => {
        if (active) setDeskHistory({ loading: false, error: e instanceof Error ? e.message : String(e), events: [] });
      }
    );
    return () => {
      active = false;
    };
  }, [tab, deskSelectedId, deskHistoryVersion]);

  // eslint-disable-next-line react/set-state-in-effect
  useEffect(() => {
    if (tab !== "review" || !deskFact) return;
    if (!hasSourceLocator(deskFact)) {
      setDeskSource({ loading: false, error: null, data: null });
      return;
    }
    let active = true;
    setDeskSource({ loading: true, error: null, data: null });
    void api.sourceContent(deskFact.sourceDocumentId, deskFact.sourceLocation, selectedInvestigation || undefined).then(
      (data) => {
        if (active) setDeskSource({ loading: false, error: null, data });
      },
      (e) => {
        if (active) setDeskSource({ loading: false, error: e instanceof Error ? e.message : String(e), data: null });
      }
    );
    return () => {
      active = false;
    };
  }, [tab, deskFact, selectedInvestigation]);

  useEffect(() => {
    if (tab !== "review") return;
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      const editable =
        !!target && (target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.tagName === "SELECT" || target.isContentEditable);
      if (event.key === "Escape") {
        if (deskHelpOpen) {
          setDeskHelpOpen(false);
          return;
        }
        if (deskAction) {
          closeDeskAction();
          return;
        }
        return;
      }
      if (deskHelpOpen || deskAction) return;
      if (event.ctrlKey || event.metaKey || event.altKey) return;
      if (editable) return;
      if (event.key === "j" || event.key === "J") {
        event.preventDefault();
        moveDesk(1);
      } else if (event.key === "k" || event.key === "K") {
        event.preventDefault();
        moveDesk(-1);
      } else if (event.key === "c" || event.key === "C") {
        event.preventDefault();
        if (deskFact && deskFact.reviewStatus !== "Confirmed") openDeskAction("confirm");
      } else if (event.key === "e" || event.key === "E") {
        event.preventDefault();
        if (deskFact) openDeskAction("edit");
      } else if (event.key === "r" || event.key === "R") {
        event.preventDefault();
        if (deskFact && deskFact.reviewStatus !== "Rejected") openDeskAction("reject");
      } else if (event.key === "h" || event.key === "H") {
        event.preventDefault();
        setDeskHistoryOpen((open) => !open);
      } else if (event.key === "?") {
        event.preventDefault();
        setDeskHelpOpen(true);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [tab, deskHelpOpen, deskAction, deskFact, deskSelectedId, deskStatus, facts, openDeskAction, closeDeskAction, moveDesk]);

  const filteredLots = useMemo(
    () => lots.filter((x) => !query || x.code.toLowerCase().includes(query.toLowerCase()) || x.product.toLowerCase().includes(query.toLowerCase())),
    [lots, query]
  );
  const lotsByKind = useMemo(() => {
    const groups: Record<string, LotOptionDto[]> = {};
    for (const lot of filteredLots) (groups[lot.kind] ??= []).push(lot);
    return groups;
  }, [filteredLots]);
  const provider = providerView(ai);
  const edgeFacts = (edge: TraceEdgeDto): FactDto[] => {
    try {
      const ids: unknown = JSON.parse(edge.sourceReferences);
      if (!Array.isArray(ids)) return [];
      return facts.filter((f) => ids.includes(f.id));
    } catch {
      return [];
    }
  };
  const traceEdges = trace?.edges ?? [];
  const visibleEdges = traceEdges.filter((e) => stateFilter.includes(e.state));
  const traceFindingsForEdge = (edge: TraceEdgeDto) => findings.filter((f) => f.relatedEntityId === edge.toId || f.relatedEntityId === edge.fromId);
  const candidateMatchCount = matches.filter((m) => m.state === "Candidate").length;
  const visibleLinks = relationshipLinks.filter((l) => !selectedInvestigation || l.investigationId === selectedInvestigation);
  const pendingLinkCount = visibleLinks.filter((l) => l.state === "Candidate").length;
  const openFindingCount = findings.filter((f) => f.status === "Open").length;
  const claimPendingCount = factCounts.Unreviewed ?? 0;
  const openItemsCount = claimPendingCount + candidateMatchCount + openFindingCount;
  const exportOpenItems = openItemsCount;
  const attentionDocumentCount = documents.filter((d) => d.processingStatus === "parse_failed" || d.processingStatus === "retained_not_ocr_processed").length;
  const decisionBreakdown =
    `${claimPendingCount} pending claim${claimPendingCount === 1 ? "" : "s"} + ` +
    `${candidateMatchCount} match proposal${candidateMatchCount === 1 ? "" : "s"} + ` +
    `${openFindingCount} finding${openFindingCount === 1 ? "" : "s"}`;
  const nextAction: { label: string; reason: string; tab: Tab } = !selectedInvestigation
    ? { label: "Select an Investigation", reason: "no active investigation selected", tab: "investigations" }
    : attentionDocumentCount > 0
      ? { label: "Review Evidence Issues", reason: `check ${attentionDocumentCount} evidence document${attentionDocumentCount === 1 ? "" : "s"} needing attention`, tab: "evidence" }
      : candidateMatchCount > 0
        ? { label: "Resolve Lot Matches", reason: `decide ${candidateMatchCount} lot match proposal${candidateMatchCount === 1 ? "" : "s"}`, tab: "matches" }
        : claimPendingCount > 0
          ? { label: "Open Review Desk", reason: `review ${claimPendingCount} pending claim${claimPendingCount === 1 ? "" : "s"}`, tab: "review" }
          : runs.length === 0
            ? { label: "Run Lot Traceability", reason: "no trace run saved for this investigation yet", tab: "trace" }
            : openFindingCount > 0
              ? { label: "Review Open Findings", reason: `resolve ${openFindingCount} unresolved finding${openFindingCount === 1 ? "" : "s"}`, tab: "findings" }
              : { label: "Export Review Pack", reason: "export the reviewed investigation pack", tab: "export" };

  const workspaceMode = workspace?.mode ?? (workspace?.isDemo ? "demo" : "pilot");
  const isSyntheticWorkspace = workspace != null && workspaceMode === "demo";
  const hasSyntheticEvidence = (workspace?.hasSyntheticData ?? false) || isSyntheticWorkspace;

  const navBadgeCount = (navId: Tab): number | null => {
    if (navId === "evidence") return documents.length || null;
    if (navId === "review") return claimPendingCount || null;
    if (navId === "matches") return candidateMatchCount || null;
    if (navId === "findings") return openFindingCount || null;
    if (navId === "audit") return audits.length || null;
    return null;
  };

  return (
    <div className="app">
      <a className="skip-link" href="#main">
        Skip to main content
      </a>
      <header className="app-header">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            <img src="/klarrow-k-64.png" alt="" />
          </span>
          <span className="brand-text">
            <span className="brand-wordmark">Klarrow</span>
            <span className="brand-subtitle">Evidence reconciliation workbench</span>
          </span>
        </div>
        <div className="header-right">
          <nav className="app-nav" aria-label="Primary">
            {GLOBAL_NAV.map((item) => (
              <button
                key={item.id}
                type="button"
                className={`nav-item${tab === item.id ? " is-active" : ""}`}
                aria-current={tab === item.id ? "page" : undefined}
                onClick={() => {
                  setHighlightId(null);
                  setTab(item.id);
                }}
              >
                <Icon name={item.icon} width={17} height={17} />
                <span>{item.label}</span>
              </button>
            ))}
          </nav>
          <div className="header-status">
            <span
              className={`mode-chip${isSyntheticWorkspace ? "" : " mode-pilot"}`}
              title={
                isSyntheticWorkspace
                  ? "This workspace holds seeded synthetic demonstration evidence."
                  : hasSyntheticEvidence
                    ? "Pilot workspace containing seeded synthetic demonstration records."
                    : "Pilot investigation workspace with operator-supplied records."
              }
            >
              <Icon name={isSyntheticWorkspace ? "alert" : "check-circle"} width={14} height={14} />
              {workspace ? (isSyntheticWorkspace ? "Synthetic workspace" : "Pilot workspace") : "Loading"}
            </span>
            {api.hasKey() && (
              <span className="key-chip" title="Session API key active for this browser tab.">
                <Icon name="check-circle" width={14} height={14} /> Session Authenticated
              </span>
            )}
          </div>
        </div>
      </header>

      <div className="pattern-strip" aria-hidden="true" />

      {showInvestigationShell && (
        <div className="investigation-shell">
          <div className="investigation-context">
            <span className="investigation-crumb">
              <span className="crumb-ref">{currentInvestigation?.reference || "CASE"}</span>
            </span>
            <span className="investigation-suspect">
              Suspect Lot: <span className="run-lot">{currentInvestigation?.suspectLotId || "Not assigned"}</span>
            </span>
            <StatusBadge tone={currentInvestigation?.status === "Open" ? "info" : "neutral"}>
              {currentInvestigation?.status || "Active"}
            </StatusBadge>
            <div className="decision-wrap">
              <button
                type="button"
                className={`decision-link${openItemsCount === 0 ? " is-clear" : ""}`}
                aria-describedby="decision-formula"
                onClick={() => setTab(nextAction.tab)}
              >
                {openItemsCount > 0 ? `Needs your decision: ${openItemsCount}` : "No decisions pending"}
              </button>
              <span id="decision-formula" className="decision-formula">{decisionBreakdown}</span>
            </div>
          </div>
          <nav className="investigation-tabs" aria-label="Investigation sections">
            {INVESTIGATION_NAV.map((item) => {
              const count = navBadgeCount(item.id);
              return (
                <button
                  key={item.id}
                  type="button"
                  className={`nav-item${tab === item.id ? " is-active" : ""}`}
                  aria-current={tab === item.id ? "page" : undefined}
                  onClick={() => {
                    setHighlightId(null);
                    setTab(item.id);
                  }}
                >
                  <Icon name={item.icon} width={15} height={15} />
                  <span>{item.label}</span>
                  {count !== null && <span className="nav-badge">{count}</span>}
                </button>
              );
            })}
          </nav>
        </div>
      )}

      <div className="banner-stack">
        {error && (
          <div className="banner banner-error" role="alert">
            <Icon name="alert" width={18} height={18} />
            <span>{error}</span>
            <button type="button" className="banner-action" onClick={() => { setError(null); void loadAll(); }}>
              Retry
            </button>
          </div>
        )}
        {message && (
          <div className="banner banner-success" role="status">
            <Icon name="check" width={18} height={18} />
            <span>{message}</span>
            <button type="button" className="banner-action" onClick={() => setMessage(null)} aria-label="Dismiss message">
              <Icon name="close" width={16} height={16} />
            </button>
          </div>
        )}
        {hasSyntheticEvidence && (
          <div className="synthetic-banner" role="note">
            <Icon name="alert" width={16} height={16} />
            <span>
              {isSyntheticWorkspace
                ? "Synthetic demonstration data active. Not for operational food-safety decisions."
                : `Pilot workspace contains ${workspace?.syntheticDocumentCount ?? 0} seeded synthetic demonstration document(s). Synthetic records are tagged and must not be used for operational decisions.`}
            </span>
          </div>
        )}
        {platformHealth && (
          <div
            className={`warning-banner${platformHealth.accessControl.mode === "none" ? " warning-caution" : " warning-info"}`}
            role="note"
          >
            <Icon name={platformHealth.accessControl.mode === "none" ? "alert" : "check-circle"} width={18} height={18} />
            <div>
              <strong>
                {platformHealth.accessControl.mode === "none"
                  ? "Local prototype mode - API access control not configured"
                  : "API-token access control enabled (single shared key)"}
              </strong>
              {platformHealth.accessControl.notice}
              {platformHealth.accessControl.mode === "token" && " One shared session key per deployment - not per-user accounts, SSO, or role-based access management."}
            </div>
          </div>
        )}
        {!online && (
          <div className="warning-banner warning-critical" role="alert">
            <Icon name="alert-triangle" width={18} height={18} />
            <div>
              <strong>Network connection lost.</strong> Changes cannot be saved until connectivity returns.
            </div>
          </div>
        )}
      </div>

      <main id="main" className="app-main">
        {requiresKey ? (
          <section className="page">
            <div className="panel auth-panel">
              <h1>Workspace authentication required</h1>
              <p className="panel-copy">
                This Klarrow server is protected by an API key. Enter the key provided by your administrator. It is stored only in session memory for this tab.
              </p>
              <label className="field">
                <span>API Key</span>
                <input
                  type="password"
                  value={apiKeyInput}
                  onChange={(e) => setApiKeyInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") void saveApiKey();
                  }}
                  autoFocus
                />
              </label>
              <button type="button" className="button button-primary" disabled={!apiKeyInput.trim()} onClick={saveApiKey}>
                Authenticate Session
              </button>
            </div>
          </section>
        ) : loadingWorkspace ? (
          <LoadingPanel />
        ) : (
          <>
            {tab === "home" && (
              <section className="page page-home" aria-label="Command Center overview">
                <section className="panel hero-card" aria-labelledby="home-heading">
                  <div className="hero-top">
                    <div>
                      <span className="eyebrow">Recall investigation workbench</span>
                      <h1 id="home-heading" className="hero-greeting">
                        {currentInvestigation ? currentInvestigation.name : (workspace?.name ?? "Command Center")}
                      </h1>
                    </div>
                    <div className="hero-actions">
                      <span className="hero-next-hint">Next: {nextAction.reason}</span>
                      <button type="button" className="button button-primary" onClick={() => setTab(nextAction.tab)}>
                        {nextAction.label}
                      </button>
                      {nextAction.tab !== "trace" && (
                        <button type="button" className="button button-secondary" onClick={() => setTab("trace")}>
                          <Icon name="trace" width={16} height={16} /> Run Lot Traceability
                        </button>
                      )}
                    </div>
                  </div>

                  <div className="workflow-stepper" aria-label="Investigation workflow progress">
                    <button type="button" className={`workflow-step ${documents.length > 0 ? (attentionDocumentCount > 0 ? "is-active" : "is-complete") : "is-active"}`} onClick={() => setTab("evidence")}>
                      <span className="step-num">1</span>
                      <div>
                        <strong>Ingest Evidence</strong>
                        <span>
                          {documents.length} file{documents.length === 1 ? "" : "s"} loaded
                          {attentionDocumentCount > 0 ? ` · ${attentionDocumentCount} need${attentionDocumentCount === 1 ? "s" : ""} attention` : ""}
                        </span>
                      </div>
                    </button>
                    <button type="button" className={`workflow-step ${facts.length > 0 && claimPendingCount === 0 ? "is-complete" : "is-active"}`} onClick={() => setTab("review")}>
                      <span className="step-num">2</span>
                      <div>
                        <strong>Verify Claims</strong>
                        <span>{factCounts.Confirmed + factCounts.Edited} verified · {claimPendingCount} pending</span>
                      </div>
                    </button>
                    <button type="button" className={`workflow-step ${matches.length > 0 && candidateMatchCount === 0 ? "is-complete" : "is-active"}`} onClick={() => setTab("matches")}>
                      <span className="step-num">3</span>
                      <div>
                        <strong>Resolve Lot Aliases</strong>
                        <span>{aliases.length} confirmed · {candidateMatchCount} pending</span>
                      </div>
                    </button>
                    <button type="button" className={`workflow-step ${runs.length > 0 ? (openItemsCount > 0 ? "is-active" : "is-complete") : ""}`} onClick={() => setTab("trace")}>
                      <span className="step-num">4</span>
                      <div>
                        <strong>Trace & Export</strong>
                        <span>
                          {runs.length > 0
                            ? openItemsCount > 0
                              ? `${runs.length} trace run${runs.length === 1 ? "" : "s"} · ${openItemsCount} open item${openItemsCount === 1 ? "" : "s"}`
                              : `${runs.length} trace run${runs.length === 1 ? "" : "s"} · no open items`
                            : openItemsCount > 0
                              ? `No trace run yet · ${openItemsCount} open item${openItemsCount === 1 ? "" : "s"}`
                              : "No trace run yet"}
                        </span>
                      </div>
                    </button>
                  </div>
                </section>

                <div className="context-strip">
                  <div>
                    <span className="context-label">Active scenario</span>
                    <p className="context-title">{workspace?.name ?? "Klarrow Workspace"}</p>
                    <p className="context-meta">{workspace?.scenario}</p>
                  </div>
                </div>

                <div className="metrics" aria-label="Workspace overview">
                  <MetricTile label="Evidence files" value={documents.length} hint="SHA-256 recorded" />
                  <MetricTile label="Extracted claims" value={facts.length} hint="Source-linked records" />
                  <MetricTile
                    label="Pending review"
                    value={claimPendingCount}
                    hint="Claims awaiting a decision"
                    tone={claimPendingCount > 0 ? "warning" : "positive"}
                  />
                  <MetricTile label="Trace snapshots" value={runs.length} hint="Auditable trace runs" tone="info" />
                  <MetricTile
                    label="Open findings"
                    value={openFindingCount}
                    hint="Gaps & discrepancies"
                    tone={openFindingCount > 0 ? "warning" : "positive"}
                  />
                </div>

                <div className="home-grid">
                  <div className="home-main">
                    <section className="panel" aria-labelledby="investigation-heading">
                      <div className="panel-head">
                        <div>
                          <span className="eyebrow">Case management</span>
                          <h2 id="investigation-heading">Active Recall Investigation</h2>
                        </div>
                        {investigations.length > 0 && <StatusBadge tone="info">{investigations.length} active cases</StatusBadge>}
                      </div>
                      <p className="panel-copy">
                        Each investigation isolates its suspect lot, uploaded evidence documents, claim verifications, and traceability snapshots.
                      </p>
                      <label className="field">
                        <span>Switch active investigation</span>
                        <select value={selectedInvestigation} onChange={(e) => setSelectedInvestigation(e.target.value)}>
                          <option value="">Workspace-wide (all investigations)</option>
                          {investigations.map((i) => (
                            <option key={i.id} value={i.id}>
                              {i.reference ? `[${i.reference}] ` : ""}
                              {i.name} · Suspect: {i.suspectLotId || "Unassigned"} ({i.traceRunCount} runs)
                            </option>
                          ))}
                        </select>
                      </label>
                      <div className="investigation-create">
                        <label className="field">
                          <span>New investigation title</span>
                          <input
                            value={newInvestigationName}
                            onChange={(e) => setNewInvestigationName(e.target.value)}
                            placeholder="e.g. FDA Event 93340 - Cinnamon Lead Recall"
                          />
                        </label>
                        <label className="field">
                          <span>Case reference ID</span>
                          <input
                            value={newInvestigationReference}
                            onChange={(e) => setNewInvestigationReference(e.target.value)}
                            placeholder="e.g. FDA-F-0402-2024"
                          />
                        </label>
                        <label className="field">
                          <span>Suspect lot (optional)</span>
                          <select value={newInvestigationSuspectLot} onChange={(e) => setNewInvestigationSuspectLot(e.target.value)}>
                            <option value="">Unassigned / decide later</option>
                            {lots.map((lot) => (
                              <option key={`${lot.kind}|${lot.id}`} value={`${lot.kind}|${lot.id}`}>
                                {lot.code} - {lot.product}
                              </option>
                            ))}
                          </select>
                        </label>
                        <button type="button" className="button button-secondary" disabled={!newInvestigationName.trim()} onClick={createInvestigation}>
                          Create Case
                        </button>
                      </div>
                    </section>

                    <section className="panel" aria-labelledby="recent-heading">
                      <div className="panel-head">
                        <div>
                          <span className="eyebrow">Traceability history</span>
                          <h2 id="recent-heading">Recent Traceability Snapshots</h2>
                        </div>
                        <button type="button" className="text-button" onClick={() => setTab("trace")}>
                          Open Trace Workspace
                        </button>
                      </div>
                      {runs.length === 0 ? (
                        <EmptyState
                          icon="trace"
                          title="No trace snapshots recorded yet"
                          body="Run a forward, backward, or bidirectional trace on a suspect lot to capture an immutable snapshot of affected lots, customer destinations, and evidence gaps."
                          action={
                            <button type="button" className="button button-primary" onClick={() => setTab("trace")}>
                              Start a Trace
                            </button>
                          }
                        />
                      ) : (
                        <ul className="run-list">
                          {runs.slice(0, 5).map((r) => (
                            <li key={r.id}>
                              <div>
                                <span className="run-lot">{r.selectedId}</span>
                                <span className="run-kind">{entityTypeLabel(r.selectedType)} · Snapshot {formatShortId(r.id)}</span>
                              </div>
                              <span className="run-time">{formatDateTime(r.completedAt)}</span>
                              <button type="button" className="button button-small button-secondary" onClick={() => openRun(r.id)}>
                                Open Snapshot
                              </button>
                            </li>
                          ))}
                        </ul>
                      )}
                    </section>

                    <section className="panel" aria-labelledby="gaps-heading">
                      <div className="panel-head">
                        <div>
                          <span className="eyebrow">Reconciliation engine</span>
                          <h2 id="gaps-heading">Open Evidence Findings & Gaps</h2>
                        </div>
                        <StatusBadge tone={openFindingCount > 0 ? "warning" : "positive"}>
                          {openFindingCount} open {openFindingCount === 1 ? "finding" : "findings"}
                        </StatusBadge>
                      </div>
                      {findings.length === 0 ? (
                        <EmptyState
                          icon="check"
                          title="No unresolved findings"
                          body="All current supply-chain links and quantities reconcile within tolerance."
                        />
                      ) : (
                        <ul className="home-finding-list">
                          {findings.slice(0, 4).map((f) => (
                            <li key={f.id} className="home-finding-item">
                              <SeverityTag severity={(f.severity?.toLowerCase() as Severity) ?? "low"} />
                              <div className="home-finding-body">
                                <strong>{f.title}</strong>
                                <span className="cell-meta">{formatFindingKind(f.kind)} · Scope: {formatScopeLabel(f.scope)}</span>
                              </div>
                              <button
                                type="button"
                                className="button button-small"
                                onClick={() => {
                                  setTab("findings");
                                  openFinding(f);
                                }}
                              >
                                Inspect
                              </button>
                            </li>
                          ))}
                        </ul>
                      )}
                    </section>
                  </div>

                  <aside className="home-side">
                    <section className="panel" aria-labelledby="provider-heading">
                      <div className="panel-head">
                        <div>
                          <span className="eyebrow">Intelligence pipeline</span>
                          <h2 id="provider-heading">AI Claim Extraction</h2>
                        </div>
                        <StatusBadge tone={provider.tone}>{provider.label}</StatusBadge>
                      </div>
                      <p className="panel-copy">{provider.detail}</p>
                      <dl className="detail-list">
                        <div>
                          <dt>Active Model</dt>
                          <dd>{ai?.modelId || "Not configured"}</dd>
                        </div>
                        <div>
                          <dt>Inference Host</dt>
                          <dd>{ai?.provider || "Local Parser"}</dd>
                        </div>
                      </dl>
                      {capabilities.length > 0 && (
                        <ul className="capability-list">
                          {capabilities.map((c) => (
                            <li key={c.id}>
                              <span>{c.name}</span>
                              <StatusBadge
                                tone={c.availability === "configured" ? "positive" : c.availability === "partial" ? "info" : "neutral"}
                              >
                                {c.availability === "configured" ? "Active" : c.availability === "partial" ? "Parser Ready" : "Not Enabled"}
                              </StatusBadge>
                            </li>
                          ))}
                        </ul>
                      )}
                    </section>

                    <section className="panel" aria-labelledby="import-heading">
                      <span className="eyebrow">Chain of Custody</span>
                      <h2 id="import-heading">Import Evidence File</h2>
                      {publicDemoMode ? (
                        <div className="dropzone">
                          <Icon name="upload" width={22} height={22} />
                          <p><strong>Public demonstration workspace — uploads are disabled.</strong></p>
                          <p className="dropzone-meta">
                            The pre-loaded synthetic evidence set is fully interactive: review claims, confirm
                            relationships and run traces. Pilot installations use this same flow with live uploads.
                          </p>
                        </div>
                      ) : (
                      <div
                        className={`dropzone${dragActive ? " is-active" : ""}`}
                        onDragOver={(e) => {
                          e.preventDefault();
                          setDragActive(true);
                        }}
                        onDragLeave={() => setDragActive(false)}
                        onDrop={(e) => {
                          e.preventDefault();
                          setDragActive(false);
                          const file = e.dataTransfer.files?.[0];
                          if (file) void onUpload(file);
                        }}
                      >
                        <Icon name="upload" width={22} height={22} />
                        <p>Drop an evidence document here or</p>
                        <label className="button button-secondary" htmlFor="evidence-file">
                          Browse files
                        </label>
                        <input
                          id="evidence-file"
                          className="visually-hidden"
                          type="file"
                          accept={ACCEPTED_FORMATS}
                          onChange={(e) => {
                            const file = e.target.files?.[0];
                            if (file) void onUpload(file);
                            e.target.value = "";
                          }}
                        />
                        <p className="dropzone-meta">
                          Supports PDF, CSV, XLSX, TSV, TXT, LOG, EML, PNG, JPG - up to {MAX_UPLOAD_MB} MB
                        </p>
                      </div>
                      )}
                      <p className="setup-hint">
                        {isSyntheticWorkspace
                          ? "Synthetic demonstration workspace: keep uploads synthetic. Not for operational recall decisions."
                          : "Every uploaded file is fingerprinted with SHA-256 to prevent duplicate ingestion and preserve audit integrity."}
                      </p>
                    </section>
                  </aside>
                </div>
              </section>
            )}

            {tab === "evidence" && (
              <section className="page" aria-labelledby="evidence-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">Source Document Repository</span>
                    <h1 id="evidence-heading">Evidence Inbox</h1>
                  </div>
                  <div className="page-actions">
                    <label className="search-field">
                      <span className="visually-hidden">Search evidence</span>
                      <Icon name="search" width={16} height={16} />
                      <input value={docQuery} onChange={(e) => setDocQuery(e.target.value)} placeholder="Search filename or category…" />
                    </label>
                    {publicDemoMode ? (
                      <span className="setup-hint" style={{ alignSelf: "center" }}>
                        Public demo: uploads disabled
                      </span>
                    ) : (
                      <>
                        <label className="button button-primary" htmlFor="evidence-inbox-upload" style={{ cursor: "pointer" }}>
                          <Icon name="upload" width={16} height={16} /> Upload Evidence
                        </label>
                        <input
                          id="evidence-inbox-upload"
                          className="visually-hidden"
                          type="file"
                          accept={ACCEPTED_FORMATS}
                          onChange={(e) => {
                            const file = e.target.files?.[0];
                            if (file) void onUpload(file);
                            e.target.value = "";
                          }}
                        />
                      </>
                    )}
                  </div>
                </div>

                <WarningBanner tone={currentInvestigation ? "info" : "caution"} title={scopeNote}>
                  {currentInvestigation
                    ? "All evidence files below are scoped to this recall investigation and fingerprinted with SHA-256."
                    : "Showing all documents in the workspace. Select an investigation to isolate case evidence."}
                </WarningBanner>

                {filteredDocuments.length === 0 ? (
                  <EmptyState
                    icon="inbox"
                    title={documents.length === 0 ? "No evidence files uploaded yet" : "No documents match your filter"}
                    body={
                      documents.length === 0
                        ? "Upload supplier delivery notes, goods receipt logs, batch records, lab reports, or dispatch manifests to begin."
                        : "Try a different filename or document category."
                    }
                    action={
                      <button type="button" className="button button-secondary" onClick={() => setDocQuery("")}>
                        Clear search
                      </button>
                    }
                  />
                ) : (
                  <div className="table-wrap">
                    <table className="data-table mobile-cards">
                      <caption className="visually-hidden">Evidence documents</caption>
                      <thead>
                        <tr>
                          <th scope="col">Document File</th>
                          <th scope="col">Ingestion Status</th>
                          <th scope="col">Category</th>
                          <th scope="col">Size</th>
                          <th scope="col">Imported At</th>
                          <th scope="col">Extracted Claims</th>
                          <th scope="col">Source Lines</th>
                          <th scope="col">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {filteredDocuments.map((d) => {
                          const st = documentStatusLabel(d.processingStatus);
                          return (
                            <tr key={d.id}>
                              <td data-label="Document File">
                                <span className="cell-strong" title={d.fileName}>
                                  {d.fileName}
                                </span>
                                <span className="cell-meta">SHA-256: {d.sha256 ? `${d.sha256.slice(0, 12)}…` : "-"}</span>
                              </td>
                              <td data-label="Ingestion Status">
                                <StatusBadge tone={st.tone} title={st.hint}>
                                  {st.label}
                                </StatusBadge>
                              </td>
                              <td data-label="Category">
                                <span className="category-pill">{formatDocumentType(d.documentType)}</span>
                              </td>
                              <td data-label="Size">{formatBytes(d.sizeBytes)}</td>
                              <td data-label="Imported At">{formatDateTime(d.uploadedAt)}</td>
                              <td data-label="Extracted Claims">
                                <strong>{d.factCount}</strong> {d.factCount === 1 ? "claim" : "claims"}
                              </td>
                              <td data-label="Source Lines">{d.locations.length}</td>
                              <td data-label="Actions">
                                <button
                                  type="button"
                                  className="button button-small button-secondary"
                                  onClick={() => {
                                    setSelectedDocument(d);
                                    void loadDocumentPreview(d);
                                  }}
                                >
                                  Inspect
                                </button>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            )}

            {tab === "claims" && (
              <section className="page" aria-labelledby="claims-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">Extracted Supply Chain Records</span>
                    <h1 id="claims-heading">Claims & Observations</h1>
                  </div>
                  <label className="search-field">
                    <span className="visually-hidden">Search claims</span>
                    <Icon name="search" width={16} height={16} />
                    <input value={factQuery} onChange={(e) => setFactQuery(e.target.value)} placeholder="Search lot code, product, or document…" />
                  </label>
                </div>

                <WarningBanner tone={currentInvestigation ? "info" : "caution"} title={scopeNote}>
                  {currentInvestigation
                    ? "Claims are extracted by structured document parsers and live AI model inference. Every candidate claim requires human review before confirmation."
                    : "Showing claims across the entire workspace."}
                </WarningBanner>

                <div className="filter-row" role="group" aria-label="Filter by review status">
                  {(["All", "Unreviewed", "Confirmed", "Edited", "Rejected"] as const).map((status) => (
                    <button
                      key={status}
                      type="button"
                      className={`filter-chip${factStatus === status ? " is-active" : ""}`}
                      aria-pressed={factStatus === status}
                      onClick={() => setFactStatus(status)}
                    >
                      {status} <span className="chip-count">{factCounts[status] ?? 0}</span>
                    </button>
                  ))}
                </div>

                {filteredFacts.length === 0 ? (
                  <EmptyState
                    icon="list"
                    title="No claims match the current filter"
                    body="Adjust your search query or select a different review status tab above."
                  />
                ) : (
                  <div className="table-wrap">
                    <table className="data-table mobile-cards">
                      <caption className="visually-hidden">
                        Claims showing {filteredFacts.length} of {facts.length}
                      </caption>
                      <thead>
                        <tr>
                          <th scope="col">Source Location</th>
                          <th scope="col">Subject / Lot Identifier</th>
                          <th scope="col">Extracted Evidence & Quantity</th>
                          <th scope="col">Verification</th>
                          <th scope="col">Review Status</th>
                          <th scope="col">Decision</th>
                        </tr>
                      </thead>
                      <tbody>
                        {pagedFacts.map((f) => {
                          const ext = extractionLabel(f);
                          const valBadge = formatValidationOutcome(f.validationOutcome, f.reviewStatus);
                          return (
                            <tr key={f.id} data-highlight-id={f.id} className={highlightId === f.id ? "is-highlighted" : undefined}>
                              <td data-label="Source Location">
                                <SourceLocatorChip
                                  documentName={f.sourceDocumentName}
                                  synthetic={f.sourceKind === "SeededFixture"}
                                  locator={f.sourceLocation && !/not recorded/i.test(f.sourceLocation) ? f.sourceLocation : undefined}
                                  onOpen={
                                    f.sourceDocumentId && f.sourceLocation && !/not recorded/i.test(f.sourceLocation)
                                      ? () => openSource(f)
                                      : undefined
                                  }
                                />
                              </td>
                              <td data-label="Subject / Lot Identifier">
                                <span className="cell-strong" title={f.claimSubject}>
                                  {f.claimSubject}
                                </span>
                                <span className="cell-meta" title={ext.hint}>
                                  {formatFactType(f.factType)} · {ext.shortLabel}
                                </span>
                              </td>
                              <td data-label="Extracted Evidence & Quantity">
                                {f.quantity != null && (
                                  <div className="claim-qty-line">
                                    <QuantityDisplay quantity={f.quantity} unit={f.quantityUnit} />
                                    {f.eventDate && <span className="cell-meta"> · {formatShortDate(f.eventDate)}</span>}
                                  </div>
                                )}
                                <span className="cell-clamp" title={f.rawValue}>
                                  {formatCleanValue(f.rawValue)}
                                </span>
                              </td>
                              <td data-label="Verification">
                                <StatusBadge tone={valBadge.tone}>{valBadge.label}</StatusBadge>
                              </td>
                              <td data-label="Review Status">
                                <StateBadge
                                  state={
                                    f.reviewStatus === "Confirmed"
                                      ? "confirmed"
                                      : f.reviewStatus === "Rejected"
                                        ? "rejected"
                                        : f.reviewStatus === "Edited"
                                          ? "inferred"
                                          : "candidate"
                                  }
                                />
                              </td>
                              <td data-label="Decision">
                                <div className="row-actions">
                                  <button
                                    type="button"
                                    className="button button-small button-secondary"
                                    disabled={busyFactId === f.id || f.reviewStatus === "Confirmed"}
                                    onClick={() => {
                                      setClaimAction({ id: f.id, action: "confirm" });
                                      setClaimReason("");
                                    }}
                                  >
                                    Confirm
                                  </button>
                                  <button
                                    type="button"
                                    className="button button-small button-danger"
                                    disabled={busyFactId === f.id || f.reviewStatus === "Rejected"}
                                    onClick={() => {
                                      setClaimAction({ id: f.id, action: "reject" });
                                      setClaimReason("");
                                    }}
                                  >
                                    Reject
                                  </button>
                                </div>
                                {claimAction?.id === f.id && (
                                  <div className="review-panel">
                                    <label className="field">
                                      <span>
                                        {claimAction.action === "reject"
                                          ? "Reason for rejection (required)"
                                          : reasonRequiredForConfirm(f)
                                            ? "Verification note (required - no line locator)"
                                            : "Verification note (optional)"}
                                      </span>
                                      <textarea value={claimReason} onChange={(e) => setClaimReason(e.target.value)} rows={2} />
                                    </label>
                                    <div className="row-actions">
                                      <button
                                        type="button"
                                        className="button button-small button-primary"
                                        disabled={
                                          claimAction.action === "reject"
                                            ? !claimReason.trim()
                                            : reasonRequiredForConfirm(f) && !claimReason.trim()
                                        }
                                        onClick={() => {
                                          void reviewFactWithReason(f.id, claimAction.action, claimReason);
                                          setClaimAction(null);
                                        }}
                                      >
                                        Submit decision
                                      </button>
                                      <button type="button" className="button button-small" onClick={() => setClaimAction(null)}>
                                        Cancel
                                      </button>
                                    </div>
                                  </div>
                                )}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
                <div className="pagination" role="group" aria-label="Claims pagination">
                  <button
                    type="button"
                    className="button button-small"
                    disabled={currentClaimPage <= 1}
                    onClick={() => setClaimPage((p) => Math.max(1, p - 1))}
                  >
                    Previous
                  </button>
                  <span aria-live="polite">
                    Showing {claimRangeStart}-{claimRangeEnd} of {filteredFacts.length} claims · Page {currentClaimPage} of {totalClaimPages}
                  </span>
                  <button
                    type="button"
                    className="button button-small"
                    disabled={currentClaimPage >= totalClaimPages}
                    onClick={() => setClaimPage((p) => p + 1)}
                  >
                    Next
                  </button>
                  <label className="page-size">
                    Rows per page
                    <select value={claimPageSize} onChange={(e) => setClaimPageSize(Number(e.target.value))}>
                      <option value={25}>25</option>
                      <option value={50}>50</option>
                      <option value={100}>100</option>
                    </select>
                  </label>
                </div>
              </section>
            )}

            {tab === "review" && (
              <section className="page desk-page" aria-labelledby="review-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">Human-in-the-Loop Verification</span>
                    <h1 id="review-heading">Claim Review Desk</h1>
                  </div>
                  <div className="desk-head-actions">
                    <span className="desk-counter" aria-live="polite">
                      Reviewed this session: <strong>{deskDecidedCount}</strong>
                    </span>
                    <button
                      type="button"
                      className="button button-small button-secondary"
                      aria-label="Keyboard shortcut help"
                      onClick={() => setDeskHelpOpen(true)}
                    >
                      Keyboard Shortcuts <kbd>?</kbd>
                    </button>
                  </div>
                </div>

                <WarningBanner tone={currentInvestigation ? "info" : "caution"} title={scopeNote}>
                  {currentInvestigation
                    ? "Inspect each extracted claim alongside its highlighted source document line before confirming, editing, or rejecting."
                    : "Reviewing claims across the entire workspace."}
                </WarningBanner>

                <div className="desk-grid">
                  <aside className={`panel desk-queue-pane${deskSheetOpen ? " is-open" : ""}`} aria-label="Review queue">
                    <button
                      type="button"
                      className="desk-sheet-toggle"
                      aria-expanded={deskSheetOpen}
                      onClick={() => setDeskSheetOpen((open) => !open)}
                    >
                      <Icon name="list" width={15} height={15} />
                      <span>
                        Review Queue - {deskQueue.length} claim{deskQueue.length === 1 ? "" : "s"}
                        {deskIndex >= 0 ? ` (${deskIndex + 1} of ${deskQueue.length})` : ""}
                      </span>
                      <Icon name="chevron-down" width={15} height={15} />
                    </button>
                    <div className="desk-queue-body">
                      <div className="filter-row" role="group" aria-label="Filter reviews by state">
                        {(["All", "Unreviewed", "Confirmed", "Edited", "Rejected"] as const).map((status) => (
                          <button
                            key={status}
                            type="button"
                            className={`filter-chip${deskStatus === status ? " is-active" : ""}`}
                            aria-pressed={deskStatus === status}
                            onClick={() => setDeskStatus(status)}
                          >
                            {status} <span className="chip-count">{factCounts[status] ?? 0}</span>
                          </button>
                        ))}
                      </div>
                      {deskQueue.length === 0 ? <DeskClaimEmpty /> : deskQueueList}
                    </div>
                  </aside>

                  <section className="panel desk-claim-pane" key={`${deskFact?.id ?? "empty"}:${deskSlideSeq}`} aria-label="Claim under review">
                    {!deskFact ? (
                      <DeskClaimEmpty />
                    ) : (
                      <>
                        <div className="panel-head">
                          <div>
                            <span className="eyebrow">{formatFactType(deskFact.factType)} · {formatClaimType(deskFact.claimType)}</span>
                            <h2 className="desk-claim-subject">{deskFact.claimSubject}</h2>
                          </div>
                          <DeskStateBadge fact={deskFact} />
                        </div>
                        <ProvenanceRibbon
                          fact={deskFact}
                          onOpenSource={hasSourceLocator(deskFact) ? () => void openSource(deskFact) : undefined}
                        />
                        <dl className="detail-list detail-inline">
                          <div>
                            <dt>Recorded Quantity</dt>
                            <dd>
                              {deskFact.quantity !== null && deskFact.quantity !== undefined ? (
                                <QuantityDisplay quantity={deskFact.quantity} unit={deskFact.quantityUnit} />
                              ) : (
                                <span className="cell-meta">No numeric quantity on this line</span>
                              )}
                            </dd>
                          </div>
                          <div>
                            <dt>Transaction / Event Date</dt>
                            <dd>{formatDateTime(deskFact.eventDate)}</dd>
                          </div>
                          <div className="detail-full">
                            <dt>Formatted Evidence Summary</dt>
                            <dd>{formatCleanValue(deskFact.rawValue)}</dd>
                          </div>
                          {deskFact.reviewedAt && (
                            <div className="detail-full">
                              <dt>Last Review Decision</dt>
                              <dd>
                                {deskFact.reviewStatus} by <strong>{deskFact.reviewer || "Reviewer"}</strong> · {formatDateTime(deskFact.reviewedAt)}
                              </dd>
                            </div>
                          )}
                        </dl>
                        <div className="desk-source">
                          <h3 className="mini-heading">Primary Source Document Excerpt</h3>
                          {!hasSourceLocator(deskFact) ? (
                            <div className="locator-card" role="note">
                              <Icon name="dashed-circle" width={16} height={16} />
                              <div>
                                <strong>No line locator recorded</strong>
                                <p>
                                  {deskFact.sourceDocumentName || "The source document"} does not have a specific line coordinate attached to this claim. A verification note is required to confirm it.
                                </p>
                              </div>
                            </div>
                          ) : deskSource.loading ? (
                            <LoadingPanel label="Loading source document lines…" />
                          ) : deskSource.error ? (
                            <div className="locator-card" role="note">
                              <Icon name="alert" width={16} height={16} />
                              <div>
                                <strong>Source preview unavailable</strong>
                                <p>{deskSource.error}</p>
                                <p className="cell-meta">
                                  {deskFact.sourceDocumentName} · {formatLocatorLabel(deskFact.sourceLocation)}
                                </p>
                              </div>
                            </div>
                          ) : deskSource.data ? (
                            deskSource.data.status === "resolved" && deskSource.data.lines.length > 0 ? (
                              <ol className="source-lines desk-source-lines">
                                {deskSource.data.lines.map((line) => (
                                  <li
                                    key={line.lineNumber}
                                    className={deskSource.data?.highlightLine === line.lineNumber ? "is-highlighted" : undefined}
                                  >
                                    <span className="line-number">{line.lineNumber}</span>
                                    <span className="line-text">{line.text}</span>
                                  </li>
                                ))}
                              </ol>
                            ) : (
                              <div className="locator-card" role="note">
                                <Icon name="file" width={16} height={16} />
                                <div>
                                  <strong>Structured Format Locator ({formatLocatorLabel(deskFact.sourceLocation)})</strong>
                                  <p>{ formatCleanValue(deskFact.evidenceText || deskFact.rawValue) }</p>
                                  <p className="cell-meta">
                                    {deskFact.sourceDocumentName} · {formatLocatorLabel(deskFact.sourceLocation)}
                                  </p>
                                </div>
                              </div>
                            )
                          ) : null}
                        </div>
                      </>
                    )}
                  </section>

                  <section className="panel desk-decision-pane" aria-label="Decision panel">
                    <div className="panel-head">
                      <div>
                        <span className="eyebrow">Operator Action</span>
                        <h2>Verification Decision</h2>
                      </div>
                      <span className="cell-meta">J/K navigate · C/E/R decide · H history</span>
                    </div>
                    {deskResult && (
                      <p className="desk-result" role="status">
                        {deskResult}
                      </p>
                    )}
                    {deskFact ? (
                      <>
                        <ReviewActionGroup fact={deskFact} busy={deskBusy} panel={deskAction} onOpen={openDeskAction} />
                        {deskAction && (
                          <form
                            id="desk-decision-form"
                            className="review-panel desk-decision-form"
                            onSubmit={(e) => {
                              e.preventDefault();
                              void submitDeskDecision();
                            }}
                          >
                            {deskAction === "edit" && deskEdits ? (
                              <>
                                <div className="desk-edit-grid">
                                  <label className="field">
                                    <span>Subject / Lot Code</span>
                                    <input
                                      value={deskEdits.claimSubject}
                                      onChange={(e) => setDeskEdits({ ...deskEdits, claimSubject: e.target.value })}
                                    />
                                  </label>
                                  <label className="field">
                                    <span>Extracted Value</span>
                                    <input
                                      value={deskEdits.rawValue}
                                      onChange={(e) => setDeskEdits({ ...deskEdits, rawValue: e.target.value })}
                                    />
                                  </label>
                                  <label className="field">
                                    <span>Normalized Lot</span>
                                    <input
                                      value={deskEdits.normalizedValue}
                                      onChange={(e) => setDeskEdits({ ...deskEdits, normalizedValue: e.target.value })}
                                    />
                                  </label>
                                  <label className="field">
                                    <span>Quantity</span>
                                    <input
                                      inputMode="decimal"
                                      value={deskEdits.quantity}
                                      onChange={(e) => setDeskEdits({ ...deskEdits, quantity: e.target.value })}
                                    />
                                  </label>
                                  <label className="field">
                                    <span>Unit</span>
                                    <input
                                      value={deskEdits.quantityUnit}
                                      onChange={(e) => setDeskEdits({ ...deskEdits, quantityUnit: e.target.value })}
                                    />
                                  </label>
                                  <label className="field">
                                    <span>Event Date</span>
                                    <input
                                      type="date"
                                      value={deskEdits.eventDate}
                                      onChange={(e) => setDeskEdits({ ...deskEdits, eventDate: e.target.value })}
                                    />
                                  </label>
                                </div>
                                <p className="cell-meta">
                                  Every edit is logged in the append-only audit history alongside the original extracted values.
                                </p>
                              </>
                            ) : (
                              <p className="panel-copy">
                                {deskAction === "confirm"
                                  ? reasonRequiredForConfirm(deskFact)
                                    ? "This claim has no line locator recorded, so a brief verification note is required."
                                    : "Confirming marks this claim as human-verified evidence for traceability runs."
                                  : "Rejecting excludes this candidate claim from active traceability paths while preserving it in the audit log."}
                              </p>
                            )}
                            <label className="field">
                              <span>
                                {deskAction === "confirm"
                                  ? deskReasonRequired
                                    ? "Reason for confirmation (required)"
                                    : "Verification note (optional)"
                                  : deskAction === "reject"
                                    ? "Reason for rejection (required)"
                                    : "Reason for edit (required)"}
                              </span>
                              <textarea
                                rows={2}
                                value={deskReason}
                                onChange={(e) => setDeskReason(e.target.value)}
                                onKeyDown={(e) => {
                                  if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
                                    e.preventDefault();
                                    void submitDeskDecision();
                                  }
                                }}
                              />
                            </label>
                            {deskError && (
                              <p className="inline-error" role="alert">
                                {deskError}
                              </p>
                            )}
                            <div className="row-actions desk-form-actions">
                              <button type="submit" className="button button-small button-primary" disabled={deskSubmitDisabled}>
                                {deskAction === "edit" ? "Save edit" : "Submit decision"}
                              </button>
                              <button type="button" className="button button-small" disabled={deskBusy} onClick={closeDeskAction}>
                                Cancel
                              </button>
                            </div>
                          </form>
                        )}
                        <div className="desk-history">
                          <button
                            type="button"
                            className="desk-history-toggle"
                            aria-expanded={deskHistoryOpen}
                            onClick={() => setDeskHistoryOpen((open) => !open)}
                          >
                            <Icon name="clock" width={15} height={15} />
                            <span>History ({deskHistory.loading ? "…" : deskHistory.events.length})</span>
                            <span className="key-hints" aria-hidden="true">
                              H
                            </span>
                          </button>
                          {deskHistoryOpen &&
                            (deskHistory.loading ? (
                              <LoadingPanel label="Loading history…" />
                            ) : deskHistory.error ? (
                              <p className="inline-error" role="alert">
                                {deskHistory.error}
                              </p>
                            ) : (
                              <HistoryList events={deskHistory.events} />
                            ))}
                        </div>
                      </>
                    ) : (
                      <p className="panel-copy">Select a claim from the queue to begin verification.</p>
                    )}
                  </section>
                </div>

                <div className="desk-mobile-bar">
                  {deskFact && !deskAction && <ReviewActionGroup fact={deskFact} busy={deskBusy} panel={null} onOpen={openDeskAction} />}
                  {deskFact && deskAction && (
                    <div className="row-actions">
                      <button type="submit" form="desk-decision-form" className="button button-primary" disabled={deskSubmitDisabled}>
                        {deskAction === "edit" ? "Save edit" : "Submit decision"}
                      </button>
                      <button type="button" className="button" disabled={deskBusy} onClick={closeDeskAction}>
                        Cancel
                      </button>
                    </div>
                  )}
                </div>
              </section>
            )}

            {tab === "matches" && (
              <section className="page" aria-labelledby="matches-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">Entity Resolution & Confusable Lot Guard</span>
                    <h1 id="matches-heading">Identifier Matches & Aliases</h1>
                  </div>
                  <button type="button" className="button button-secondary" onClick={refreshMatches}>
                    Recompute Proposals
                  </button>
                </div>

                <p className="panel-copy">
                  Near-match lot identifiers (such as OCR or manual entry substitutions between digit <code>0</code> and letter <code>O</code>) are never merged silently. Review the side-by-side evidence comparison below to confirm or reject each alias.
                </p>

                <div className="view-switcher matches-tabs" role="tablist" aria-label="Match views">
                  <button
                    type="button"
                    role="tab"
                    id="matches-tab-proposals"
                    aria-selected={matchView === "proposals"}
                    aria-controls="matches-panel-proposals"
                    className={`view-tab${matchView === "proposals" ? " is-active" : ""}`}
                    onClick={() => setMatchView("proposals")}
                  >
                    Proposals <span className="chip-count">{matches.length}</span>
                  </button>
                  <button
                    type="button"
                    role="tab"
                    id="matches-tab-aliases"
                    aria-selected={matchView === "aliases"}
                    aria-controls="matches-panel-aliases"
                    className={`view-tab${matchView === "aliases" ? " is-active" : ""}`}
                    onClick={() => setMatchView("aliases")}
                  >
                    Aliases <span className="chip-count">{aliases.length}</span>
                  </button>
                </div>

                {matchView === "proposals" ? (
                  <div id="matches-panel-proposals" role="tabpanel" aria-labelledby="matches-tab-proposals">
                    {matches.length === 0 ? (
                      <EmptyState
                        icon="search"
                        title="No near-match proposals found"
                        body="Click Recompute Proposals after importing evidence to scan for visually confusable lot identifiers."
                      />
                    ) : (
                      <div className="match-list">
                        {matches.map((m) => (
                          <MatchComparison
                            key={m.id}
                            proposal={m}
                            facts={facts}
                            busy={matchBusyId === m.id}
                            highlighted={highlightId === m.id}
                            onDecide={(action, reason) => decideMatchWithReason(m, action, reason)}
                          />
                        ))}
                      </div>
                    )}
                  </div>
                ) : (
                  <section id="matches-panel-aliases" role="tabpanel" className="panel" aria-label="Confirmed aliases">
                    <div className="panel-head">
                      <div>
                        <span className="eyebrow">Active Entity Mappings</span>
                        <h2 id="aliases-heading">Confirmed Lot Aliases</h2>
                      </div>
                      <StatusBadge tone="positive">{aliases.length} confirmed</StatusBadge>
                    </div>
                    {aliases.length === 0 ? (
                      <p className="panel-copy">
                        No aliases have been confirmed yet. Confirming a near-match proposal maps the variant code to its canonical lot while keeping original evidence untouched.
                      </p>
                    ) : (
                      <div className="table-wrap">
                        <table className="data-table">
                          <thead>
                            <tr>
                              <th scope="col">Evidence Variant</th>
                              <th scope="col">Canonical Lot</th>
                              <th scope="col">Entity Category</th>
                              <th scope="col">Confirmed By</th>
                              <th scope="col">Recorded At</th>
                            </tr>
                          </thead>
                          <tbody>
                            {aliases.map((a) => (
                              <tr key={a.id}>
                                <td>
                                  <LotDiffBadge value={a.aliasValue} compareTo={a.canonicalValue} />
                                </td>
                                <td>
                                  <span className="run-lot">{a.canonicalValue}</span>
                                </td>
                                <td>{entityTypeLabel(a.entityType)}</td>
                                <td>{a.confirmedBy}</td>
                                <td>{formatDateTime(a.createdAt)}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </section>
                )}
              </section>
            )}

            {tab === "trace" && (
              <section className="page" aria-labelledby="trace-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">One-Step-Back & One-Step-Forward Supply Chain Graph</span>
                    <h1 id="trace-heading">Traceability Workspace</h1>
                    <p className="page-subtitle">Links show which claims support them and where evidence is missing.</p>
                  </div>
                  {trace && (
                    <StatusBadge tone={isSyntheticWorkspace ? "warning" : "positive"}>
                      {isSyntheticWorkspace ? "Synthetic Demonstration Snapshot" : "Pilot Investigation Snapshot"}
                    </StatusBadge>
                  )}
                </div>

                <div className="trace-grid">
                  <section className="panel trace-form" aria-labelledby="trace-form-heading">
                    <span className="eyebrow">Configure Trace Run</span>
                    <h2 id="trace-form-heading">Execute Supply Chain Trace</h2>
                    {currentInvestigation ? (
                      <p className="inline-note">
                        <Icon name="check" width={15} height={15} /> Case: {currentInvestigation.name}
                        {currentInvestigation.reference ? ` (${currentInvestigation.reference})` : ""}
                      </p>
                    ) : (
                      <p className="inline-note inline-note-warning">
                        <Icon name="alert" width={15} height={15} /> No investigation selected - trace will run across workspace-wide links.
                      </p>
                    )}
                    <label className="field">
                      <span>Filter lots or products</span>
                      <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Type lot code or product name…" />
                    </label>
                    <label className="field">
                      <span>Target lot or batch</span>
                      <select ref={lotSelectRef} value={selectedLot} onChange={(e) => setSelectedLot(e.target.value)}>
                        <option value="">Select a lot or batch…</option>
                        {Object.entries(lotsByKind).map(([kind, group]) => (
                          <optgroup key={kind} label={entityTypeLabel(kind)}>
                            {group.map((l) => (
                              <option key={l.kind + l.id} value={l.id}>
                                {l.code} - {l.product}
                                {l.isSuspect ? " (Suspect Lot)" : ""}
                              </option>
                            ))}
                          </optgroup>
                        ))}
                      </select>
                    </label>
                    {selectedLotData?.isSuspect && (
                      <p className="inline-note inline-note-warning">
                        <Icon name="alert" width={15} height={15} /> Primary suspect lot for this recall investigation.
                      </p>
                    )}
                    {nearMatchWarnings.length > 0 && selectedLotData && (
                      <div className="inline-alert" role="note">
                        <Icon name="alert" width={16} height={16} />
                        <div>
                          <strong>Confusable Identifier Detected in Evidence</strong>
                          <p>
                            Canonical lot <span className="run-lot">{selectedLotData.code}</span> has visually similar variant(s) in evidence:{" "}
                            {nearMatchWarnings.map((nm, idx) => (
                              <span key={nm}>
                                {idx > 0 && ", "}
                                <LotDiffBadge value={nm} compareTo={selectedLotData.code} />
                              </span>
                            ))}
                            . Verify in Matches before treating as identical.
                          </p>
                        </div>
                      </div>
                    )}
                    <fieldset className="field">
                      <legend>Traversal direction</legend>
                      <div className="radio-row">
                        {(["backward", "forward", "both"] as const).map((d) => (
                          <label key={d} className={`radio-pill${direction === d ? " is-active" : ""}`}>
                            <input type="radio" name="direction" value={d} checked={direction === d} onChange={() => setDirection(d)} />
                            <span>{DIRECTION_LABEL[d]}</span>
                          </label>
                        ))}
                      </div>
                    </fieldset>
                    <button type="button" className="button button-primary button-block" disabled={!selectedLot || runningTrace} onClick={runTrace}>
                      {runningTrace ? "Executing trace…" : "Run Traceability Analysis"}
                    </button>
                    <p className="field-hint">
                      Every trace run saves an immutable snapshot for professional review and export.
                    </p>
                  </section>

                  <section className="panel" aria-labelledby="runs-heading">
                    <span className="eyebrow">Saved Snapshots</span>
                    <h2 id="runs-heading">Trace Run History</h2>
                    {runs.length === 0 ? (
                      <EmptyState icon="clock" title="No saved trace snapshots" body="Select a lot on the left and run a trace to record the first snapshot." />
                    ) : (
                      <ul className="run-list">
                        {runs.map((r) => (
                          <li key={r.id}>
                            <div>
                              <span className="run-lot">{r.selectedId}</span>
                              <span className="run-kind">
                                {entityTypeLabel(r.selectedType)} · {formatShortId(r.id)}
                              </span>
                            </div>
                            <span className="run-time">{formatDateTime(r.completedAt)}</span>
                            <button type="button" className="button button-small button-secondary" onClick={() => openRun(r.id)}>
                              View
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                  </section>
                </div>

                <section className="panel" aria-labelledby="links-heading">
                  <div className="panel-head">
                    <div>
                      <span className="eyebrow">Human Review Gate</span>
                      <h2 id="links-heading">Candidate Trace Links</h2>
                    </div>
                    <div className="row-actions">
                      <StatusBadge tone={pendingLinkCount > 0 ? "warning" : "positive"}>
                        {pendingLinkCount > 0 ? `${pendingLinkCount} awaiting review` : "All reviewed"}
                      </StatusBadge>
                      <button
                        type="button"
                        className="button button-small button-secondary"
                        disabled={linksBusy}
                        onClick={generateRelationshipLinks}
                      >
                        {linksBusy ? "Generating…" : "Generate Proposals"}
                      </button>
                    </div>
                  </div>
                  <p className="panel-copy">
                    Candidate links never contribute to confirmed trace scope. Confirm only relationships supported by the cited source evidence and reject the rest - every decision is written to the audit log with the reviewer name. Confirming a relationship is a relationship-level decision; it does not mark the underlying claims reviewed.
                  </p>
                  {visibleLinks.length === 0 ? (
                    <p className="panel-copy">
                      No candidate links recorded for this case yet. Generate proposals after claim extraction to derive relationships from source-linked claims.
                    </p>
                  ) : (
                    <ul className="run-list">
                      {visibleLinks.map((l) => (
                        <li key={l.id}>
                          <div>
                            <span className="run-lot">
                              {l.fromId} → {l.toId}
                            </span>
                            <span className="run-kind">
                              {l.linkKind.replace(/_/g, " ")}
                              {l.quantity != null ? ` · ${l.quantity} ${l.quantityUnit}` : ""}
                              {` · ${l.detectionMethod}`}
                              {l.needsReReview ? " · needs re-review" : ""}
                            </span>
                            {l.evidenceText && (
                              <span className="run-kind" title={l.evidenceText}>
                                “{l.evidenceText.length > 120 ? `${l.evidenceText.slice(0, 120)}…` : l.evidenceText}”
                              </span>
                            )}
                          </div>
                          <div className="row-status">
                            <StatusBadge tone={l.state === "Confirmed" ? "positive" : l.state === "Rejected" ? "neutral" : "warning"}>
                              {l.state}
                            </StatusBadge>
                            {l.unreviewedSupportingFactCount > 0 && (
                              <StatusBadge tone="warning">
                                {l.unreviewedSupportingFactCount} supporting fact{l.unreviewedSupportingFactCount === 1 ? "" : "s"} unreviewed
                              </StatusBadge>
                            )}
                          </div>
                          {l.state === "Candidate" ? (
                            <div className="row-actions">
                              <button
                                type="button"
                                className="button button-small button-primary"
                                disabled={linkBusyId === l.id}
                                onClick={() => decideRelationshipLink(l, "confirm")}
                              >
                                Confirm
                              </button>
                              <button
                                type="button"
                                className="button button-small"
                                disabled={linkBusyId === l.id}
                                onClick={() => decideRelationshipLink(l, "reject")}
                              >
                                Reject
                              </button>
                            </div>
                          ) : (
                            <span className="run-time">{l.reviewedBy ? `by ${l.reviewedBy}` : "-"}</span>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                </section>

                {!trace ? (
                  <EmptyState
                    icon="trace"
                    title="No trace snapshot selected"
                    body="Select a raw material lot, production batch, or finished lot above and run a trace to inspect affected finished lots, retail destinations, mass balance, and evidence gaps."
                    action={
                      <button type="button" className="button button-secondary" onClick={() => lotSelectRef.current?.focus()}>
                        Select a Lot
                      </button>
                    }
                  />
                ) : (
                  <article className="panel result-panel" aria-labelledby="result-heading">
                    <div className="panel-head">
                      <div>
                        <span className="eyebrow">
                          Traceability Report · {entityTypeLabel(trace.selectedType)} {trace.selectedId}
                        </span>
                        <h2 id="result-heading">Traceability Analysis Result</h2>
                        <p className="panel-copy">{trace.summary}</p>
                      </div>
                      <div className="panel-head-badges">
                        <StatusBadge tone={isSyntheticWorkspace ? "warning" : "positive"}>
                          {isSyntheticWorkspace ? "Synthetic Data Report" : "Pilot Snapshot"}
                        </StatusBadge>
                        <StatusBadge tone={traceFromHistory ? "neutral" : "info"}>
                          {traceFromHistory ? "Historical Snapshot" : "Active Run"}
                        </StatusBadge>
                      </div>
                    </div>

                    <dl className="run-meta">
                      <div>
                        <dt>Snapshot Reference</dt>
                        <dd title={`Full Run UUID: ${trace.runId}`}>
                          <span className="run-lot">{formatShortId(trace.runId)}</span>{" "}
                          <CopyButton value={trace.runId} label="Copy full trace run UUID" />
                        </dd>
                      </div>
                      <div>
                        <dt>Completed At</dt>
                        <dd title={isoTitle(trace.completedAt)}>{formatDateTime(trace.completedAt)}</dd>
                      </div>
                      <div>
                        <dt>Direction</dt>
                        <dd>{DIRECTION_LABEL[trace.direction] ?? trace.direction}</dd>
                      </div>
                      <div>
                        <dt>Execution Time</dt>
                        <dd>{elapsedLabel(trace.startedAt, trace.completedAt)}</dd>
                      </div>
                    </dl>

                    <div className="metrics metrics-compact" aria-label="Link state counts">
                      <MetricTile label="Confirmed Links" value={trace.stateCounts.Confirmed ?? 0} tone="positive" />
                      <MetricTile label="Pending Review" value={trace.stateCounts.Candidate ?? 0} tone="info" />
                      <MetricTile label="Inferred Links" value={trace.stateCounts.Inferred ?? 0} />
                      <MetricTile
                        label="Evidence Gaps"
                        value={trace.gaps.length}
                        tone={trace.gaps.length > 0 ? "warning" : "positive"}
                      />
                    </div>

                    <div className="trace-controls">
                      <div className="view-switcher" role="group" aria-label="Trace view">
                        {(["paths", "graph", "table"] as const).map((view) => (
                          <button
                            key={view}
                            type="button"
                            className={`filter-chip${traceView === view ? " is-active" : ""}`}
                            aria-pressed={traceView === view}
                            disabled={view === "graph" && dataSaver}
                            title={view === "graph" && dataSaver ? "Graph is disabled while Data saver is on." : undefined}
                            onClick={() => setTraceView(view)}
                          >
                            {view === "paths" ? "Paths" : view === "graph" ? "Graph" : "Table"}
                          </button>
                        ))}
                      </div>
                      <div className="legend" aria-label="Filter link states">
                        {(
                          [
                            ["Confirmed", "Confirmed", "solid"],
                            ["Candidate", "Needs review", "dashed"],
                            ["Inferred", "Inferred (unconfirmed)", "dotted"]
                          ] as const
                        ).map(([state, label, line]) => (
                          <button
                            key={state}
                            type="button"
                            className={`legend-item legend-${line}${stateFilter.includes(state) ? " is-on" : ""}`}
                            aria-pressed={stateFilter.includes(state)}
                            onClick={() =>
                              setStateFilter((prev) => (prev.includes(state) ? prev.filter((s) => s !== state) : [...prev, state]))
                            }
                          >
                            <span className="legend-line" aria-hidden="true" />
                            {label}
                          </button>
                        ))}
                      </div>
                    </div>

                    <div className="result-columns">
                      <div className="subpanel">
                        <h3 className="mini-heading">Impacted Finished Product Lots ({trace.affectedFinishedLots.length})</h3>
                        {trace.affectedFinishedLots.length ? (
                          <div className="chip-cloud">
                            {trace.affectedFinishedLots.map((lotCode) => (
                              <span key={lotCode} className="lot-pill">
                                {lotCode}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <p className="panel-copy">No finished product lots reached in this trace direction.</p>
                        )}
                      </div>
                      <div className="subpanel">
                        <h3 className="mini-heading">Impacted Retail & Customer Destinations ({trace.affectedCustomers.length})</h3>
                        {trace.affectedCustomers.length ? (
                          <ul className="plain-list">
                            {trace.affectedCustomers.map((c) => (
                              <li key={c}>
                                <strong>{c}</strong>
                              </li>
                            ))}
                          </ul>
                        ) : (
                          <p className="panel-copy">
                            No downstream customer dispatches traversed in this direction.
                          </p>
                        )}
                      </div>
                    </div>

                    <div className="result-columns">
                      <section className="subpanel" aria-labelledby="mass-heading">
                        <div className="subpanel-head">
                          <h3 id="mass-heading" className="mini-heading">
                            Mass Balance & Quantity Reconciliation
                          </h3>
                          <StatusBadge
                            tone={massBalanceView(trace.massBalance.status).tone}
                            title="Computed from quantity-bearing links in this trace snapshot."
                          >
                            {massBalanceView(trace.massBalance.status).label}
                          </StatusBadge>
                        </div>
                        <p className="panel-copy">{trace.massBalance.notes}</p>
                        <table className="data-table mass-table">
                          <caption className="visually-hidden">Mass balance from the saved trace snapshot</caption>
                          <thead>
                            <tr>
                              <th scope="col">Stage</th>
                              <th scope="col">Recorded Quantity</th>
                              <th scope="col">Unit</th>
                            </tr>
                          </thead>
                          <tbody>
                            <tr>
                              <th scope="row">Received / Upstream Input</th>
                              <td>
                                {trace.massBalance.inputTotal == null ? (
                                  <span className="qty-missing">Not recorded</span>
                                ) : (
                                  <QuantityDisplay quantity={trace.massBalance.inputTotal} unit={trace.massBalance.inputUnit} />
                                )}
                              </td>
                              <td>{trace.massBalance.inputUnit || <span className="qty-unit-missing">unspecified</span>}</td>
                            </tr>
                            <tr>
                              <th scope="row">Packaged / Dispatched Output</th>
                              <td>
                                {trace.massBalance.outputTotal == null ? (
                                  <span className="qty-missing">Not recorded</span>
                                ) : (
                                  <QuantityDisplay quantity={trace.massBalance.outputTotal} unit={trace.massBalance.outputUnit} />
                                )}
                              </td>
                              <td>{trace.massBalance.outputUnit || <span className="qty-unit-missing">unspecified</span>}</td>
                            </tr>
                            <tr>
                              <th scope="row">Recorded Process Waste</th>
                              <td>
                                {trace.massBalance.waste == null ? (
                                  <span className="qty-missing">Not recorded</span>
                                ) : (
                                  <QuantityDisplay quantity={trace.massBalance.waste} unit={trace.massBalance.outputUnit} />
                                )}
                              </td>
                              <td>{trace.massBalance.outputUnit || <span className="qty-unit-missing">unspecified</span>}</td>
                            </tr>
                          </tbody>
                        </table>
                      </section>

                      <section className="subpanel" aria-labelledby="gaps-result-heading">
                        <h3 id="gaps-result-heading" className="mini-heading">
                          Unresolved Chain-of-Custody Gaps ({trace.gaps.length})
                        </h3>
                        {trace.gaps.length === 0 ? (
                          <p className="panel-copy">No missing links were detected along the traversed path.</p>
                        ) : (
                          <ul className="gap-list">
                            {trace.gaps.map((g, i) => (
                              <li key={`${g.title}-${i}`}>
                                <Icon name="alert" width={16} height={16} />
                                <div>
                                  <strong>{formatLinkKind(g.title)}</strong>
                                  <p>{g.description}</p>
                                  <p className="gap-action">Recommended action: {g.recommendation}</p>
                                </div>
                              </li>
                            ))}
                          </ul>
                        )}
                      </section>
                    </div>

                    <section className="subpanel" aria-labelledby="trace-findings-heading">
                      <h3 id="trace-findings-heading" className="mini-heading">
                        Automated Reconciliation Findings ({trace.findings.length})
                      </h3>
                      {trace.findings.length === 0 ? (
                        <p className="panel-copy">No reconciliation findings raised for this snapshot.</p>
                      ) : (
                        <ul className="gap-list">
                          {trace.findings.map((f) => (
                            <li key={f.id}>
                              <Icon name="alert" width={16} height={16} />
                              <div>
                                <strong>{f.title}</strong>
                                <p>{f.explanation}</p>
                                <p className="gap-action">
                                  {formatFindingKind(f.kind)} · Severity: {f.severity.toUpperCase()} · Verification rule: {formatCheckName(f.raisedByCheck)}
                                </p>
                              </div>
                            </li>
                          ))}
                        </ul>
                      )}
                    </section>

                    <section className="trace-view" aria-label={`Trace ${traceView} view`}>
                      {traceFromHistory && (
                        <p className="cell-meta" role="note">
                          Viewing immutable snapshot captured on {formatDateTime(trace.completedAt)}.
                        </p>
                      )}
                      {visibleEdges.length === 0 && (
                        <EmptyState
                          icon="trace"
                          title="No links match the active state filters"
                          body="Re-enable a link state in the legend above or run a new trace."
                        />
                      )}

                      {traceView === "paths" && visibleEdges.length > 0 && (
                        <ul className="hop-list">
                          {visibleEdges.map((e) => (
                            <li key={e.id} className={selectedEdge?.id === e.id ? "is-selected" : ""}>
                              <button
                                type="button"
                                className="hop"
                                aria-expanded={expandedEdgeId === e.id}
                                onClick={() => setExpandedEdgeId(expandedEdgeId === e.id ? null : e.id)}
                              >
                                <div>
                                  <span className="run-lot">{e.fromId}</span>
                                  <span className="cell-meta">{entityTypeLabel(e.fromType)}</span>
                                </div>
                                <span className="hop-rel">→ {formatLinkKind(e.linkKind)} →</span>
                                <div>
                                  <span className="run-lot">{e.toId}</span>
                                  <span className="cell-meta">{entityTypeLabel(e.toType)}</span>
                                </div>
                                <StateBadge state={edgeTraceState(e.state)} />
                                <span className="hop-qty">
                                  {e.quantity != null ? (
                                    <QuantityDisplay quantity={e.quantity} unit={e.quantityUnit} />
                                  ) : (
                                    <span className="qty-missing">Not recorded</span>
                                  )}
                                </span>
                              </button>
                              {expandedEdgeId === e.id && (
                                <div className="hop-detail">
                                  <p className="panel-copy">{linkSentence(e)}</p>
                                  {e.evidenceText && <p className="cell-meta">{formatCleanValue(e.evidenceText)}</p>}
                                  {edgeFacts(e).length > 0 ? (
                                    <div className="locator-row">
                                      {edgeFacts(e).map((f) => (
                                        <SourceLocatorChip
                                          key={f.id}
                                          documentName={f.sourceDocumentName}
                                          synthetic={f.sourceKind === "SeededFixture"}
                                          locator={f.sourceLocation && !/not recorded/i.test(f.sourceLocation) ? f.sourceLocation : undefined}
                                          onOpen={
                                            f.sourceDocumentId && f.sourceLocation && !/not recorded/i.test(f.sourceLocation)
                                              ? () => openSource(f)
                                              : undefined
                                          }
                                        />
                                      ))}
                                    </div>
                                  ) : (
                                    <p className="cell-meta">Linked via canonical supply-chain registry.</p>
                                  )}
                                  <button type="button" className="button button-small button-secondary" onClick={() => openEdge(e)}>
                                    Open Link Inspector
                                  </button>
                                </div>
                              )}
                            </li>
                          ))}
                        </ul>
                      )}

                      {traceView === "graph" && !dataSaver && <TraceGraphView trace={trace} edges={visibleEdges} onSelectEdge={openEdge} />}
                      {traceView === "graph" && dataSaver && (
                        <p className="cell-meta">Graph view is disabled while Data saver is on. Switch to Paths or Table.</p>
                      )}

                      {traceView === "table" && visibleEdges.length > 0 && (
                        <div className="table-wrap">
                          <table className="data-table mobile-cards">
                            <caption className="visually-hidden">Trace links with evidence and source references</caption>
                            <thead>
                              <tr>
                                <th scope="col">Origin Entity</th>
                                <th scope="col">Destination Entity</th>
                                <th scope="col">Supply Chain Step</th>
                                <th scope="col">Verification State</th>
                                <th scope="col">Quantity</th>
                                <th scope="col">Supporting Evidence</th>
                                <th scope="col">Source Citation</th>
                              </tr>
                            </thead>
                            <tbody>
                              {visibleEdges.map((e) => (
                                <tr key={e.id}>
                                  <td data-label="Origin Entity">
                                    <span className="cell-strong">{e.fromId}</span>
                                    <span className="cell-meta">{entityTypeLabel(e.fromType)}</span>
                                  </td>
                                  <td data-label="Destination Entity">
                                    <span className="cell-strong">{e.toId}</span>
                                    <span className="cell-meta">{entityTypeLabel(e.toType)}</span>
                                  </td>
                                  <td data-label="Supply Chain Step">{formatLinkKind(e.linkKind)}</td>
                                  <td data-label="Verification State">
                                    <StateBadge state={edgeTraceState(e.state)} />
                                  </td>
                                  <td data-label="Quantity">
                                    {e.quantity != null ? (
                                      <QuantityDisplay quantity={e.quantity} unit={e.quantityUnit} />
                                    ) : (
                                      <span className="qty-missing">Not recorded</span>
                                    )}
                                  </td>
                                  <td data-label="Supporting Evidence">
                                    <span className="cell-clamp" title={e.evidenceText}>
                                      {formatCleanValue(e.evidenceText)}
                                    </span>
                                  </td>
                                  <td data-label="Source Citation">
                                    {edgeFacts(e).length > 0 ? (
                                      <div className="locator-row">
                                        {edgeFacts(e)
                                           .slice(0, 2)
                                           .map((f) => (
                                            <SourceLocatorChip
                                              key={f.id}
                                              documentName={f.sourceDocumentName}
                                              synthetic={f.sourceKind === "SeededFixture"}
                                              locator={f.sourceLocation && !/not recorded/i.test(f.sourceLocation) ? f.sourceLocation : undefined}
                                              onOpen={
                                                f.sourceDocumentId && f.sourceLocation && !/not recorded/i.test(f.sourceLocation)
                                                  ? () => openSource(f)
                                                  : undefined
                                              }
                                            />
                                          ))}
                                      </div>
                                    ) : (
                                      <button type="button" className="button button-small" onClick={() => openEdge(e)}>
                                        Inspect Link
                                      </button>
                                    )}
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      )}
                    </section>

                    <div className="result-actions">
                      <a
                        className="button button-secondary"
                        href={`/api/trace-runs/${trace.runId}/export?format=xlsx`}
                        target="_blank"
                        rel="noreferrer"
                      >
                        Excel Workbook (XLSX)
                      </a>
                      <a
                        className="button button-secondary"
                        href={`/api/trace-runs/${trace.runId}/export?format=html`}
                        target="_blank"
                        rel="noreferrer"
                      >
                        Printable HTML Report
                      </a>
                      <a
                        className="button button-secondary"
                        href={`/api/trace-runs/${trace.runId}/export?format=json`}
                        target="_blank"
                        rel="noreferrer"
                      >
                        JSON Data Package
                      </a>
                      <a
                        className="button button-secondary"
                        href={`/api/trace-runs/${trace.runId}/export?format=csv`}
                        target="_blank"
                        rel="noreferrer"
                      >
                        CSV Tables (CSV)
                      </a>
                    </div>
                  </article>
                )}
              </section>
            )}

            {tab === "findings" && (
              <section className="page" aria-labelledby="findings-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">Automated Verification & Gap Detection</span>
                    <h1 id="findings-heading">Reconciliation Findings</h1>
                  </div>
                  <button type="button" className="button button-secondary" onClick={refreshFindings}>
                    Recompute Findings
                  </button>
                </div>
                {findings.length === 0 ? (
                  <EmptyState
                    icon="check"
                    title="No reconciliation findings"
                    body="All supply-chain links, quantities, and lot identifiers are reconciled."
                  />
                ) : (
                  <div className="table-wrap">
                    <table className="data-table mobile-cards">
                      <caption className="visually-hidden">Reconciliation findings</caption>
                      <thead>
                        <tr>
                          <th scope="col">Severity</th>
                          <th scope="col">Finding & Category</th>
                          <th scope="col">State</th>
                          <th scope="col">Affected Scope</th>
                          <th scope="col">Explanation</th>
                          <th scope="col">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {findings.map((f) => (
                          <tr key={f.id} data-highlight-id={f.id} className={highlightId === f.id ? "is-highlighted" : undefined}>
                            <td data-label="Severity">
                              <SeverityTag severity={(f.severity?.toLowerCase() as Severity) ?? "low"} />
                            </td>
                            <td data-label="Finding & Category">
                              <span className="cell-strong">{f.title}</span>
                              <span className="cell-meta">
                                {formatFindingKind(f.kind)} · {formatCheckName(f.raisedByCheck)}
                              </span>
                            </td>
                            <td data-label="State">
                              <StateBadge state={f.severity === "critical" || f.severity === "high" ? "conflict" : "candidate"} />
                            </td>
                            <td data-label="Affected Scope">
                              <span className="run-lot">{formatScopeLabel(f.scope)}</span>
                              <span className="cell-meta">Status: {f.status}</span>
                            </td>
                            <td data-label="Explanation">
                              <p className="finding-explanation">{f.explanation}</p>
                            </td>
                            <td data-label="Actions">
                              <button type="button" className="button button-small button-secondary" onClick={() => openFinding(f)}>
                                Inspect Details
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            )}

            {tab === "system" && (
              <section className="page" aria-labelledby="system-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">Runtime Diagnostics & Model Verification</span>
                    <h1 id="system-heading">System & AI Configuration</h1>
                  </div>
                  <StatusBadge
                    tone={modelSummary(ai, aiHealth, smoke).tone}
                    title="Live verification status of the configured AI extraction model."
                  >
                    {modelSummary(ai, aiHealth, smoke).label}
                  </StatusBadge>
                </div>

                <section className="panel" aria-labelledby="ladder-heading">
                  <span className="eyebrow">Live Inference Readiness</span>
                  <h2 id="ladder-heading">Extraction Model Verification Ladder</h2>
                  <ol className="ladder">
                    <li>
                      <StateBadge state={ai?.isConfigured ? "confirmed" : "unknown"} />{" "}
                      <span>
                        <strong>Configured Model:</strong> {ai?.modelId || "No model configured"} ({ai?.provider || "Local"})
                      </span>
                    </li>
                    <li>
                      <StateBadge state={aiHealth?.reachable ? "confirmed" : aiHealth ? "failed" : "unknown"} />{" "}
                      <span>
                        <strong>Service Reachability:</strong>{" "}
                        {aiHealth
                          ? aiHealth.reachable
                            ? `Online · Verified at ${formatDateTime(aiHealth.checkedAt)}`
                            : `Unreachable - ${aiHealth.error || "no response from inference service"}`
                          : "Not checked"}
                      </span>
                    </li>
                    <li>
                      <StateBadge state={aiHealth?.installedModels?.length ? "confirmed" : "missing"} />{" "}
                      <span>
                        <strong>Installed Models:</strong>{" "}
                        {aiHealth?.installedModels?.length ? aiHealth.installedModels.join(", ") : "None listed"}
                      </span>
                    </li>
                    <li>
                      <StateBadge state={smoke?.success ? "confirmed" : "unknown"} />{" "}
                      <span>
                        <strong>Live Inference Smoke Test:</strong>{" "}
                        {smoke
                          ? smoke.success
                            ? `Verified at ${formatDateTime(smoke.checkedAt)} (${smoke.modelId} extracted ${smoke.factsReturned} structured claim)`
                            : `Failed - ${smoke.error || "no response from model"}`
                          : "Click 'Run Smoke Test' below to verify live end-to-end extraction"}
                      </span>
                    </li>
                  </ol>
                  <div className="row-actions" style={{ marginTop: "14px" }}>
                    <button type="button" className="button button-primary" disabled={smokeRunning} onClick={runSmoke}>
                      {smokeRunning ? "Running live inference test…" : "Run smoke test"}
                    </button>
                    <button type="button" className="button button-secondary" onClick={loadAll}>
                      Refresh Status
                    </button>
                  </div>
                </section>

                <section className="panel" aria-labelledby="capabilities-heading">
                  <span className="eyebrow">Ingestion Modules</span>
                  <h2 id="capabilities-heading">Document Processing Capabilities</h2>
                  <ul className="plain-list">
                    <li>
                      <strong>Structured Text, CSV, TSV, LOG & EML Parsing:</strong> Active - deterministic line & field extraction with SHA-256 deduplication.
                    </li>
                    <li>
                      <strong>Spreadsheet Workbook Parsing (.xlsx / .xls):</strong> Active - worksheet and row-level coordinate extraction.
                    </li>
                    <li>
                      <strong>PDF Document Text Extraction (.pdf):</strong> Active - embedded text layer extraction with page-level locators.
                    </li>
                    <li>
                      <strong>Live AI Claim Extraction ({ai?.modelId || "LLM"}):</strong>{" "}
                      {ai?.isConfigured ? "Active - schema-validated JSON claim extraction." : "Not configured."}
                    </li>
                    <li>
                      <strong>Optical Character Recognition (OCR) for Scanned Images:</strong> Not enabled - binary images (.png, .jpg) are retained in the evidence vault for manual inspection.
                    </li>
                  </ul>
                </section>

                <section className="panel" aria-labelledby="datasaver-heading">
                  <h2 id="datasaver-heading">Display & Bandwidth Preferences</h2>
                  <p className="panel-copy">
                    Data Saver mode replaces interactive SVG graphs with compact path lists and disables UI transitions.
                  </p>
                  <label className="field">
                    <span>
                      <input
                        type="checkbox"
                        checked={dataSaver}
                        onChange={(e) => {
                          setDataSaver(e.target.checked);
                          try {
                            localStorage.setItem("recallgraph.dataSaver", e.target.checked ? "1" : "0");
                          } catch {
                            /* preference only */
                          }
                        }}
                      />{" "}
                      Enable Data Saver mode
                    </span>
                  </label>
                </section>

                <section className="panel" aria-labelledby="boundary-heading">
                  <span className="eyebrow">Security & Data Boundary</span>
                  <h2 id="boundary-heading">Data & Security Boundary</h2>
                  <ul className="plain-list">
                    <li>
                      <strong>Local-first storage:</strong> evidence files and the workspace database (SQLite) live on the machine running Klarrow; storage paths are operator-configured.
                    </li>
                    <li>
                      <strong>AI extraction data flow:</strong> when live AI extraction runs, the extracted text of uploaded documents is sent to the configured inference endpoint ({ai?.endpoint || "LLAMA_API_BASE_URL"}). Pointed at a local Ollama instance, no evidence text leaves your infrastructure.
                    </li>
                    <li>
                      <strong>Access control:</strong>{" "}
                      {platformHealth?.accessControl.mode === "token"
                        ? "API-token protected via a single shared key"
                        : "not configured (local prototype mode)"}{" "}
                      - {platformHealth?.accessControl.notice || "mode not yet checked"} No user accounts, SSO, or role-based permissions.
                    </li>
                    <li>
                      <strong>Deletion:</strong> investigations can be deleted from the Investigations tab. Scoped documents, claims, decisions, links, findings, and stored files are removed and the deletion is written to the audit trail; declared limitations are reported to the operator.
                    </li>
                    <li>
                      <strong>Document parsers:</strong> text, CSV/TSV, EML, XLSX/XLS, and PDF text-layer extraction only. Binary images are retained in the evidence vault for manual inspection - no OCR.
                    </li>
                    <li>
                      <strong>Audit trail:</strong> append-only operator log of decisions and actions within this workspace - it is not tamper-evident or externally notarized.
                    </li>
                  </ul>
                </section>

                <section className="panel" aria-labelledby="keystore-heading">
                  <h2 id="keystore-heading">Session API Authentication</h2>
                  <p className="panel-copy">
                    When server API key protection is enabled, enter your session key below. It is kept exclusively in browser sessionStorage and never written to exports.
                  </p>
                  {api.hasKey() ? (
                    <div className="row-actions">
                      <StatusBadge tone="positive">API Key Active for Session</StatusBadge>
                      <button type="button" className="button button-secondary" onClick={clearSessionKey}>
                        Clear Session Key
                      </button>
                    </div>
                  ) : (
                    <div className="investigation-create">
                      <label className="field">
                        <span>Session API Key</span>
                        <input
                          type="password"
                          value={apiKeyDraft}
                          onChange={(e) => setApiKeyDraft(e.target.value)}
                          onKeyDown={(e) => {
                            if (e.key === "Enter") void saveSessionKey();
                          }}
                        />
                      </label>
                      <button type="button" className="button button-primary" disabled={!apiKeyDraft.trim()} onClick={saveSessionKey}>
                        Save Key for Session
                      </button>
                    </div>
                  )}
                </section>
              </section>
            )}

            {tab === "investigations" && (
              <section className="page" aria-labelledby="investigations-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">Case Management</span>
                    <h1 id="investigations-heading">Recall Investigations</h1>
                  </div>
                </div>
                <section className="panel" aria-labelledby="create-investigation-heading">
                  <h2 id="create-investigation-heading">Create New Investigation</h2>
                  <div className="investigation-create">
                    <label className="field">
                      <span>Investigation Name (required)</span>
                      <input
                        value={newInvestigationName}
                        onChange={(e) => setNewInvestigationName(e.target.value)}
                        placeholder="e.g. FDA Event 93340 - Cinnamon Lead Recall"
                      />
                    </label>
                    <label className="field">
                      <span>Official Reference ID</span>
                      <input
                        value={newInvestigationReference}
                        onChange={(e) => setNewInvestigationReference(e.target.value)}
                        placeholder="e.g. FDA-F-0402-2024"
                      />
                    </label>
                    <label className="field">
                      <span>Suspect lot (optional)</span>
                      <select value={newInvestigationSuspectLot} onChange={(e) => setNewInvestigationSuspectLot(e.target.value)}>
                        <option value="">Unassigned / decide later</option>
                        {lots.map((lot) => (
                          <option key={`${lot.kind}|${lot.id}`} value={`${lot.kind}|${lot.id}`}>
                            {lot.code} - {lot.product}
                          </option>
                        ))}
                      </select>
                    </label>
                    <button type="button" className="button button-primary" disabled={!newInvestigationName.trim()} onClick={createInvestigation}>
                      Create Investigation
                    </button>
                  </div>
                </section>
                {investigations.length === 0 ? (
                  <EmptyState
                    icon="search"
                    title="No investigations created yet"
                    body="Create an investigation above to organize suspect lots, evidence uploads, and traceability snapshots."
                  />
                ) : (
                  <div className="table-wrap">
                    <table className="data-table mobile-cards">
                      <caption className="visually-hidden">Investigations</caption>
                      <thead>
                        <tr>
                          <th scope="col">Reference ID</th>
                          <th scope="col">Investigation Title</th>
                          <th scope="col">Suspect Lot</th>
                          <th scope="col">Status</th>
                          <th scope="col">Trace Snapshots</th>
                          <th scope="col">Last Updated</th>
                          <th scope="col">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {investigations.map((i) => (
                          <tr key={i.id} className={i.id === selectedInvestigation ? "is-highlighted" : undefined}>
                            <td data-label="Reference ID">
                              <span className="run-lot">{i.reference || "-"}</span>
                            </td>
                            <td data-label="Investigation Title">
                              <span className="cell-strong">{i.name}</span>
                              {i.description && <span className="cell-meta">{i.description}</span>}
                            </td>
                            <td data-label="Suspect Lot">
                              <span className="run-lot">{i.suspectLotId || "Unassigned"}</span>
                              {i.suspectLotId && <CopyButton value={i.suspectLotId} label={`Copy suspect lot ${i.suspectLotId}`} />}
                            </td>
                            <td data-label="Status">
                              <StatusBadge tone={i.status === "Open" ? "info" : "neutral"}>{i.status}</StatusBadge>
                            </td>
                            <td data-label="Trace Snapshots">{i.traceRunCount}</td>
                            <td data-label="Last Updated">{formatDateTime(i.updatedAt)}</td>
                            <td data-label="Actions">
                              <div className="row-actions">
                                <button
                                  type="button"
                                  className="button button-small button-primary"
                                  onClick={() => {
                                    setSelectedInvestigation(i.id);
                                    setTab("home");
                                  }}
                                >
                                  {i.id === selectedInvestigation ? "Active Case" : "Activate"}
                                </button>
                                <button
                                  type="button"
                                  className="button button-small button-danger"
                                  onClick={() => setDeleteTarget(i)}
                                  aria-label={`Delete investigation ${i.reference || i.name}`}
                                >
                                  Delete
                                </button>
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            )}

            {tab === "export" && (
              <section className="page" aria-labelledby="export-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">Regulatory & Audit Reporting</span>
                    <h1 id="export-heading">Export Traceability Package</h1>
                  </div>
                </div>
                {!trace ? (
                  <EmptyState
                    icon="external"
                    title="No trace snapshot selected for export"
                    body="Open the Traceability tab, execute or select a trace snapshot, then return here to download the complete audit package."
                    action={
                      <button type="button" className="button button-primary" onClick={() => setTab("trace")}>
                        Go to Traceability
                      </button>
                    }
                  />
                ) : (
                  <>
                    <section className="panel" aria-labelledby="precheck-heading">
                      <div className="panel-head">
                        <div>
                          <span className="eyebrow">Snapshot {formatShortId(trace.runId)} · {trace.selectedId}</span>
                          <h2 id="precheck-heading">Pre-Export Readiness Summary</h2>
                        </div>
                        <StatusBadge tone={exportOpenItems > 0 ? "warning" : "positive"}>
                          {exportOpenItems > 0 ? `${exportOpenItems} open items noted` : "All items verified"}
                        </StatusBadge>
                      </div>
                      <div className="metrics metrics-compact" style={{ marginBottom: "14px" }}>
                        <MetricTile label="Open Findings" value={openFindingCount} tone={openFindingCount > 0 ? "warning" : "positive"} />
                        <MetricTile label="Pending Matches" value={candidateMatchCount} tone={candidateMatchCount > 0 ? "warning" : "positive"} />
                        <MetricTile label="Unreviewed Claims" value={factCounts.Unreviewed ?? 0} tone={(factCounts.Unreviewed ?? 0) > 0 ? "warning" : "positive"} />
                        <MetricTile label="Mass Balance" value={massBalanceView(trace.massBalance.status).label} tone={massBalanceView(trace.massBalance.status).tone} />
                      </div>
                      <WarningBanner tone="info" title="Full Transparency Export">
                        Unresolved gaps and unreviewed candidate claims do not block export - they are explicitly itemized in the exported report so investigators and regulators see both confirmed links and remaining uncertainty.
                      </WarningBanner>
                    </section>
                    <section className="panel" aria-labelledby="download-heading">
                      <span className="eyebrow">Download Formats</span>
                      <h2 id="download-heading">Download Traceability & Audit Package</h2>
                      <p className="panel-copy">
                        Every export package includes the immutable trace snapshot ({formatShortId(trace.runId)}), all upstream and downstream supply-chain hops, mass balance reconciliation, open findings, and the complete append-only operator audit trail. The Excel workbook separates the investigation summary, evidence and facts, trace links, findings and gaps, and audit trail into named worksheets.
                      </p>
                      <p className="cell-meta" style={{ marginBottom: "14px" }}>
                        Spreadsheet safety: text values beginning with <code>=</code>, <code>+</code>, <code>-</code>, or <code>@</code> are written as text, not executable formulas. CSV remains available separately as a flat table.
                      </p>
                      <div className="result-actions">
                        <a
                          className="button button-primary"
                          href={`/api/trace-runs/${trace.runId}/export?format=xlsx`}
                          target="_blank"
                          rel="noreferrer"
                        >
                          Download Excel Workbook (XLSX)
                        </a>
                        <a
                          className="button button-secondary"
                          href={`/api/trace-runs/${trace.runId}/export?format=html`}
                          target="_blank"
                          rel="noreferrer"
                        >
                          Download Printable Report (HTML)
                        </a>
                        <a
                          className="button button-secondary"
                          href={`/api/trace-runs/${trace.runId}/export?format=json`}
                          target="_blank"
                          rel="noreferrer"
                        >
                          Download Structured Data (JSON)
                        </a>
                        <a
                          className="button button-secondary"
                          href={`/api/trace-runs/${trace.runId}/export?format=csv`}
                          target="_blank"
                          rel="noreferrer"
                        >
                          Download CSV Tables (CSV)
                        </a>
                      </div>
                    </section>
                  </>
                )}
              </section>
            )}

            {tab === "audit" && (
              <section className="page" aria-labelledby="audit-heading">
                <div className="page-head">
                  <div>
                    <span className="eyebrow">Immutable Chain of Custody</span>
                    <h1 id="audit-heading">Operator & System Audit Trail</h1>
                  </div>
                  <StatusBadge tone="info">{audits.length} recorded events</StatusBadge>
                </div>
                {audits.length === 0 ? (
                  <EmptyState
                    icon="audit"
                    title="No audit events recorded yet"
                    body="Import evidence, review a claim, confirm an alias, or run a trace to populate the append-only audit log."
                  />
                ) : (
                  <div className="table-wrap">
                    <table className="data-table mobile-cards audit-table">
                      <caption className="visually-hidden">Audit events</caption>
                      <thead>
                        <tr>
                          <th scope="col">Timestamp</th>
                          <th scope="col">Action Type</th>
                          <th scope="col">Operator</th>
                          <th scope="col">Record Category</th>
                          <th scope="col">Event Summary</th>
                        </tr>
                      </thead>
                      <tbody>
                        {audits.map((a) => (
                          <tr key={a.id}>
                            <td data-label="Timestamp" className="cell-nowrap">
                              {formatDateTime(a.createdAt)}
                            </td>
                            <td data-label="Action Type" className="cell-nowrap">
                              <StatusBadge
                                tone={
                                  a.eventType.includes("rejected")
                                    ? "critical"
                                    : a.eventType.includes("confirmed")
                                      ? "positive"
                                      : a.eventType.includes("trace")
                                        ? "info"
                                        : "neutral"
                                }
                              >
                                {formatAuditEventType(a.eventType)}
                              </StatusBadge>
                            </td>
                            <td data-label="Operator" className="cell-nowrap">
                              <strong>{a.actor}</strong>
                            </td>
                            <td data-label="Record Category" className="cell-nowrap">
                              <span className="category-pill">{formatAuditTargetType(a.targetType)}</span>
                            </td>
                            <td data-label="Event Summary">{formatAuditDescription(a.description)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            )}
          </>
        )}
      </main>

      {selectedDocument && (
        <div className="drawer-scrim" onClick={() => setSelectedDocument(null)}>
          <aside
            ref={documentTrap}
            className="drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Document details"
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => {
              if (e.key === "Escape") setSelectedDocument(null);
            }}
          >
            <div className="drawer-head">
              <div>
                <span className="eyebrow">Evidence Document Inspector</span>
                <h2>{selectedDocument.fileName}</h2>
              </div>
              <button
                type="button"
                className="button button-small"
                autoFocus
                onClick={() => setSelectedDocument(null)}
                aria-label="Close document details"
              >
                Close
              </button>
            </div>
            <div className="drawer-body">
              <dl className="detail-list detail-inline">
                <div>
                  <dt>Ingestion Status</dt>
                  <dd>
                    <StatusBadge tone={documentStatusLabel(selectedDocument.processingStatus).tone}>
                      {documentStatusLabel(selectedDocument.processingStatus).label}
                    </StatusBadge>
                  </dd>
                </div>
                <div>
                  <dt>Parser Engine</dt>
                  <dd>{formatParserLabel(selectedDocument.parserName, selectedDocument.parserVersion)}</dd>
                </div>
                <div>
                  <dt>Document Category</dt>
                  <dd>{formatDocumentType(selectedDocument.documentType)}</dd>
                </div>
                <div>
                  <dt>Imported At</dt>
                  <dd title={isoTitle(selectedDocument.uploadedAt)}>{formatDateTime(selectedDocument.uploadedAt)}</dd>
                </div>
                <div>
                  <dt>Extracted Claims</dt>
                  <dd>{selectedDocument.factCount} candidate claims</dd>
                </div>
                <div>
                  <dt>File Size</dt>
                  <dd>{formatBytes(selectedDocument.sizeBytes)}</dd>
                </div>
              </dl>
              <div>
                <h3 className="mini-heading">SHA-256 Cryptographic Fingerprint</h3>
                <div className="hop-state-row">
                  <code className="hash-code">{selectedDocument.sha256 || "Not recorded"}</code>
                  {selectedDocument.sha256 && <CopyButton value={selectedDocument.sha256} label="Copy SHA-256 hash" />}
                </div>
              </div>
              <div>
                <h3 className="mini-heading">Extracted Document Text Preview</h3>
                <pre className="preview">
                  {documentPreviews[selectedDocument.id] === undefined
                    ? "Loading extracted text…"
                    : documentPreviews[selectedDocument.id].slice(0, 4000) ||
                      "Binary image or scan retained in evidence vault. Optical character recognition (OCR) is not enabled in this configuration."}
                </pre>
              </div>
              <button
                type="button"
                className="button button-primary"
                onClick={() => {
                  setSelectedDocument(null);
                  setTab("claims");
                }}
              >
                Review Extracted Claims
              </button>
            </div>
          </aside>
        </div>
      )}

      {deleteTarget && (
        <div className="drawer-scrim" onClick={() => { if (!deleteBusy) setDeleteTarget(null); }}>
          <aside
            ref={deleteTrap}
            className="drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Delete investigation confirmation"
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => {
              if (e.key === "Escape" && !deleteBusy) setDeleteTarget(null);
            }}
          >
            <div className="drawer-head">
              <div>
                <span className="eyebrow">Destructive action</span>
                <h2>Delete investigation</h2>
              </div>
              <button
                type="button"
                className="button button-small"
                autoFocus
                disabled={deleteBusy}
                onClick={() => setDeleteTarget(null)}
                aria-label="Cancel deletion"
              >
                Cancel
              </button>
            </div>
            <div className="drawer-body">
              <p className="panel-copy">
                This permanently removes <strong>{deleteTarget.reference || deleteTarget.name}</strong> ({deleteTarget.name}) and its scoped evidence from
                this local workspace: documents, extracted claims, review decisions, trace links and runs, findings, and stored evidence files.
              </p>
              <WarningBanner tone="critical" title="No undo in this prototype">
                Deletion cannot be undone here. Exports or copies of this investigation that already exist outside the workspace are not affected. The
                deletion action itself is written to the audit trail.
              </WarningBanner>
              <div className="row-actions">
                <button type="button" className="button button-danger" disabled={deleteBusy} onClick={() => void performDeleteInvestigation()}>
                  {deleteBusy ? "Deleting investigation…" : "Delete investigation and scoped evidence"}
                </button>
                <button type="button" className="button button-secondary" disabled={deleteBusy} onClick={() => setDeleteTarget(null)}>
                  Keep investigation
                </button>
              </div>
            </div>
          </aside>
        </div>
      )}

      {selectedFinding && (
        <div className="drawer-scrim" onClick={closeFinding}>
          <aside
            ref={findingTrap}
            className="drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Finding details"
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => {
              if (e.key === "Escape") closeFinding();
            }}
          >
            <div className="drawer-head">
              <div>
                <span className="eyebrow">{formatFindingKind(selectedFinding.kind)}</span>
                <h2>Reconciliation Finding Details</h2>
              </div>
              <button type="button" className="button button-small" autoFocus onClick={closeFinding} aria-label="Close finding details">
                Close
              </button>
            </div>
            <div className="drawer-body">
              <div className="finding-head">
                <SeverityTag severity={(selectedFinding.severity?.toLowerCase() as Severity) ?? "low"} />
                <StateBadge state={selectedFinding.severity === "critical" || selectedFinding.severity === "high" ? "conflict" : "candidate"} />
                <StatusBadge tone="neutral">{selectedFinding.status}</StatusBadge>
              </div>
              <h3>{selectedFinding.title}</h3>
              <p className="panel-copy">{selectedFinding.explanation}</p>
              <dl className="detail-list detail-inline">
                <div>
                  <dt>Affected Scope</dt>
                  <dd>{formatScopeLabel(selectedFinding.scope)}</dd>
                </div>
                <div>
                  <dt>Verification Rule</dt>
                  <dd>{formatCheckName(selectedFinding.raisedByCheck)}</dd>
                </div>
                <div>
                  <dt>First Detected</dt>
                  <dd>{formatDateTime(selectedFinding.createdAt)}</dd>
                </div>
                <div>
                  <dt>Last Evaluated</dt>
                  <dd>{formatDateTime(selectedFinding.updatedAt)}</dd>
                </div>
              </dl>
              <div>
                <h3 className="mini-heading">Supporting Evidence Summary</h3>
                {(() => {
                  const evidence = parseEvidenceJson(selectedFinding.evidenceJson);
                  if (!evidence || Object.keys(evidence).length === 0) {
                    return <p className="cell-meta">Derived from supply-chain graph topology verification.</p>;
                  }
                  const entries: { label: string; content: React.ReactNode }[] = [];
                  if (evidence.receivedTotal !== undefined) {
                    entries.push({
                      label: "Total Received Quantity",
                      content: <QuantityDisplay quantity={Number(evidence.receivedTotal)} unit="kg" />
                    });
                  }
                  if (evidence.consumedTotal !== undefined) {
                    entries.push({
                      label: "Total Consumed in Batches",
                      content: <QuantityDisplay quantity={Number(evidence.consumedTotal)} unit="kg" />
                    });
                  }
                  if (evidence.Quantity !== undefined && evidence.Quantity !== null) {
                    entries.push({
                      label: "Unaccounted Quantity",
                      content: <QuantityDisplay quantity={Number(evidence.Quantity)} unit={String(evidence.QuantityUnit || "")} />
                    });
                  }
                  if (evidence.canonical && evidence.evidenceVariant) {
                    entries.push({
                      label: "Canonical vs Variant Lot",
                      content: (
                        <span>
                          <span className="run-lot">{String(evidence.canonical)}</span> vs{" "}
                          <LotDiffBadge value={String(evidence.evidenceVariant)} compareTo={String(evidence.canonical)} />
                        </span>
                      )
                    });
                  }
                  if (evidence.unreviewed !== undefined) {
                    entries.push({
                      label: "Pending Candidate Claims",
                      content: `${String(evidence.unreviewed)} claims awaiting operator review`
                    });
                  }
                  if (Array.isArray(evidence.linkIds)) {
                    entries.push({
                      label: "Evaluated Trace Links",
                      content: `${evidence.linkIds.length} supply-chain link records`
                    });
                  }
                  if (typeof evidence.ConfidenceNote === "string" && evidence.ConfidenceNote) {
                    entries.push({
                      label: "Verification Note",
                      content: evidence.ConfidenceNote
                    });
                  }
                  if (entries.length === 0) {
                    return <p className="cell-meta">Verified against active supply-chain links.</p>;
                  }
                  return (
                    <dl className="detail-list detail-inline">
                      {entries.map((item) => (
                        <div key={item.label}>
                          <dt>{item.label}</dt>
                          <dd>{item.content}</dd>
                        </div>
                      ))}
                    </dl>
                  );
                })()}
              </div>
              <div>
                <h3 className="mini-heading">Related Supply Chain Entity</h3>
                {selectedFinding.relatedEntityId && selectedFinding.relatedEntityType !== "workspace" ? (
                  <div className="hop-state-row">
                    <span className="cell-meta">
                      {entityTypeLabel(selectedFinding.relatedEntityType)}:{" "}
                      <span className="run-lot">{selectedFinding.relatedEntityId}</span>
                    </span>
                    {["raw-lot", "finished-lot", "production-batch"].includes(selectedFinding.relatedEntityType) && (
                      <button
                        type="button"
                        className="button button-small button-primary"
                        onClick={() => {
                          setSelectedLot(selectedFinding.relatedEntityId);
                          closeFinding();
                          setTab("trace");
                        }}
                      >
                        Trace this Lot
                      </button>
                    )}
                  </div>
                ) : (
                  <p className="cell-meta">Applies across the active investigation workspace.</p>
                )}
              </div>
            </div>
          </aside>
        </div>
      )}

      {selectedEdge && (
        <div className="drawer-scrim" onClick={closeEdge}>
          <aside
            ref={edgeTrap}
            className="drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Link inspector"
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => {
              if (e.key === "Escape") closeEdge();
            }}
          >
            <div className="drawer-head">
              <div>
                <span className="eyebrow">Supply Chain Link Inspector</span>
                <h2>
                  {selectedEdge.fromId} → {selectedEdge.toId}
                </h2>
              </div>
              <button type="button" className="button button-small" autoFocus onClick={closeEdge} aria-label="Close link inspector">
                Close
              </button>
            </div>
            <div className="drawer-body">
              <p className="panel-copy">{linkSentence(selectedEdge)}</p>
              <div className="hop-state-row">
                <StateBadge state={edgeTraceState(selectedEdge.state)} />
                <span className="cell-meta">
                  {selectedEdge.state === "Gap"
                    ? "Downstream record has not been located yet."
                    : selectedEdge.confidenceNote || "Verified supply-chain relationship."}
                </span>
              </div>
              <dl className="detail-list detail-inline">
                <div>
                  <dt>Relationship Step</dt>
                  <dd>{formatLinkKind(selectedEdge.linkKind)}</dd>
                </div>
                <div>
                  <dt>Recorded Quantity</dt>
                  <dd>
                    {selectedEdge.quantity != null ? (
                      <QuantityDisplay quantity={selectedEdge.quantity} unit={selectedEdge.quantityUnit} />
                    ) : (
                      <span className="qty-missing">Not recorded</span>
                    )}
                  </dd>
                </div>
                <div>
                  <dt>Transaction Date</dt>
                  <dd>{selectedEdge.eventDate ? formatDateTime(selectedEdge.eventDate) : "Not recorded"}</dd>
                </div>
              </dl>
              {selectedEdge.evidenceText && (
                <div>
                  <h3 className="mini-heading">Evidence Summary</h3>
                  <p className="panel-copy">{formatCleanValue(selectedEdge.evidenceText)}</p>
                </div>
              )}
              <div>
                <h3 className="mini-heading">Linked Source Citations</h3>
                {edgeFacts(selectedEdge).length > 0 ? (
                  <div className="locator-row">
                    {edgeFacts(selectedEdge).map((f) => (
                      <SourceLocatorChip
                        key={f.id}
                        documentName={f.sourceDocumentName}
                        synthetic={f.sourceKind === "SeededFixture"}
                        locator={f.sourceLocation && !/not recorded/i.test(f.sourceLocation) ? f.sourceLocation : undefined}
                        onOpen={
                          f.sourceDocumentId && f.sourceLocation && !/not recorded/i.test(f.sourceLocation)
                            ? () => openSource(f)
                            : undefined
                        }
                      />
                    ))}
                  </div>
                ) : (
                  <p className="cell-meta">Established via canonical supply-chain lot registry.</p>
                )}
              </div>
              <div>
                <h3 className="mini-heading">Related Reconciliation Findings</h3>
                {traceFindingsForEdge(selectedEdge).length > 0 ? (
                  <ul className="plain-list">
                    {traceFindingsForEdge(selectedEdge).map((f) => (
                      <li key={f.id}>
                        <strong>{f.title}</strong> ({f.severity.toUpperCase()})
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="cell-meta">No reconciliation findings raised for this link.</p>
                )}
              </div>
            </div>
          </aside>
        </div>
      )}

      {sourceView.open && (
        <div className="drawer-scrim" onClick={closeSource}>
          <aside
            ref={sourceTrap}
            className="drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Source preview"
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => {
              if (e.key === "Escape") closeSource();
            }}
          >
            <div className="drawer-head">
              <div>
                <span className="eyebrow">Primary Evidence Excerpt</span>
                <h2>{sourceView.data?.fileName ?? "Source Document Preview"}</h2>
                {sourceView.data?.locator && <p className="cell-meta">{formatLocatorLabel(sourceView.data.locator)}</p>}
              </div>
              <button type="button" className="button button-small" autoFocus onClick={closeSource} aria-label="Close source preview">
                Close
              </button>
            </div>
            <div className="drawer-body">
              {sourceView.loading && <p className="panel-copy">Loading extracted text…</p>}
              {sourceView.error && (
                <WarningBanner tone="critical" title="Source preview could not be loaded">
                  {sourceView.error}
                </WarningBanner>
              )}
              {sourceView.data && sourceView.data.status === "resolved" && (
                <ol className="source-lines">
                  {sourceView.data.lines.map((line) => (
                    <li key={line.lineNumber} className={sourceView.data?.highlightLine === line.lineNumber ? "is-highlighted" : ""}>
                      <span className="line-number">{line.lineNumber}</span>
                      <span className="line-text">{line.text}</span>
                    </li>
                  ))}
                </ol>
              )}
              {sourceView.data && sourceView.data.status !== "resolved" && (
                <WarningBanner tone="info" title={`Locator: ${formatLocatorLabel(sourceView.data.locator)}`}>
                  {sourceView.data.reason || "Line-level text preview is only available for line-indexed text files."}
                </WarningBanner>
              )}
              {sourceView.data && (
                <dl className="detail-list detail-inline drawer-meta">
                  <div>
                    <dt>Ingestion Status</dt>
                    <dd>{documentStatusLabel(sourceView.data.processingStatus).label}</dd>
                  </div>
                  <div>
                    <dt>Parser Engine</dt>
                    <dd>{formatParserLabel(sourceView.data.parserName, sourceView.data.parserVersion)}</dd>
                  </div>
                </dl>
              )}
            </div>
          </aside>
        </div>
      )}

      {deskHelpOpen && (
        <div className="drawer-scrim" onClick={() => setDeskHelpOpen(false)}>
          <aside
            ref={deskHelpTrap}
            className="drawer help-drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Keyboard shortcuts"
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => {
              if (e.key === "Escape") setDeskHelpOpen(false);
            }}
          >
            <div className="drawer-head">
              <div>
                <span className="eyebrow">Review Desk Navigation</span>
                <h2>Keyboard Shortcuts</h2>
              </div>
              <button
                type="button"
                className="button button-small"
                onClick={() => setDeskHelpOpen(false)}
                aria-label="Close shortcut help"
              >
                Close
              </button>
            </div>
            <div className="drawer-body">
              <ShortcutHelpBody />
              <p className="panel-copy">
                Keyboard shortcuts never confirm or reject a claim automatically - they open the decision form so every operator action is deliberate and auditable.
              </p>
            </div>
          </aside>
        </div>
      )}

      <footer className="app-footer">
        <span>Klarrow evidence reconciliation workbench · All claims, aliases, and trace snapshots are preserved in the append-only audit trail.</span>
      </footer>
    </div>
  );
}
