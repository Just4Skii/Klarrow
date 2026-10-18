import { useEffect, useRef, useState, type ReactNode } from "react";
import Icon, { type IconName } from "./Icon";

export type Tone = "neutral" | "positive" | "warning" | "critical" | "info";

export function StatusBadge({ tone = "neutral", children, title }: { tone?: Tone; children: ReactNode; title?: string }) {
  return (
    <span className={`badge badge-${tone}`} title={title}>
      {children}
    </span>
  );
}

export type TraceState = "confirmed" | "candidate" | "inferred" | "rejected" | "conflict" | "missing" | "failed" | "unknown";

const stateMeta: Record<TraceState, { label: string; icon: IconName; hint: string }> = {
  confirmed: { label: "Confirmed", icon: "check-circle", hint: "Reviewed and verified against primary source evidence." },
  candidate: { label: "Needs review", icon: "question-circle", hint: "Candidate record extracted from evidence; awaiting human verification." },
  inferred: { label: "Inferred (unconfirmed)", icon: "branch", hint: "Derived by rule from parsed evidence; not human-confirmed." },
  rejected: { label: "Rejected", icon: "x-circle", hint: "Reviewed and excluded from active traceability paths." },
  conflict: { label: "Conflict", icon: "alert-diamond", hint: "Conflicting quantities or attributes detected across records." },
  missing: { label: "Missing evidence", icon: "dashed-circle", hint: "Expected downstream or upstream record has not been located yet." },
  failed: { label: "Processing failed", icon: "alert-triangle", hint: "The document parser could not extract structured text from this file." },
  unknown: { label: "Unverified", icon: "dashed-circle", hint: "Verification status has not been evaluated yet." }
};

export function StateBadge({ state, title }: { state: TraceState; title?: string }) {
  const meta = stateMeta[state];
  return (
    <span className={`state state-${state}`} title={title ?? meta.hint}>
      <Icon name={meta.icon} width={14} height={14} />
      <span>{meta.label}</span>
    </span>
  );
}

export type Severity = "critical" | "high" | "medium" | "low";

export function SeverityTag({ severity }: { severity: Severity }) {
  const bars = severity === "critical" ? 4 : severity === "high" ? 3 : severity === "medium" ? 2 : 1;
  const safeSeverity: Severity = (["critical", "high", "medium", "low"] as Severity[]).includes(severity) ? severity : "low";
  return (
    <span className={`severity severity-${safeSeverity}`} title={`${safeSeverity} severity`}>
      <span className="severity-bars" aria-hidden="true">
        {[0, 1, 2, 3].map((i) => <span key={i} className={i < bars ? "on" : ""} />)}
      </span>
      <span>{safeSeverity[0].toUpperCase() + safeSeverity.slice(1)}</span>
    </span>
  );
}

export function formatLocatorLabel(locator?: string): string {
  if (!locator) return "";
  const trimmed = locator.trim();
  const singleLine = /^lines?\s+(\d+)-\1$/i.exec(trimmed);
  if (singleLine) return `Line ${singleLine[1]}`;
  const multiLine = /^lines?\s+(\d+)-(\d+)$/i.exec(trimmed);
  if (multiLine) return `Lines ${multiLine[1]}-${multiLine[2]}`;
  const singleL = /^line\s+(\d+)$/i.exec(trimmed);
  if (singleL) return `Line ${singleL[1]}`;
  const pageMatch = /^page\s+(\d+)$/i.exec(trimmed);
  if (pageMatch) return `Page ${pageMatch[1]}`;
  const sheetRow = /^sheet\s+([^,]+),\s*row\s+(\d+)$/i.exec(trimmed) || /^([^!]+)!row\s+(\d+)$/i.exec(trimmed);
  if (sheetRow) return `${sheetRow[1]} · Row ${sheetRow[2]}`;
  return trimmed;
}

export function SourceLocatorChip({ documentName, locator, synthetic, onOpen }: { documentName: string; locator?: string; synthetic?: boolean; onOpen?: () => void }) {
  const cleanLoc = formatLocatorLabel(locator);
  const text = cleanLoc ? `${documentName} · ${cleanLoc}` : documentName || "Source not linked";
  const content = (
    <>
      <Icon name={cleanLoc ? "file" : "dashed-circle"} width={13} height={13} />
      <span className="locator-text" title={text}>{text}</span>
      {synthetic && <span className="locator-synthetic" title="Source document is a seeded synthetic demonstration record.">Synthetic</span>}
    </>
  );
  if (onOpen) {
    return (
      <button type="button" className={`locator-chip${cleanLoc ? "" : " locator-missing"}`} onClick={onOpen} title={`Preview source: ${text}`}>
        {content}
      </button>
    );
  }
  return <span className={`locator-chip${cleanLoc ? "" : " locator-missing"}`} title={text}>{content}</span>;
}

export function QuantityDisplay({ quantity, unit }: { quantity?: number | null; unit?: string | null }) {
  if (quantity === null || quantity === undefined) return <span className="qty-missing">Not recorded</span>;
  const formatted = new Intl.NumberFormat("en-US", { maximumFractionDigits: 4 }).format(quantity);
  if (!unit) return <span className="qty-unit-missing">{formatted} (unit unspecified)</span>;
  return (
    <span className="qty">
      <span className="qty-number">{formatted}</span> <span className="qty-unit">{unit}</span>
    </span>
  );
}

export function WarningBanner({ tone = "info", title, children, action }: { tone?: "info" | "caution" | "critical"; title: string; children?: ReactNode; action?: ReactNode }) {
  const icon: IconName = tone === "critical" ? "alert-triangle" : tone === "caution" ? "alert" : "info";
  return (
    <div className={`warning-banner warning-${tone}`} role={tone === "critical" ? "alert" : "note"}>
      <Icon name={icon} width={18} height={18} />
      <div className="warning-body">
        <strong>{title}</strong>
        {children && <div>{children}</div>}
      </div>
      {action && <div className="warning-action">{action}</div>}
    </div>
  );
}

export function MetricTile({ label, value, hint, tone = "neutral" }: { label: string; value: ReactNode; hint?: string; tone?: Tone }) {
  return (
    <div className={`metric metric-${tone}`}>
      <span className="metric-label">{label}</span>
      <span className="metric-value">{value}</span>
      {hint && <span className="metric-hint">{hint}</span>}
    </div>
  );
}

export function EmptyState({ icon = "inbox", title, body, action }: { icon?: IconName; title: string; body: string; action?: ReactNode }) {
  return (
    <div className="empty-state">
      <span className="empty-icon" aria-hidden="true">
        <Icon name={icon} width={22} height={22} />
      </span>
      <div>
        <h3>{title}</h3>
        <p>{body}</p>
        {action && <div className="empty-action">{action}</div>}
      </div>
    </div>
  );
}

export function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <button
      type="button"
      className="copy-button"
      aria-label={label}
      title={label}
      onClick={() => {
        void navigator.clipboard?.writeText(value).then(
          () => { setCopied(true); window.setTimeout(() => setCopied(false), 2000); },
          () => undefined
        );
      }}
    >
      <Icon name="copy" width={14} height={14} />
      <span aria-live="polite">{copied ? "Copied" : ""}</span>
    </button>
  );
}

export function useFocusTrap(active: boolean) {
  const ref = useRef<HTMLElement>(null);
  useEffect(() => {
    if (!active) return;
    const el = ref.current;
    if (!el) return;
    const items = () => Array.from(el.querySelectorAll<HTMLElement>('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')).filter((x) => !x.hasAttribute("disabled"));
    items()[0]?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Tab") return;
      const list = items();
      if (list.length === 0) return;
      const first = list[0];
      const last = list[list.length - 1];
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    };
    el.addEventListener("keydown", onKey);
    return () => { el.removeEventListener("keydown", onKey); };
  }, [active]);
  return ref;
}

export function LoadingPanel({ label = "Loading workspace" }: { label?: string }) {
  return (
    <div className="loading-panel" role="status">
      <span className="spinner" aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}
