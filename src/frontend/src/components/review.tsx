import { useState, type ReactNode } from "react";
import type { FactDto, FactHistoryDto, MatchProposalDto } from "../types";
import {
  compareMatchAttributes,
  diffIdentifierChars,
  entityTypeLabel,
  extractionLabel,
  formatValidationOutcome,
  hasSourceLocator,
  historyDiff,
  parseSignals,
  reasonRequiredForConfirm,
  reviewActionLabel,
  reviewStateOf,
  syntheticSourceHint,
  syntheticSourceLabel
} from "../reviewLogic";
import Icon from "./Icon";
import { EmptyState, SourceLocatorChip, StateBadge, StatusBadge } from "./primitives";

export function LotDiffBadge({ value, compareTo }: { value: string; compareTo: string }) {
  const segments = diffIdentifierChars(value, compareTo);
  return (
    <span className="lot-diff-code" title={`Identifier comparison: ${value} vs ${compareTo}`}>
      {segments.map((seg, idx) =>
        seg.isDiff ? (
          <mark key={idx} className="lot-diff-mark">{seg.char}</mark>
        ) : (
          <span key={idx}>{seg.char}</span>
        )
      )}
    </span>
  );
}

export function DeskStateBadge({ fact }: { fact: FactDto }) {
  return <StateBadge state={reviewStateOf(fact)} />;
}

export function DeskClaimEmpty() {
  return (
    <EmptyState
      icon="check-circle"
      title="No claims in this queue"
      body="Select a different review status filter above or import additional evidence documents."
    />
  );
}

export function ShortcutHelpBody() {
  return (
    <dl className="shortcut-grid">
      <dt><kbd>J</kbd> / <kbd>K</kbd></dt>
      <dd>Move to the next or previous claim in the review queue</dd>
      <dt><kbd>C</kbd></dt>
      <dd>Open the Confirm decision panel for the selected claim</dd>
      <dt><kbd>E</kbd></dt>
      <dd>Open the Edit panel to refine extracted values</dd>
      <dt><kbd>R</kbd></dt>
      <dd>Open the Reject panel (requires a documented reason)</dd>
      <dt><kbd>H</kbd></dt>
      <dd>Show or hide the append-only audit history for this claim</dd>
      <dt><kbd>Esc</kbd></dt>
      <dd>Close the active decision form or help drawer</dd>
      <dt><kbd>?</kbd></dt>
      <dd>Open this keyboard shortcut reference</dd>
    </dl>
  );
}

export function ProvenanceRibbon({ fact, onOpenSource }: { fact: FactDto; onOpenSource?: () => void }) {
  const extraction = extractionLabel(fact);
  const state = reviewStateOf(fact);
  const validation = formatValidationOutcome(fact.validationOutcome, fact.reviewStatus);
  return (
    <div className="provenance-ribbon" aria-label="Claim provenance and verification status">
      <div className="prov-cell" title={extraction.hint}>
        <span className="prov-label">Extraction method</span>
        <span className="prov-value">{extraction.label}</span>
      </div>
      <div className="prov-cell" title={syntheticSourceHint(fact)}>
        <span className="prov-label">Data provenance</span>
        <span className="prov-value">{syntheticSourceLabel(fact)}</span>
      </div>
      <div className="prov-cell">
        <span className="prov-label">Source locator</span>
        <span className="prov-value">
          <SourceLocatorChip
            documentName={fact.sourceDocumentName}
            locator={hasSourceLocator(fact) ? fact.sourceLocation : undefined}
            synthetic={fact.sourceKind === "SeededFixture"}
            onOpen={onOpenSource}
          />
        </span>
      </div>
      <div className="prov-cell">
        <span className="prov-label">Verification status</span>
        <span className="prov-value">
          <StatusBadge tone={validation.tone}>{validation.label}</StatusBadge>
        </span>
      </div>
      <div className="prov-cell">
        <span className="prov-label">Review state</span>
        <StateBadge state={state} />
      </div>
      {fact.reviewReason && (
        <div className="prov-cell prov-cell-wide">
          <span className="prov-label">Reviewer note</span>
          <span className="prov-value">“{fact.reviewReason}”</span>
        </div>
      )}
    </div>
  );
}

export function ReviewActionGroup({
  fact,
  busy,
  panel,
  onOpen
}: {
  fact: FactDto;
  busy?: boolean;
  panel: "confirm" | "edit" | "reject" | null;
  onOpen: (action: "confirm" | "edit" | "reject") => void;
}) {
  const needsReason = reasonRequiredForConfirm(fact);
  const hasLoc = hasSourceLocator(fact);
  return (
    <div className="review-action-group" role="group" aria-label={`Review actions for ${fact.claimSubject || "claim"}`}>
      <button
        type="button"
        className={`button button-small ${panel === "confirm" ? "button-primary" : "button-secondary"}`}
        disabled={busy || fact.reviewStatus === "Confirmed"}
        aria-pressed={panel === "confirm"}
        title={hasLoc ? "Confirm this claim against its linked source line" : "Source line is not linked; a brief verification note is required to confirm"}
        onClick={() => onOpen("confirm")}
      >
        <Icon name="check" width={14} height={14} /> Confirm{needsReason ? " with note" : ""} <kbd>C</kbd>
      </button>
      <button
        type="button"
        className={`button button-small ${panel === "edit" ? "button-primary" : "button-secondary"}`}
        disabled={busy}
        aria-pressed={panel === "edit"}
        onClick={() => onOpen("edit")}
      >
        Edit <kbd>E</kbd>
      </button>
      <button
        type="button"
        className="button button-small button-danger"
        disabled={busy || fact.reviewStatus === "Rejected"}
        aria-pressed={panel === "reject"}
        onClick={() => onOpen("reject")}
      >
        Reject <kbd>R</kbd>
      </button>
    </div>
  );
}

export function HistoryList({ events }: { events: FactHistoryDto[] }) {
  if (events.length === 0) {
    return <p className="cell-meta">No review decisions have been recorded for this claim yet.</p>;
  }
  return (
    <ol className="history-list">
      {events.map((event) => {
        const changes = historyDiff(event.previousValuesJson, event.newValuesJson);
        return (
          <li key={event.id} className="history-event">
            <div className="history-event-head">
              <StatusBadge tone={event.action === "confirmed" ? "positive" : event.action === "rejected" ? "critical" : "info"}>
                {reviewActionLabel(event.action)}
              </StatusBadge>
              <span className="cell-meta">
                by <strong>{event.reviewer || "Reviewer"}</strong> · {new Date(event.createdAt).toLocaleString("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" })}
              </span>
            </div>
            {event.reason && <p className="history-reason">“{event.reason}”</p>}
            {changes.length > 0 && (
              <ul className="history-changes">
                {changes.map((c) => (
                  <li key={c.field}>
                    <span className="cell-meta">{c.field}:</span>{" "}
                    <span className="history-from">{c.from}</span> → <strong className="history-to">{c.to}</strong>
                  </li>
                ))}
              </ul>
            )}
          </li>
        );
      })}
    </ol>
  );
}

export function MatchComparison({
  proposal,
  facts,
  busy,
  onDecide,
  actions,
  highlighted = false
}: {
  proposal: MatchProposalDto;
  facts: FactDto[];
  busy?: boolean;
  onDecide?: (action: "confirm" | "reject" | "defer", reason: string) => Promise<void> | void;
  actions?: ReactNode;
  highlighted?: boolean;
}) {
  const [signalsOpen, setSignalsOpen] = useState(false);
  const [openDecision, setOpenDecision] = useState<"confirm" | "reject" | "defer" | null>(null);
  const [reason, setReason] = useState("");
  const rows = compareMatchAttributes(proposal, facts);
  const signals = parseSignals(proposal.signalsJson);
  const state = proposal.state === "Confirmed"
    ? "confirmed"
    : proposal.state === "Rejected"
      ? "rejected"
      : proposal.state === "Deferred"
        ? "unknown"
        : "candidate";

  const submitDecision = async () => {
    if (!openDecision || !onDecide) return;
    if ((openDecision === "confirm" || openDecision === "reject") && !reason.trim()) return;
    await onDecide(openDecision, reason.trim());
    setOpenDecision(null);
    setReason("");
  };

  return (
    <article className={`panel match-card${highlighted ? " is-highlighted" : ""}`} id={`match-${proposal.id}`}>
      <div className="panel-head">
        <div>
          <span className="eyebrow">{entityTypeLabel(proposal.entityType)} · Near-Match Review</span>
          <h2 className="match-values">
            <LotDiffBadge value={proposal.leftValue} compareTo={proposal.rightValue} />
            <span className="cell-meta">vs</span>
            <LotDiffBadge value={proposal.rightValue} compareTo={proposal.leftValue} />
          </h2>
        </div>
        <div className="panel-head-badges">
          <StateBadge state={state} />
          <StatusBadge
            tone="warning"
            title="Flagged by character similarity analysis (such as confusable 0/O or 1/I characters). Requires human verification."
          >
            Character Similarity Match
          </StatusBadge>
        </div>
      </div>

      <div className="match-columns" role="group" aria-label="Side by side identifier comparison">
        <div className="match-column">
          <span className="match-column-label">Canonical Registry Identifier</span>
          <div className="match-column-code">
            <LotDiffBadge value={proposal.leftValue} compareTo={proposal.rightValue} />
          </div>
          <span className="cell-meta">{entityTypeLabel(proposal.entityType)}</span>
        </div>
        <div className="match-column">
          <span className="match-column-label">Variant Found in Evidence</span>
          <div className="match-column-code">
            <LotDiffBadge value={proposal.rightValue} compareTo={proposal.leftValue} />
          </div>
          <span className="cell-meta">Highlighted characters differ from canonical code</span>
        </div>
      </div>

      <ul className="comparison-list" aria-label="Evidence comparison">
        {rows.map((r) => (
          <li key={r.label} className={`comparison-row comparison-${r.tone}`}>
            <span className="comparison-label">{r.label}</span>
            <span className="comparison-text">{r.text}</span>
          </li>
        ))}
      </ul>

      <p className="consequence-line">
        <strong>Impact of confirmation:</strong> Confirming records an official alias so claims citing{" "}
        <span className="run-lot">{proposal.rightValue}</span> resolve to canonical lot{" "}
        <span className="run-lot">{proposal.leftValue}</span> in traceability runs. Original source text is never altered.
      </p>

      {signals.length > 0 && (
        <details className="signal-details" open={signalsOpen} onToggle={(e) => setSignalsOpen((e.currentTarget as HTMLDetailsElement).open)}>
          <summary>Detection signals ({signals.length})</summary>
          <ul className="signal-list">
            {signals.map((s) => <li key={s}>{s}</li>)}
          </ul>
        </details>
      )}

      {proposal.reason && (
        <p className="cell-meta">
          <strong>Recorded decision note:</strong> “{proposal.reason}” {proposal.decidedBy ? `· ${proposal.decidedBy}` : ""}
        </p>
      )}

      {onDecide && (
        <div className="match-decision-area">
          <div className="row-actions">
            <button
              type="button"
              className="button button-small button-primary"
              disabled={busy || proposal.state === "Confirmed"}
              onClick={() => { setOpenDecision("confirm"); setReason(""); }}
            >
              <Icon name="check" width={14} height={14} /> Confirm Alias
            </button>
            <button
              type="button"
              className="button button-small button-danger"
              disabled={busy || proposal.state === "Rejected"}
              onClick={() => { setOpenDecision("reject"); setReason(""); }}
            >
              Reject Match
            </button>
            <button
              type="button"
              className="button button-small"
              disabled={busy || proposal.state === "Deferred"}
              onClick={() => { setOpenDecision("defer"); setReason(""); }}
            >
              Defer
            </button>
          </div>

          {openDecision && (
            <div className="review-panel match-decision-form">
              <label className="field">
                <span>
                  {openDecision === "confirm"
                    ? "Verification note for confirming this alias (required)"
                    : openDecision === "reject"
                      ? "Reason for rejecting this match proposal (required)"
                      : "Note for deferring this decision (optional)"}
                </span>
                <textarea
                  rows={2}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="e.g. Verified physical pallet tag against warehouse intake log..."
                />
              </label>
              <div className="row-actions">
                <button
                  type="button"
                  className="button button-small button-primary"
                  disabled={busy || ((openDecision === "confirm" || openDecision === "reject") && !reason.trim())}
                  onClick={() => void submitDecision()}
                >
                  Submit decision
                </button>
                <button type="button" className="button button-small" disabled={busy} onClick={() => setOpenDecision(null)}>
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {actions}
    </article>
  );
}
