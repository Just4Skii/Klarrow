import type { FactDto, MatchProposalDto } from "./types";

export function normalizeLot(value: string | null | undefined): string {
  if (!value) return "";
  return value.trim().toUpperCase().replace(/\s+/g, " ");
}

export function nearMatchKey(value: string | null | undefined): string {
  return normalizeLot(value).replace(/O/g, "0").replace(/I/g, "1").replace(/L/g, "1").replace(/-/g, "").replace(/ /g, "");
}

export function hasSourceLocator(fact: FactDto): boolean {
  if (!fact.sourceLocationId) return false;
  return !/not recorded/i.test(fact.sourceLocation || "");
}

export function reasonRequiredForConfirm(fact: FactDto): boolean {
  return !hasSourceLocator(fact);
}

export function reviewStateOf(fact: FactDto): "confirmed" | "candidate" | "inferred" | "rejected" {
  if (fact.reviewStatus === "Confirmed") return "confirmed";
  if (fact.reviewStatus === "Rejected") return "rejected";
  if (fact.reviewStatus === "Edited") return "inferred";
  return "candidate";
}

export function syntheticSourceLabel(fact: FactDto): string {
  switch (fact.sourceKind) {
    case "SeededFixture": return "Synthetic (seeded demonstration)";
    case "ManualEntry": return "Operator manual entry";
    case "DemoParser": return "Synthetic (fixture parser)";
    default: return "Operator supplied record";
  }
}

export function syntheticSourceHint(fact: FactDto): string {
  switch (fact.sourceKind) {
    case "SeededFixture":
    case "DemoParser":
      return "This claim comes from a seeded synthetic demonstration record. It is not real-world evidence and must not be used for operational food-safety decisions.";
    case "ManualEntry":
      return "This claim was entered manually by an operator and requires the same verification as any extracted claim.";
    default:
      return "This claim comes from an operator-supplied document in this workspace.";
  }
}

export function formatFactType(factType: string | null | undefined): string {
  switch (factType) {
    case "SupplierDelivery": return "Supplier Delivery";
    case "GoodsReceipt": return "Goods Receipt";
    case "StockLedger": return "Stock Ledger";
    case "ProductionBatch": return "Production Batch";
    case "ReworkEntry": return "Rework Entry";
    case "FinishedLot": return "Finished Product Lot";
    case "Dispatch": return "Customer Dispatch";
    case "QualityTest": return "Laboratory Analysis";
    case "Invoice": return "Commercial Invoice";
    case "Note": return "Investigation Note";
    case "Other": return "General Record";
    default: return factType ? factType.replace(/([a-z])([A-Z])/g, "$1 $2") : "General Record";
  }
}

export function formatClaimType(claimType: string | null | undefined): string {
  const raw = (claimType || "").trim().toLowerCase();
  if (!raw || raw === "source line") return "Document Line Extract";
  if (raw === "spreadsheet row") return "Spreadsheet Row";
  if (raw === "pdf line") return "PDF Text Extract";
  if (raw === "scan unreadable") return "Unreadable Scan Page";
  if (raw === "image retained") return "Retained Image";
  if (raw === "llm extracted") return "AI Model Claim";
  return raw
    .replace(/[_-]+/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

export function formatValidationOutcome(outcome: string | null | undefined, reviewStatus?: string): { label: string; tone: "positive" | "warning" | "neutral" } {
  if (reviewStatus === "Confirmed") return { label: "Human Verified", tone: "positive" };
  if (reviewStatus === "Edited") return { label: "Reviewer Edited", tone: "positive" };
  const raw = (outcome || "").trim().toLowerCase();
  if (raw === "schema-validated") return { label: "Schema Valid Only", tone: "neutral" };
  if (raw === "quote_verified") return { label: "Value In Quote - Verified", tone: "positive" };
  if (raw === "context_match_value_adjacent") return { label: "Context Match - Value Adjacent", tone: "warning" };
  if (raw === "identifier_matched_quote_unverified") return { label: "Identifier Match Only - Quote Not Located", tone: "warning" };
  if (raw === "needs_review_unverified_source") return { label: "Unverified Source", tone: "warning" };
  if (raw === "unvalidated" || !raw) return { label: "Pending Verification", tone: "warning" };
  return {
    label: raw.replace(/[_-]+/g, " ").replace(/\b\w/g, (c) => c.toUpperCase()),
    tone: "neutral"
  };
}

export function extractionLabel(fact: FactDto): { label: string; shortLabel: string; hint: string } {
  const method = (fact.extractionMethod || "").trim();
  if (!method) {
    return {
      label: "Structured Document Parser",
      shortLabel: "Document Parser",
      hint: "Extracted from structured document lines and fields. Requires human review."
    };
  }
  if (method === "deterministic-parser") {
    return {
      label: "Structured Document Parser",
      shortLabel: "Document Parser",
      hint: "Parsed deterministically from structured document lines, tables, or logs. Requires human review."
    };
  }
  if (method === "ai-extraction") {
    const model = fact.modelId ? ` (${fact.modelId})` : "";
    return {
      label: `AI Model Extraction${model}`,
      shortLabel: fact.modelId ? `AI · ${fact.modelId}` : "AI Extraction",
      hint: "Extracted by the configured language model and validated against the traceability schema. Requires human review."
    };
  }
  return {
    label: method.replace(/[_-]+/g, " ").replace(/\b\w/g, (c) => c.toUpperCase()),
    shortLabel: method.replace(/[_-]+/g, " ").replace(/\b\w/g, (c) => c.toUpperCase()),
    hint: "Extraction method recorded by the ingestion pipeline."
  };
}

export function entityTypeLabel(entityType: string | null | undefined): string {
  const raw = (entityType || "").trim().toLowerCase();
  if (raw === "raw-lot" || raw === "raw_lot") return "Raw Material Lot";
  if (raw === "finished-lot" || raw === "finished_lot") return "Finished Product Lot";
  if (raw === "production-batch" || raw === "production_batch") return "Production Batch";
  if (raw === "supplier") return "Supplier";
  if (raw === "customer") return "Retail / Customer Destination";
  if (raw === "gap") return "Unresolved Evidence Gap";
  if (raw === "workspace") return "Investigation Workspace";
  if (!raw) return "Traceability Entity";
  return raw.replace(/[-_]/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
}

export function formatLinkKind(kind: string | null | undefined): string {
  const raw = (kind || "").trim().toLowerCase();
  if (!raw) return "Traceability Link";
  if (raw === "supplier_delivery") return "Supplier Delivery";
  if (raw === "goods_receipt") return "Goods Receipt";
  if (raw === "raw_to_batch") return "Issued to Batch";
  if (raw === "batch_to_finished") return "Packed into Finished Lot";
  if (raw === "customer_dispatch" || raw === "dispatch") return "Dispatched to Customer";
  if (raw === "rework" || raw === "rework_entry") return "Rework Inclusion";
  if (raw === "missing_downstream_dispatch") return "Missing Downstream Dispatch";
  return raw.replace(/[_-]+/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
}

export function formatFindingKind(kind: string | null | undefined): string {
  const raw = (kind || "").trim().toLowerCase();
  if (raw === "missing-downstream-evidence") return "Missing Downstream Dispatch";
  if (raw === "quantity-mismatch") return "Quantity Discrepancy";
  if (raw === "missing-upstream-evidence") return "Missing Upstream Input";
  if (raw === "ambiguous-identifier") return "Confusable Lot Identifier";
  if (raw === "unreviewed-candidates") return "Unreviewed Candidate Claims";
  return raw.replace(/[_-]+/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
}

export function formatCheckName(check: string | null | undefined): string {
  const raw = (check || "").trim().toLowerCase();
  if (raw === "trace-link-gap") return "Downstream Chain Continuity";
  if (raw === "received-vs-consumed-quantity") return "Mass Balance Reconciliation";
  if (raw === "batch-input-link") return "Batch Input Completeness";
  if (raw === "near-match-identifier") return "Identifier Similarity Guard";
  if (raw === "fact-review-status") return "Human Review Verification";
  return raw.replace(/[_-]+/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
}

export function formatAuditEventType(eventType: string | null | undefined): string {
  const raw = (eventType || "").trim().toLowerCase();
  switch (raw) {
    case "trace_exported": return "Trace Package Exported";
    case "trace_run": return "Traceability Run Executed";
    case "match_confirmed": return "Identifier Alias Confirmed";
    case "match_rejected": return "Identifier Match Rejected";
    case "match_deferred": return "Identifier Match Deferred";
    case "fact_confirmed": return "Claim Confirmed";
    case "fact_rejected": return "Claim Rejected";
    case "fact_edited": return "Claim Edited";
    case "document_imported": return "Evidence Document Imported";
    case "document_duplicate_skipped": return "Duplicate Upload Prevented";
    case "investigation_created": return "Investigation Created";
    case "investigation_updated": return "Investigation Updated";
    case "extraction_run": return "AI Model Extraction Run";
    default:
      return raw.replace(/[_-]+/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
  }
}

export function formatAuditTargetType(targetType: string | null | undefined): string {
  switch (targetType) {
    case "ExtractedFact": return "Evidence Claim";
    case "SourceDocument": return "Source Document";
    case "MatchProposal": return "Identifier Match";
    case "AiExtractionRun": return "AI Extraction";
    case "TraceRun": return "Trace Snapshot";
    case "Investigation": return "Investigation";
    default:
      return targetType ? targetType.replace(/([a-z])([A-Z])/g, "$1 $2") : "System Record";
  }
}

export function formatShortId(id: string | null | undefined): string {
  if (!id) return "-";
  const clean = id.trim();
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(clean)) {
    return `#${clean.slice(0, 8).toUpperCase()}`;
  }
  return clean;
}

export function formatAuditDescription(description: string | null | undefined): string {
  if (!description) return "";
  return description
    .replace(/\b([0-9a-f]{8})-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi, (_, short: string) => `#${short.toUpperCase()}`)
    .replace(/\braw-lot\b/gi, "Raw Lot")
    .replace(/\bfinished-lot\b/gi, "Finished Lot")
    .replace(/\bproduction-batch\b/gi, "Production Batch");
}

export function formatShortDate(value: string | null | undefined): string {
  if (!value) return "Not recorded";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value.slice(0, 10);
  return d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
}

export function formatCleanValue(value: string | null | undefined): string {
  if (!value) return "-";
  const trimmed = value.trim();
  if (!trimmed) return "-";
  // Check if it's a CSV header row like document_ref,event_date,supplier...
  if (/^[a-z_]+(,[a-z_]+){2,}$/i.test(trimmed) || /^[a-z_]+(\t[a-z_]+){2,}$/i.test(trimmed)) {
    const cols = trimmed.split(/[,\t]/).map((c) => c.trim().replace(/_/g, " ").replace(/\b\w/g, (l) => l.toUpperCase()));
    return `Schema Columns: ${cols.join(" · ")}`;
  }
  // If it's a dense CSV or TSV data row without spaces around commas/tabs, format with clean middle-dot separators
  if ((trimmed.includes(",") && !trimmed.includes(", ") && trimmed.split(",").length >= 4) || trimmed.includes("\t")) {
    const parts = trimmed.split(/\t|,(?=(?:[^"]*"[^"]*")*[^"]*$)/).map((p) => p.replace(/^"|"$/g, "").trim()).filter(Boolean);
    if (parts.length >= 3) {
      return parts.join(" · ");
    }
  }
  return trimmed;
}

export function isSchemaHeaderFact(fact: FactDto): boolean {
  const raw = (fact.rawValue || "").trim();
  return /^[a-z_]+(,[a-z_]+){2,}$/i.test(raw) || /^[a-z_]+(\t[a-z_]+){2,}$/i.test(raw);
}

export interface CharSegment {
  char: string;
  isDiff: boolean;
}

export function diffIdentifierChars(primary: string, compareTo: string): CharSegment[] {
  const a = primary || "";
  const b = compareTo || "";
  const maxLen = Math.max(a.length, b.length);
  const segments: CharSegment[] = [];
  for (let i = 0; i < a.length; i++) {
    const ch = a[i];
    const other = i < maxLen ? b[i] : "";
    segments.push({
      char: ch,
      isDiff: ch.toUpperCase() !== (other || "").toUpperCase()
    });
  }
  return segments;
}

export interface ComparisonRow {
  label: string;
  text: string;
  tone: "same" | "differs" | "info";
}

function claimsFor(value: string, facts: FactDto[]): FactDto[] {
  const n = normalizeLot(value);
  if (!n) return [];
  return facts.filter((f) => normalizeLot(f.claimSubject) === n || normalizeLot(f.normalizedValue) === n || normalizeLot(f.rawValue) === n);
}

function formatQuantity(quantity: number, unit?: string | null): string {
  const value = new Intl.NumberFormat("en-US", { maximumFractionDigits: 4 }).format(quantity);
  return unit ? `${value} ${unit}` : `${value} (unit missing)`;
}

function quantityList(facts: FactDto[]): string[] {
  const seen = new Set<string>();
  for (const f of facts) {
    if (f.quantity === null || f.quantity === undefined) continue;
    seen.add(formatQuantity(f.quantity, f.quantityUnit));
  }
  return Array.from(seen);
}

function dateList(facts: FactDto[]): string[] {
  const seen = new Set<string>();
  for (const f of facts) {
    if (!f.eventDate) continue;
    seen.add(formatShortDate(f.eventDate));
  }
  return Array.from(seen);
}

function docNames(facts: FactDto[]): string[] {
  return Array.from(new Set(facts.map((f) => f.sourceDocumentName).filter(Boolean)));
}

function sameSet(a: string[], b: string[]): boolean {
  if (a.length !== b.length) return false;
  const sa = [...a].sort();
  const sb = [...b].sort();
  return sa.every((v, i) => v === sb[i]);
}

export function compareMatchAttributes(proposal: MatchProposalDto, facts: FactDto[]): ComparisonRow[] {
  const rows: ComparisonRow[] = [];
  const left = proposal.leftValue;
  const right = proposal.rightValue;

  rows.push({
    label: "Entity category",
    text: `Both identifiers belong to ${entityTypeLabel(proposal.entityType)}.`,
    tone: "info"
  });

  const normalizedEqual = normalizeLot(left) === normalizeLot(right);
  const confusableOnly = !normalizedEqual && nearMatchKey(left) === nearMatchKey(right);
  if (normalizedEqual) {
    rows.push({ label: "Character pattern", text: "Identical after standardizing whitespace and casing.", tone: "same" });
  } else if (confusableOnly) {
    rows.push({ label: "Character pattern", text: "Differs only in visually confusable characters (e.g. digit '0' vs letter 'O', or '1' vs 'I').", tone: "differs" });
  } else {
    rows.push({ label: "Character pattern", text: "Identifiers differ in alphanumeric character positions.", tone: "differs" });
  }

  const leftClaims = claimsFor(left, facts);
  const rightClaims = claimsFor(right, facts);
  const leftQty = quantityList(leftClaims);
  const rightQty = quantityList(rightClaims);
  if (leftQty.length > 0 && rightQty.length > 0) {
    rows.push(sameSet(leftQty, rightQty)
      ? { label: "Recorded quantity", text: `Matching quantity across claims: ${leftQty.join(", ")}.`, tone: "same" }
      : { label: "Recorded quantity", text: `${left} records ${leftQty.join(", ")} whereas ${right} records ${rightQty.join(", ")}.`, tone: "differs" });
  } else if (leftQty.length > 0) {
    rows.push({ label: "Recorded quantity", text: `${left} records ${leftQty.join(", ")}; no quantity is recorded on claims for ${right}.`, tone: "info" });
  } else if (rightQty.length > 0) {
    rows.push({ label: "Recorded quantity", text: `${right} records ${rightQty.join(", ")}; no quantity is recorded on claims for ${left}.`, tone: "info" });
  } else {
    rows.push({ label: "Recorded quantity", text: "No explicit quantity values recorded on claims for either identifier.", tone: "info" });
  }

  const leftDates = dateList(leftClaims);
  const rightDates = dateList(rightClaims);
  if (leftDates.length > 0 && rightDates.length > 0) {
    rows.push(sameSet(leftDates, rightDates)
      ? { label: "Transaction dates", text: `Matching event date: ${leftDates.join(", ")}.`, tone: "same" }
      : { label: "Transaction dates", text: `${left} is dated ${leftDates.join(", ")} vs ${right} dated ${rightDates.join(", ")}.`, tone: "differs" });
  } else if (leftDates.length > 0) {
    rows.push({ label: "Transaction dates", text: `${left} is dated ${leftDates.join(", ")}.`, tone: "info" });
  } else if (rightDates.length > 0) {
    rows.push({ label: "Transaction dates", text: `${right} is dated ${rightDates.join(", ")}.`, tone: "info" });
  }

  const leftDocs = docNames(leftClaims);
  const rightDocs = docNames(rightClaims);
  if (leftDocs.length > 0 && rightDocs.length > 0) {
    if (sameSet(leftDocs, rightDocs)) {
      rows.push({ label: "Source documents", text: `Both appear in ${leftDocs.join(", ")}.`, tone: "same" });
    } else {
      const short = (list: string[]) => list.length > 2 ? `${list.slice(0, 2).join(", ")} (+${list.length - 2} more)` : list.join(", ");
      rows.push({ label: "Source documents", text: `${left} appears in ${short(leftDocs)}; ${right} appears in ${short(rightDocs)}.`, tone: "differs" });
    }
  } else if (leftDocs.length > 0) {
    rows.push({ label: "Source documents", text: `${left} is cited in ${leftDocs.length} document${leftDocs.length === 1 ? "" : "s"} (${leftDocs.slice(0, 2).join(", ")}).`, tone: "info" });
  } else if (rightDocs.length > 0) {
    rows.push({ label: "Source documents", text: `${right} is cited in ${rightDocs.length} document${rightDocs.length === 1 ? "" : "s"} (${rightDocs.slice(0, 2).join(", ")}).`, tone: "info" });
  } else {
    rows.push({ label: "Source documents", text: "Referenced in canonical lot registry and warehouse exception logs.", tone: "info" });
  }

  return rows;
}

export interface EditDraft {
  claimSubject: string;
  rawValue: string;
  normalizedValue: string;
  quantity: string;
  quantityUnit: string;
  eventDate: string;
}

export function draftFromFact(fact: FactDto): EditDraft {
  return {
    claimSubject: fact.claimSubject ?? "",
    rawValue: fact.rawValue ?? "",
    normalizedValue: fact.normalizedValue ?? "",
    quantity: fact.quantity === null || fact.quantity === undefined ? "" : String(fact.quantity),
    quantityUnit: fact.quantityUnit ?? "",
    eventDate: fact.eventDate ? fact.eventDate.slice(0, 10) : ""
  };
}

export function parseDraftQuantity(value: string): number | null {
  const trimmed = value.trim().replace(",", ".");
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

export function buildFactPatch(fact: FactDto, draft: EditDraft): { patch: Record<string, unknown>; hasChanges: boolean; quantityInvalid: boolean } {
  const patch: Record<string, unknown> = {};
  const quantityInvalid = draft.quantity.trim() !== "" && parseDraftQuantity(draft.quantity) === null;
  if (draft.claimSubject.trim() !== (fact.claimSubject ?? "")) patch.claimSubject = draft.claimSubject.trim();
  if (draft.rawValue.trim() !== (fact.rawValue ?? "")) patch.rawValue = draft.rawValue.trim();
  if (draft.normalizedValue.trim() !== (fact.normalizedValue ?? "")) patch.normalizedValue = draft.normalizedValue.trim();
  if (draft.quantityUnit.trim() !== (fact.quantityUnit ?? "")) patch.quantityUnit = draft.quantityUnit.trim();
  if (!quantityInvalid && draft.quantity.trim() !== "") {
    const parsed = parseDraftQuantity(draft.quantity);
    if (parsed !== null && parsed !== fact.quantity) patch.quantity = parsed;
  }
  if (draft.eventDate && draft.eventDate !== (fact.eventDate ? fact.eventDate.slice(0, 10) : "")) patch.eventDate = draft.eventDate;
  return { patch, hasChanges: Object.keys(patch).length > 0, quantityInvalid };
}

const HISTORY_FIELDS: { key: string; label: string }[] = [
  { key: "ClaimSubject", label: "Subject / Lot" },
  { key: "RawValue", label: "Extracted value" },
  { key: "NormalizedValue", label: "Normalized lot" },
  { key: "Quantity", label: "Quantity" },
  { key: "QuantityUnit", label: "Unit" },
  { key: "EventDate", label: "Event date" },
  { key: "ReviewStatus", label: "Review status" },
  { key: "ReviewReason", label: "Review note" }
];

const REVIEW_STATUS_MAP: Record<string, string> = {
  "0": "Unreviewed",
  "1": "Confirmed",
  "2": "Edited",
  "3": "Rejected",
  "Unreviewed": "Unreviewed",
  "Confirmed": "Confirmed",
  "Edited": "Edited",
  "Rejected": "Rejected"
};

function formatHistoryFieldValue(key: string, val: unknown): string {
  if (val === null || val === undefined || val === "") return "None";
  const str = String(val);
  if (key === "ReviewStatus") {
    return REVIEW_STATUS_MAP[str] ?? str;
  }
  if (key === "EventDate") {
    return formatShortDate(str);
  }
  if (key === "Quantity") {
    const num = Number(str);
    return Number.isFinite(num) ? new Intl.NumberFormat("en-US", { maximumFractionDigits: 4 }).format(num) : str;
  }
  return formatCleanValue(str);
}

export interface HistoryChange {
  field: string;
  from: string;
  to: string;
}

export function historyDiff(previousJson: string, newJson: string): HistoryChange[] {
  let previous: Record<string, unknown> = {};
  let next: Record<string, unknown> = {};
  try { previous = JSON.parse(previousJson) as Record<string, unknown>; } catch { return []; }
  try { next = JSON.parse(newJson) as Record<string, unknown>; } catch { return []; }
  const changes: HistoryChange[] = [];
  for (const { key, label } of HISTORY_FIELDS) {
    const before = previous[key];
    const after = next[key];
    if (JSON.stringify(before) === JSON.stringify(after)) continue;
    const fromFormatted = formatHistoryFieldValue(key, before);
    const toFormatted = formatHistoryFieldValue(key, after);
    if (fromFormatted === toFormatted) continue;
    changes.push({
      field: label,
      from: fromFormatted,
      to: toFormatted
    });
  }
  return changes;
}

export function parseSignals(json: string): string[] {
  try {
    const value: unknown = JSON.parse(json);
    if (!Array.isArray(value)) return [];
    return value.map((v) => {
      const s = String(v);
      if (s === "normalized identifier similarity") return "Normalized alphanumeric lot pattern matches canonical lot";
      if (s.includes("visually confusable")) return "Contains visually confusable character substitution (e.g., letter 'O' vs digit '0')";
      return s.charAt(0).toUpperCase() + s.slice(1);
    });
  } catch {
    return [];
  }
}

export function reviewActionLabel(action: string): string {
  if (action === "confirmed") return "Confirmed";
  if (action === "rejected") return "Rejected";
  if (action === "edited") return "Edited";
  return action ? action.charAt(0).toUpperCase() + action.slice(1) : "Reviewed";
}
