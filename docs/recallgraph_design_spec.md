Klarrow - System Design Specification and DeepSeek Implementation Prompt
Prepared as senior product designer, UX architect and design-systems lead. Part 0 is the strategy. Part 1 is the full design specification. Part 2 is the single copy-paste prompt for DeepSeek V4.1 Flash in OpenCode.
Status tags used throughout: [Existing] = stated in your brief as implemented. [UI] = new presentation using existing data/APIs. [Backend?] = needs an API/model capability the brief does not prove exists. [Not verified] = cannot be confirmed from the brief; inspect at runtime.
PART 0 - STRATEGY SYNTHESIS
The reframing
The brief lists modules (Evidence, Facts, Trace, Audit). The user does not think in modules. A quality manager under recall pressure thinks: “I am investigating lot X. What do I know, how do I know it, what is shaky, and what do I do next?”
So the design has one organising idea: an Investigation is the container; every module is a lens on the same evidence graph. The atomic unit of trust is the claim with provenance (a fact plus the exact page/cell/line it came from). Matches, trace links, findings and the export are all built from claims and inherit their certainty. The UI must make that inheritance visible: a trace link is only as strong as the weakest claim or match beneath it.
Main design risks
1. Module sprawl. Five tabs of CRUD tables with no sense of “which case, what next”. Fix: investigation-scoped navigation plus a persistent “needs your decision” strip.
2. Uncertainty collapsing into decoration. Too many badges makes everything look equally alarming, so nothing is. Fix: a closed vocabulary of 7 states used identically everywhere; severity reserved for findings.
3. False reassurance. A green tick on mass balance or on “0 gaps” when data is simply missing. Fix: “Insufficient data” is a first-class neutral state; no success state for completeness unless every input is present.
4. Provenance buried. If the source locator is two clicks away, reviewers confirm blind. Fix: a locator chip on every claim row and trace hop, and a source viewer one click away.
5. Dishonest AI/model status. Configured is not reachable is not installed is not invoked. Fix: a four-step status ladder driven only by the health/smoke endpoints.
6. Synthetic demonstration data mistaken for real data. Fix: an API-driven workspace-mode indicator and a persistent synthetic-data banner, plus a “Synthetic” tag on seeded fixture records and synthetic-data provenance notices in exports.
7. Scope mismatch (key API risk). The brief says trace runs can link to an investigation, but does not say documents or facts do. If evidence is workspace-global, an “investigation-scoped Evidence tab” would be misleading. See Section J.
8. Data density on mobile. Wide tables with long lot IDs. Fix: card mode below 640px with the same fields in the same priority order.
Primary vs secondary information
Always visible (primary)
One click away (secondary)
On demand (technical)
Which investigation; suspect lot; workspace mode
Claim history timeline
Parser name/version, derived text path
Count of items needing my decision
Match comparison detail
Extraction provider/model/prompt version
Status of each claim/link (7-state vocabulary)
Trace hop inspector
SHA-256, processing error detail
Source locator chip on every claim
Mass-balance line items
Raw JSON of a finding
Unresolved findings count (persisted source)
Export options
Audit event raw payload
PART 1 - DESIGN SPECIFICATION
A. Product positioning and mental model
Positioning line (About/footer): “Klarrow reconstructs an evidence-backed traceability investigation from the records you already have. It shows what is supported, what may be related, where evidence disagrees, and what is missing.”
Never say: “AI-verified”, “compliant”, “certified”, “safe”, “cleared”, “recall-ready”. Say: “supported by”, “candidate”, “needs your review”, “insufficient data”.
Mental model (a five-step spine, reused as the investigation progress strip):
1. Sources (documents you added) →
2. Claims (facts read from sources, each with a locator) →
3. Matches (possible same-lot identifiers you decide on) →
4. Trace (forward/backward paths built from claims and matches) →
5. Findings & Export (gaps, conflicts, mass balance, audit pack)
Each step shows a count of items awaiting a decision, never a “% complete”. A percentage implies an end-state the system cannot prove.
B. Information architecture and navigation
Global header (sticky, 48px) [Existing shell, restructured]
• Left: Klarrow wordmark + “Evidence reconciliation workbench” subtitle (hidden below 640px).
• Nav (icon + label): Workspace, Investigations, System.
• Right: workspace-mode chip (“Synthetic workspace” amber outline / “Pilot workspace” teal outline, both driven by the API workspace mode) and, if API-key protection is on, a “Key set for this session” indicator with a Clear action.
Investigation shell (inside an investigation) adds a second sticky row (44px):
• Breadcrumb: Investigations / reference - name
• Context strip: Suspect lot (mono), status, “Last trace run: date”, “Needs your decision: N”.
• Tabs: Overview · Evidence · Claims · Matches · Trace · Findings · Audit · Export
• A count appears on a tab only when it is actionable (Claims awaiting review, Matches undecided, Findings unresolved). No count on Audit or Export.
Label mapping (keep routes and endpoints; change UI labels only): Facts → Claims; gap findings → Findings; match proposals → Matches.
Workspace-wide areas outside any investigation: Investigations list and System (model/extraction status, workspace mode, pilot/API-key, limitations). Evidence/Claims inside an investigation show a scope label; see Section J.
Deep linking: every investigation, document, claim, match, trace run and finding has a stable URL; filters live in the query string, so an auditor can be sent a link and reload restores state.
C. Design-system tokens
Colour roles
Role
Hex
Use
canvas
#F6F3EE
page background (warm neutral)
surface
#FFFFFF
cards, tables, panels
surface-sunken
#EFEBE4
table headers, inset panels, ID chips
border
#DDD6CB
default dividers
border-strong
#B9AF9F
inputs, emphasised separators
text
#1F2A28
primary text
text-muted
#55625F
secondary text (verify ≥4.5:1 on canvas and surface)
primary
#0F5F5C
primary buttons, active nav, links
primary-hover
#0B4B49
hover/pressed
primary-subtle
#DCEFEC
selected row, active tab background
on-primary
#FFFFFF
text on primary
focus-ring
#0F5F5C with 2px white gap
all interactive elements
disabled-fg / bg
#8A918F / #ECEAE6
disabled controls (with aria-disabled and a reason where useful)
Status semantics (the closed 7-state vocabulary)
Used for claims, matches, trace links and findings. Each has colour and icon and text and (in graphs) line style.
State
Label
Fg / Bg / Border
Icon
Trace line
confirmed
Confirmed
#1B6B3A / #E3F2E7 / #9CCBAA
check-circle
solid
candidate
Needs review
#7A4A00 / #FCEFD2 / #E5B75A
question-circle
dashed
inferred
Inferred
#34428A / #E8EBF8 / #AAB4E3
branch
dotted
rejected
Rejected
#565B5A / #ECEAE6 / #C9C4BA
x-circle (row muted, text readable)
hidden by default; toggle
conflict
Conflict
#9B1C1C / #FBE4E1 / #E39A92
split/alert-diamond
solid red with conflict marker
missing
Missing evidence
#7A2E2E / #FFFFFF / #C77D75 (dashed border)
dashed-circle
broken line with gap marker
failed
Processing failed
#9B1C1C / #FBE4E1 / #E39A92
alert-triangle
n/a
Severity (findings only, separate from state): Critical (solid #9B1C1C chip, white text), High (red outline), Medium (amber outline), Low (grey outline). Always text plus a 1-4 bar glyph. Do not stack a state badge and a severity badge when they repeat the same message.
Neutral/unknown: “Not checked” and “Insufficient data” use sunken grey with a dashed-circle icon. Never green.
Typography
System UI stack (Inter, system-ui, Segoe UI, Roboto, sans-serif); IDs, lot numbers, hashes and locators in ui-monospace, SFMono-Regular, Menlo, Consolas, monospace.
Token
Size / line-height / weight
Use
display
24 / 32 / 600
page title only
h2
18 / 26 / 600
section titles
h3
15 / 22 / 600
panel titles
body
14 / 21 / 400
default
table
13 / 18 / 400
table cells
caption
12 / 16 / 500
helper text, timestamps, column headers (not all-caps)
mono
12.5 / 18 / 450
IDs, locators
No text below 12px.
Spacing, shape, elevation, motion
• Spacing (4px base): 4, 8, 12, 16, 24, 32, 48. Page padding 24 desktop / 16 mobile. Panel padding 16. Table cell padding 8×12 (compact).
• Radii: 4 (chips, inputs), 6 (buttons, cards), 10 (modals, drawers).
• Elevation: surfaces use 1px borders, not shadows. Popovers 0 4px 16px rgba(31,42,40,.12); drawers/modals 0 8px 32px rgba(31,42,40,.18) with a 40% scrim.
• Motion: 120ms ease-out for hover/disclosure; none under prefers-reduced-motion.
Breakpoints
sm <640 (cards, single column) · md 640-1023 (scrollable tables, full-width drawers) · lg 1024-1439 (two-pane) · xl ≥1440 (three-pane source review).
Icons
One inline SVG set, 16px inline / 20px nav, 1.5px stroke, currentColor; aria-hidden when paired with text, otherwise role="img" with a label. Icons never carry meaning alone.
Table pattern
• Sticky header; identifier column sticky on horizontal scroll.
• Long IDs: mono, overflow-wrap:anywhere, copy button on hover and focus, full value in title.
• Sort/filter only where the API or loaded dataset supports it; if paging is server-side and sorting is client-side, label “Sorted within loaded rows”. [Not verified: server paging]
• Row actions are always visible (keyboard and touch must reach them), never hover-only.
• Selected row: primary-subtle background plus a 3px left primary bar.
Form pattern
Visible labels above inputs, helper text below, error text replaces helper text with an icon, linked via aria-describedby. “Required” written in words. Consequential buttons are never the default Enter action.
D. Screen-by-screen specification
D1. Workspace (home) [Existing data, new composition]
Purpose: answer “where was I, and what needs me?”
Layout (lg): left column 2/3, right column 1/3.
• Top banner (conditional, shown whenever the workspace contains synthetic demonstration evidence): synthetic-data banner (amber, persistent, not dismissible). Whole-workspace mode: “Synthetic demonstration data active. Not for operational food-safety decisions.” Mixed pilot workspace: “Pilot workspace contains N seeded synthetic demonstration document(s). Synthetic records are tagged and must not be used for operational decisions.”
• Needs attention (primary panel): ranked list of up to 6 rows across investigations: unresolved Critical/High findings, undecided matches, claims awaiting review, failed documents. Each row: icon, one-line statement, investigation reference, age, “Open”. Empty state: “Nothing is waiting for a decision.” (no success tick).
• Recent investigations / trace runs: compact table (reference, suspect lot, status, last activity, unresolved findings).
• Right column: workspace summary as a compact definition list (Documents, Claims, Matches, Trace runs, Unresolved findings from persisted findings only), compact System status card (D12), and the Import drop zone.
• Primary action: New investigation (top-right of the header row).
D2. Investigations list, create, Overview
List: table with Reference, Name, Suspect lot, Status, Last trace run, Needs decision (N), Updated. Filter by status; search by reference/lot. Empty state: “No investigations yet. An investigation holds the documents, claims, matches and trace runs for one suspect lot.” + Create investigation.
Create (dialog on desktop, full page on mobile): Name (required), Reference, Suspect lot ID (required, mono, helper “Raw material lot, batch, or finished-good lot”), lot kind (only if the trace API needs it), Description. Submit: “Create investigation”. After creation, land on Overview with the next-step card “Add source documents”.
Overview:
1. Investigation shell context (always visible).
2. Progress spine: the five steps from §A, each linking to its tab with counts: Sources (n, failed n), Claims (n awaiting review), Matches (n undecided), Trace (latest run date or “Not run”), Findings (n unresolved).
3. What to do next: one prominent card computed from existing counts by deterministic rules, in this priority: failed documents → undecided matches → claims awaiting review → run a trace → unresolved findings → export pack. Show the reason (“3 documents failed to process”).
4. Latest trace snapshot summary: counts by state, affected lots, known destinations, mass-balance status. Caption: “Snapshot taken <timestamp>. Later review decisions do not change it; run a new trace to reflect them.” [Existing: snapshots persisted]
5. Recent activity: last 8 audit events for this investigation, if filterable (see J).
D3. Evidence list, detail and source viewer
Evidence list: columns Document (filename, wraps), Type, Added, Processing status, Claims extracted (count), Flags. Status mapping: queued/processing (neutral, text + static icon), processed (neutral “Processed”, not green), failed (state failed), duplicate (“Duplicate of …” linking to the original), superseded (muted chip), image retained (“Stored, not read - OCR is not available”). Filters: status, type, “Failed only”. A failed row shows the first line of the processing error and View details.
Evidence detail (right drawer lg+, page on md/sm):
• Header: filename, status, added at.
• Summary: claims extracted by review state, parser name.
• Technical details (collapsed): SHA-256 (mono, copy), parser version, derived text path, original/superseded relationship, processing error.
• Open in source viewer (primary).
• If failed: panel “What happened / What you can do” (copy in §H).
Source viewer (provenance centrepiece) [UI + Backend?]: split pane. Left: claims for this document (filter by state). Right: the source at the locator.
• Spreadsheet (sheet/row): read-only table slice around the cited row (±5 rows), cited row highlighted, sheet name shown.
• Text (line): mono lines with line numbers, cited line highlighted, ±10 lines of context.
• PDF (page): show the page’s extracted text with the cited page marked. Do not simulate a PDF render and never draw bounding boxes without real coordinates. Offer “Download original” only if retrievable. [Not verified]
• Images: metadata plus “This file is stored but not read. OCR is not implemented, so no claims were extracted.”
• If no content endpoint exists: degrade to a locator card (document, page/sheet/row/line, extracted value, “Content preview is not available in this build”). List the missing endpoint as a proposed backend change in the final report. This is an honest fallback, not a fake preview.
D4. Import flow and processing result [Existing upload, redesigned feedback]
• Drop zone states: idle, drag-over, uploading (per-file progress only if measurable, otherwise indeterminate with text), done, rejected.
• Helper text lists only the formats and size limit the backend accepts, read from the backend or a single shared constant, never duplicated copy that can drift.
• Per-file result list after upload, persistent until dismissed:
    ◦ Processed (“12 claims found”) → link to claims.
    ◦ Duplicate (“Same content as <name>, added <date>. Not added again.”) with link.
    ◦ Rejected (“Format not accepted: .xyz” / “Exceeds N MB”) with the accepted list.
    ◦ Failed (parser error) with Retry only if a retry endpoint exists; otherwise “Re-upload after fixing the file”.
• Summary line, announced via aria-live="polite": “5 files: 3 processed, 1 duplicate, 1 failed.”
• Nothing is deleted on failure; the document stays listed with its error.
D5. Claims review queue and claim detail/history
Queue (lg): left filter rail (state, document, claim type, validation outcome, origin: Synthetic (seeded) / Operator supplied / Manual entry), centre table, right detail pane at xl.
Columns: Claim (e.g. “Lot LOT-889 received 400 kg”), Value (quantity component), Source (locator chip invoice_44.pdf · p3), State, Validation, Updated. Default sort: needs-review first, then by document.
• Claims without a locator show the dashed “No source position” chip and are visually subordinate; confirming one requires a typed reason. [UI rule]
• Validation failures show an inline amber note (“Quantity unit missing”).
• Bulk actions only if the existing API supports bulk review. No “confirm all”.
Claim detail pane:
1. Statement and structured fields (editable only after clicking Edit).
2. Source block: locator chip, excerpt if available, Open in source viewer.
3. Review actions (E4): Confirm · Edit · Reject.
4. History timeline [Existing: fact history API]: newest first; action, actor, timestamp, reason, and for edits previous → new values. Caption: “History is append-only and cannot be changed.”
5. Extraction details (collapsed): method, provider, model, prompt version, data provenance (synthetic seeded / fixture parser / operator supplied / manual entry); “Synthetic” tag for seeded fixture claims.
D6. Matches (entity resolution) [Existing]
List: each proposal shows the two identifiers side by side. Show a similarity figure only if the API supplies one; otherwise “Heuristic proposal” plus reason text if present. States: Needs review / Confirmed / Rejected / Deferred.
Review pane (comparison layout): columns “Identifier A” and “Identifier B”, each with the identifier (mono), documents it appears in (locator chips), and surrounding quantities/dates/suppliers. A centre strip lists matching and differing attributes in text (“Same supplier”, “Quantity differs: 400 kg vs 380 kg”). Consequence line: “Confirming records an alias so these identifiers are treated as the same lot in future traces. It does not merge or edit source records.” Actions: Confirm as same lot · Reject · Defer; reason required for Confirm and Reject.
Aliases: secondary tab listing confirmed aliases (identifier ↔ identifier, who, when, reason). Revert alias appears only if the backend supports it [Backend?]; otherwise it is omitted, not shown disabled.
D7. Trace setup and results
Setup (drawer or card atop Trace tab): Starting lot (prefilled from suspect lot, editable), lot kind (if the API requires it), Direction (Forward · Backward · Both; segmented control with one-line explanations), Run trace. Beneath the button, if matches are undecided: “3 matches are undecided. They will appear as candidates in this trace.”
Run list: persisted runs (date, start lot, direction, counts by state, findings). Selecting one opens its snapshot, read-only, with the banner “Snapshot from <timestamp>.”
Results (lg):
• Run header: metadata (investigation, start lot, direction, time) + export actions.
• State summary bar: Confirmed, Needs review, Inferred, Conflict, Missing; each a filter toggle with a text count.
• Key answers (3 across on lg, stacked on sm): Affected lots (n, listed), Known destinations (n, listed; amber “Dispatch evidence missing for N lots” when applicable), Mass balance (D9 summary).
• View switcher: Paths (default) · Graph · Table.
D8. Trace views and link inspector
• Paths view (default, most accessible): indented directional list grouped by direction. Hop row: from-lot (mono), link chip (state icon + label), to-lot, quantity, 1-3 locator chips. Collapsible with arrow keys and aria-expanded.
• Graph view: layered left-to-right (backward left, suspect lot centred and highlighted, forward right). Edge styles per §C. Required: always-visible legend (doubling as filter), zoom/pan with keyboard equivalents, aria-labels on nodes/edges, and a one-click switch to Paths/Table. Above a readable node cap, show “Showing N of M. Switch to Paths for the full list.” Use an existing graph dependency if present; otherwise layered SVG by depth with no new heavy dependency unless justified.
• Table view: Link id, From, To, Relationship, State, Quantity, Evidence count, Findings count; client-side sortable.
• Link inspector (right drawer) from click/Enter in any view:
    1. Plain sentence (“Lot A was used in Batch B.”).
    2. State badge and why this state (“Candidate: depends on an undecided match between A-7 and A7”).
    3. Supporting evidence: each claim with locator chip, document, claim state, Open in source viewer.
    4. Dependencies: matches/claims this link relies on, each with state and a jump link (“Decide this match”).
    5. Related findings (severity + title).
    6. For gap links: “No evidence found for this hop” plus the record type that could resolve it (e.g., dispatch record), phrased as guidance.
D9. Mass balance
One reusable component, shown in Trace results and Findings.
• States: Insufficient data (default whenever any quantity, unit or source is missing or mismatched) · Balanced within recorded evidence (only if every input has a quantity, unit and source and no open conflict) · Imbalance (shows delta).
• Never the word “pass”. “Balanced within recorded evidence” always carries the line “This reflects only the records provided.”
• Body: compact table Received · Consumed · Dispatched · Unaccounted with units and locator counts. Missing cells show a dashed “Missing” chip, never zero.
• Unit mismatches are shown, not silently converted; if the backend converts, display “Converted from X”.
D10. Findings (gaps and conflicts) [Existing persisted findings]
• List grouped by severity then type, default filter “Unresolved”. Row: severity tag, title (“Received 1,200 kg, consumed 1,050 kg”), type, affected lots, evidence count, first detected.
• Count source: badges and metrics count persisted unresolved findings only (fixes the earlier gap-edge miscount).
• Detail drawer: what was found; evidence on each side (side-by-side for conflicts, locator chips); one neutral sentence on why it matters; resolution links to the relevant tool (open match, open claim). Resolve/dismiss controls only if the API supports status changes [Backend?]; otherwise copy: “This finding clears when the underlying evidence changes and a new trace is run.”
D11. Audit history
Chronological filterable table (time, actor, action, object, reason, summary). Filters: action type (review, match decision, trace run, export), object, date range, actor. Row expands to previous/new values. Footer: “Actor is shown as recorded. Full user identity is not yet implemented.” Empty state: “No actions recorded for this investigation yet.”
D12. System status (model/extraction) and settings
Model status ladder (rendered only from the health and smoke endpoints; each step its own row with text status):
1. Configured - provider and model name from settings.
2. Reachable - health endpoint responded (checked-at time, “Check again”).
3. Model installed - present in the provider’s model list (llama3.2:3b found / not found).
4. Live inference verified - last smoke result with timestamp, or “Not run”. Run smoke test (disabled while running; shows timeout and error text on failure).
Summary pill, derived strictly: “Live extraction verified” only when step 4 succeeded; “Model not installed” when 2 passes and 3 fails; “Provider unreachable” when 2 fails; otherwise “Not verified”. Treat state as unknown until the endpoint says otherwise.
Capabilities list: structured text/spreadsheet extraction (state from parsers), Image OCR - Not implemented, Visual similarity (DINOv2/FAISS) - Not available, Segmentation (SAM 2) - Not available. For “model not installed”, show the install command as copyable text only; the UI never executes it.
Workspace & pilot settings: read-only workspace-mode indicator (Synthetic demonstration / Pilot) with explanation. API key entry: password-type field, “Save for this session”, never echoed back (show “Key set” + Clear), never in localStorage, URLs, logs, errors or exports. Keep existing sessionStorage only if the key must survive reload, and disclose it (“Kept in this browser tab until you close it”). 401/403 handling: banner “The API key was missing or not accepted” + key entry.
D13. Export [Existing: HTML / JSON / CSV]
• Scope: investigation, latest or a selected trace snapshot. Format: Printable report (HTML) · JSON · CSV. Include audit trail (default on).
• Pre-export check panel: unresolved findings, undecided matches, claims awaiting review, mass-balance state, synthetic data present (yes/no). These never block export; they show an amber “This pack has open items” notice.
• The backend disclaimer is shown verbatim before download and is always in the file.
• Export history if export audit events exist: time, format, run, actor.
• Button copy: “Download report (HTML)”, “Download data (JSON)”, “Download tables (CSV)”. CSV helper: “Cells beginning with =, +, - or @ are neutralised for spreadsheet safety.”
E. Component specifications
• E1. StatusBadge: props state, size. Icon + full text, never abbreviated. role="status" only when the value updates dynamically. title/tooltip explains the state in one sentence.
• E2. SourceLocatorChip: format <doc short name> · p3, Sheet1!R12, or L41. Mono; truncate the middle of long names, full value in tooltip. Click/Enter opens the source viewer at that locator. No locator → dashed “No source position” chip. Seeded synthetic claims add a small “Synthetic” tag.
• E3. QuantityDisplay: tabular numerals, thousands separators, unit in text-muted, no silent rounding. Missing unit: “400 (unit missing)” in amber. Missing quantity: dashed “Missing” chip.
• E4. ReviewActionGroup: Confirm, Edit, Reject (Defer only where the API supports it). Choosing an action opens an inline panel (not a modal) with a summary of the change, a Reason textarea (required for Reject, Edit, and Confirm-without-locator; optional otherwise) and Submit decision. Wait for the server before updating the UI; on success the history timeline refreshes and aria-live announces “Claim confirmed.” On failure the error stays inline with Retry and the typed reason is preserved. History is append-only, so there is no Undo, and the copy says so.
• E5. MatchComparison: two equal columns (stacked on sm), attributes aligned row by row, differing rows tinted amber with the text “Differs”, matching rows labelled “Same”. Actions sit below (sticky bottom bar on mobile).
• E6. FindingSeverityTag: per §C; text always present.
• E7. TraceLegend: horizontal list (wraps on sm) of line sample + icon + label for all seven states; doubles as filter toggles with aria-pressed.
• E8. EmptyState: neutral icon, one-line title, one-line explanation, one primary action only when an action exists. Never congratulatory.
• E9. WarningBanner: info (grey-teal), caution (amber), critical (red). Icon + bold lead sentence + optional action. Dismissible only when the condition is not persistent; the synthetic-data banner is not dismissible.
• E10. ImportProgress: per-file rows with name, status text and action, plus a summary line. Results persist while the user navigates within the session.
• E11. ModelStatusPanel: four-step ladder (D12); compact one-line variant with a chevron on Workspace.
• E12. HistoryTimeline: vertical, local time with ISO in the tooltip, reasons quoted, edits as “Was: … / Now: …”, events keyboard-focusable.
• E13. FilterBar: active-filter chips, “Clear filters”, result count in an aria-live region, state in the URL.
• E14. ExportActionBar: three download buttons plus a pre-export check link; “Preparing…” while generating; error banner on failure with the previous state intact.
• E15. Drawer/Dialog: focus trap, Escape closes, focus returns to the trigger, aria-modal, title + close button (≥40px target).
F. Responsive behaviour
• xl ≥1440: three-pane source review (claims | source | detail); trace results with a persistent inspector.
• lg 1024-1439: two panes; inspector as a 480px right drawer.
• md 640-1023: one column; tables inside overflow-x:auto containers with a sticky first column; drawers become full-width sheets; investigation tabs scroll horizontally with a fade edge and the active tab scrolled into view.
• sm <640: tables become cards with the same field priority (identifier, state, locator chip, quantity, then the rest behind “Details”); header nav collapses to an icon row (max 5 items); investigation tabs become a “Section” switcher with counts; action groups become a sticky bottom bar; Graph view is replaced by Paths with the note “Graph view is available on larger screens” (Table stays available).
• Always visible: investigation name and suspect lot, workspace-mode chip, primary decision buttons, state labels, unresolved-findings count.
• No page-level horizontal scroll at 390×844 or 1440×960.
G. Accessibility, loading, error, empty and partial states
• Landmarks (header, nav, main, aside), one h1 per route, ordered headings, skip link.
• Visible focus everywhere; tab order follows visual order; drawers trap and restore focus.
• Contrast ≥4.5:1 for text and ≥3:1 for UI boundaries and icons, verified for every badge pair.
• Real <table> semantics with scope and an accessible name; card mode keeps visible “Label: value” pairs.
• aria-live="polite" for import results, review outcomes and status checks; assertive only for blocking errors.
• Targets ≥40px for primary controls; ≥32px with spacing for dense table actions.
• prefers-reduced-motion: no transitions; replace spinner rotation with a static indicator plus text.
• Loading: skeleton rows matching the final layout; after 8s show “Still loading…” with Retry.
• Error: what failed, whether data is safe (“Nothing was changed”), Retry, and HTTP details under a collapsed “Technical details”.
• Empty: per E8.
• Partial data: show what exists with a banner naming what is unavailable. A failing sub-panel never blanks the page.
H. Copywriting
Context
Copy
Synthetic-data banner
“Synthetic demonstration data active. Not for operational food-safety decisions.”
Pilot banner
“Pilot workspace. Decisions are recorded in the audit trail.”
Needs review tooltip
“Suggested from the records. No one has confirmed this yet.”
Inferred tooltip
“Derived from other records. It is not stated in any single source.”
Missing
“No record found for this step. This does not prove the step did not happen.”
Insufficient mass balance
“Not enough data to check the balance: 2 quantities have no unit and 1 has no source.”
Model not installed
“The extraction service is reachable, but the configured model (llama3.2:3b) is not installed. Install it, then check again.”
Inference unverified
“Live extraction has not been verified. Synthetic and parsed records are unaffected.”
Image not read
“Stored but not read. Text recognition for images and scans is not available yet.”
Duplicate upload
“This file has the same content as ‘<name>’, added <date>. It was not added again.”
Rejected upload
“This format is not accepted. Accepted: <list from backend>. Maximum size: <limit>.”
Failed document
“What happened: <parser error>. What you can do: check the file opens normally and re-upload it. The original is kept.”
Confirm match
Button “Confirm as same lot”; helper “Records an alias. Source records are not changed.”
Reject claim
Button “Reject claim”; helper “It stays in the history and is excluded from traces.”
Export notice
“This pack contains N open items. They are listed in the report.”
API key error
“The API key was not accepted. Enter a valid key to continue.”
Footer
“Klarrow assists investigation. People remain responsible for food-safety decisions.”
Avoid: “AI-powered”, “verified by AI”, “compliant”, “all clear”, exclamation marks, celebratory language.
I. Acceptance criteria
Manual
1. From Workspace I can create an investigation and land on its Overview with a computed next step.
2. Uploading a duplicate, an unsupported type and a valid file each yields the correct per-file result using the real limits.
3. Every claim row shows a locator chip or “No source position”.
4. Confirm/Reject/Edit require reasons where specified and appear in history after the server responds.
5. Confirming a match creates an alias and merges nothing.
6. A trace shows all seven states distinguishably in greyscale (text, icon, line style).
7. Opening any hop shows its evidence, dependencies and findings.
8. Mass balance never shows a reassuring state when data is missing.
9. The unresolved-findings count equals persisted unresolved findings.
10. The model ladder reflects real endpoint responses: Ollama stopped, running with no model, running with the model.
11. Export includes the workspace-mode provenance notice; the synthetic marker appears when synthetic data is present.
12. Reopening an investigation shows identical historical snapshots.
13. No horizontal page scroll at 390×844 and 1440×960.
14. Keyboard-only run-through of create → upload → review → trace → export succeeds.
Automated (where feasible)
• Playwright: create-investigation flow; upload result rows (duplicate/rejected); review panel requires a reason; match confirm shows the alias; legend toggles filter the trace; model-status states via route mocks (labelled as mocked); scrollWidth <= innerWidth at both viewports; axe-core on main routes with zero serious/critical violations (only if added as a dev dependency).
• Component tests: StatusBadge (every state has icon + text), QuantityDisplay, locator formatting.
• .NET tests for any new endpoint.
J. Mismatches between ideal UX and existing APIs/model
#
Ideal UX
Brief evidence
Resolution
1
Investigation-scoped Evidence/Claims
Only trace runs are stated as linkable to an investigation
[Not verified] Inspect the schema. If absent, show workspace-wide records inside the investigation with a visible “Workspace-wide records” scope label. [Backend?] Minimal fix: additive nullable InvestigationId on documents (no destructive migration) plus an optional filter parameter, with tests. Defer unless required.
2
Source viewer with page/sheet/row content
Locators exist; no content endpoint stated
[Not verified] Fall back to the locator card. [Backend?] Read-only endpoint returning derived text or a row slice for a locator, with path-traversal tests.
3
PDF rendering / bounding boxes
No coordinates stated
Extracted page text only; no boxes.
4
Per-file upload progress
Unknown
Indeterminate progress if not measurable.
5
Server-side paging/sorting/filtering
Not stated
Client-side on loaded data; label truncation honestly.
6
Bulk review
Not stated
Omit.
7
Defer on claims
Defer stated for matches only
Defer only on matches.
8
Revert alias / resolve finding
Not stated
Omit controls; explain behaviour in copy.
9
Audit filtered by investigation
Not stated
Client-side filter if events carry an investigation/object reference; otherwise label as workspace audit.
10
Actor identity
No identity system
Show the recorded reviewer string plus the limitation note.
11
Switching workspace mode in the UI
Mode is configuration
Read-only indicator unless an endpoint exists.
12
OCR, DINOv2/FAISS, SAM 2
Not implemented / deferred
Always listed as unavailable; no controls.
Impact summary: the design works end to end on existing APIs with two honest degradations (workspace-wide evidence scope; locator card instead of live source preview). Items 1 and 2 are the highest-value backend additions and should be proposed and tested, never silently assumed.
