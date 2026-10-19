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
  ImportResultDto,
  InvestigationDeleteResultDto,
  InvestigationDto,
  LlamaSmokeResultDto,
  MatchProposalDto,
  OllamaHealthDto,
  LotOptionDto,
  RelationshipProposalDto,
  SourceContentDto,
  TraceResultDto,
  WorkspaceDto
} from "./types";

const API_KEY_STORAGE = "recallgraph.apiKey";

// Deployment-aware API base: empty string = same-origin (Vite dev proxy, or the API serving this UI itself).
// Set VITE_API_BASE at build time when the UI is hosted separately from the API (e.g. GraffGrid Pages -> Render).
const API_BASE: string = (import.meta.env.VITE_API_BASE as string | undefined)?.replace(/\/$/, "") ?? "";

export const publicDemoMode: boolean = import.meta.env.VITE_PUBLIC_DEMO === "1" || (typeof window !== "undefined" && window.location.pathname.startsWith("/demo"));

const resolveUrl = (path: string): string => (path.startsWith("/") ? `${API_BASE}${path}` : path);

const request = async (path: string, init: RequestInit = {}): Promise<Response> => {
  const headers = new Headers(init.headers);
  const key = sessionStorage.getItem(API_KEY_STORAGE);
  if (key) headers.set("X-Api-Key", key);
  const res = await fetch(resolveUrl(path), { ...init, headers });
  if (!res.ok) {
    if (res.status === 401) throw new Error("Unauthorized. Enter the pilot API key to continue.");
    throw new Error((await res.text()) || res.statusText);
  }
  return res;
};

const json = async (path: string, init?: RequestInit) => (await request(path, init)).json();

export const api = {
  hasKey: () => Boolean(sessionStorage.getItem(API_KEY_STORAGE)),
  setKey: (key: string) => { if (key) sessionStorage.setItem(API_KEY_STORAGE, key); else sessionStorage.removeItem(API_KEY_STORAGE); },
  health: () => json("/api/health") as Promise<HealthDto>,
  home: () => json("/api/workspace/home") as Promise<WorkspaceDto>,
  documents: (investigationId?: string) => json(`/api/documents${investigationId ? `?investigationId=${encodeURIComponent(investigationId)}` : ""}`) as Promise<DocumentDto[]>,
  document: (id: string) => json(`/api/documents/${id}`) as Promise<DocumentDto>,
  upload: (file: File, investigationId?: string): Promise<ImportResultDto> => {
    const fd = new FormData();
    fd.append("file", file);
    if (investigationId) fd.append("investigationId", investigationId);
    return json("/api/documents/upload", { method: "POST", body: fd }) as Promise<ImportResultDto>;
  },
  facts: (investigationId?: string) => json(`/api/workspace/facts${investigationId ? `?investigationId=${encodeURIComponent(investigationId)}` : ""}`) as Promise<FactDto[]>,
  sourceContent: (documentId: string, locator: string, investigationId?: string) =>
    json(`/api/documents/${documentId}/content?locator=${encodeURIComponent(locator)}${investigationId ? `&investigationId=${encodeURIComponent(investigationId)}` : ""}`) as Promise<SourceContentDto>,
  workspaceFacts: () => json("/api/workspace/facts") as Promise<FactDto[]>,
  patchFact: (id: string, body: unknown) =>
    json(`/api/facts/${id}`, { method: "PATCH", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) }) as Promise<FactDto>,
  factHistory: (id: string) => json(`/api/facts/${id}/history`) as Promise<FactHistoryDto[]>,
  confirmFact: (id: string, reviewer: string, reason = "") =>
    json(`/api/facts/${id}/confirm`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reviewer, notes: reason }) }) as Promise<FactDto>,
  rejectFact: (id: string, reviewer: string, reason = "") =>
    json(`/api/facts/${id}/reject`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reviewer, notes: reason }) }) as Promise<FactDto>,
  lots: (query?: string) => json(`/api/workspace/lots${query ? `?query=${encodeURIComponent(query)}` : ""}`) as Promise<LotOptionDto[]>,
  investigations: () => json("/api/investigations") as Promise<InvestigationDto[]>,
  createInvestigation: (body: { name: string; reference: string; suspectLotType: string; suspectLotId: string; description: string; status: string; actor: string }) =>
    json("/api/investigations", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) }) as Promise<InvestigationDto>,
  deleteInvestigation: (id: string, actor: string) =>
    json(`/api/investigations/${id}?actor=${encodeURIComponent(actor)}`, { method: "DELETE" }) as Promise<InvestigationDeleteResultDto>,
  matches: (refresh = false) => json(`/api/match-proposals${refresh ? "?refresh=true" : ""}`) as Promise<MatchProposalDto[]>,
  aliases: () => json("/api/entity-aliases") as Promise<EntityAliasDto[]>,
  decideMatch: (id: string, action: "confirm" | "reject" | "defer", actor: string, reason: string) =>
    json(`/api/match-proposals/${id}/${action}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ actor, reason }) }) as Promise<MatchProposalDto>,
  relationshipLinks: (investigationId?: string) =>
    json(`/api/relationship-proposals${investigationId ? `?investigationId=${encodeURIComponent(investigationId)}` : ""}`) as Promise<RelationshipProposalDto[]>,
  generateRelationshipLinks: (investigationId?: string) =>
    json(`/api/relationship-proposals/generate${investigationId ? `?investigationId=${encodeURIComponent(investigationId)}` : ""}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ investigationId: investigationId ?? null })
    }) as Promise<{ generatedCount: number; existingCount: number; existingLinkCount: number; existingFactKeyHitCount: number; skippedUngroundedCount: number; proposals: RelationshipProposalDto[] }>,
  decideRelationshipLink: (id: string, action: "confirm" | "reject", reviewer: string, notes: string) =>
    json(`/api/relationship-proposals/${id}/${action}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reviewer, notes }) }) as Promise<RelationshipProposalDto>,
  gaps: (refresh = false) => json(`/api/gaps${refresh ? "?refresh=true" : ""}`) as Promise<GapFindingDto[]>,
  runTrace: (body: { investigationId?: string; selectedType: string; selectedId: string; direction: string }) =>
    json("/api/trace-runs", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) }) as Promise<TraceResultDto>,
  traceRuns: () => json("/api/trace-runs") as Promise<{ id: string; selectedType: string; selectedId: string; completedAt: string; direction: string; status: string; gapCount: number; massBalanceStatus: string }[]>,
  traceRun: (id: string) => json(`/api/trace-runs/${id}`) as Promise<TraceResultDto>,
  audits: () => json("/api/workspace/audits") as Promise<AuditEventDto[]>,
  aiStatus: () => json("/api/extraction/status") as Promise<AiProviderStatusDto>,
  capabilities: () => json("/api/capabilities") as Promise<CapabilityStatusDto[]>,
  aiHealth: () => json("/api/extraction/health") as Promise<OllamaHealthDto>,
  aiSmoke: () => json("/api/extraction/smoke", { method: "POST" }) as Promise<LlamaSmokeResultDto>
};
