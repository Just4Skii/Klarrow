# UI redesign continuation plan (Stage 1)

Canonical spec: `docs/recallgraph_design_spec.md` (read; not duplicated or extracted from the zip).

Real endpoints/DTOs found: `/api/workspace/home|facts|lots|audits`; `/api/documents[/{id}|/upload]`; `/api/facts[/{id}|/{id}/history|confirm|reject]`; `/api/investigations[/{id}]`; `/api/trace-runs[/{id}|/{id}/export]`; `/api/match-proposals`, `/api/entity-aliases`; `/api/gaps`; `/api/extraction/status|health|smoke|run`; `/api/capabilities`.

Scope reality (proved in code): `SourceDocument` has `WorkspaceId` but no `InvestigationId`; `ExtractedFact` links to document; `TraceLink` has `WorkspaceId` only. Evidence and links are therefore workspace-wide, and the UI must not imply isolation.

Stage 1 objectives in order:
1. Add nullable `InvestigationId` to `SourceDocument` and `TraceLink`; additive idempotent schema step (version 5) using ALTER TABLE, no data deletion.
2. Upload accepts optional investigation id; document list/detail can filter by investigation; facts filter via their document; trace uses only links assigned to the selected investigation when one is supplied (workspace-wide demo links stay assigned to the demo investigation during seed).
3. Seed assigns the demo investigation to seeded documents and trace links.
4. UI: replace flat nav with global nav (Workspace, Investigations, System) plus an investigation row (Overview, Evidence, Claims, Matches, Trace, Findings, Audit, Export) only when an investigation is selected; compact, no wrap at 1440x960.
5. Scope honesty: Evidence/Claims show "Workspace-wide records" or the selected investigation name; no implied isolation. Label remains until scoping filters are active.
6. Tests: cross-investigation leakage (documents/facts), upload with/without investigation, trace with investigation filter; keep all existing tests green.
7. Deferred to later stages: pagination, clickable source viewer and `/documents/{id}/content`, findings drawer, trace Paths/Graph/inspector, mass-balance component, export workspace, mobile card mode, axe.

## Bounded Pass A (this session)

Correctness first: exports now embed the workspace audit trail in CSV (AUDIT rows), HTML (Audit trail section) and JSON (`auditTrail` property); JSON export uses web camelCase conventions for consistency. Added `Export_includes_audit_trail_and_disclaimer` test.
Parser copy: user-facing "Llama" replaced with "the extraction model" (provider status uses the configured model name from settings). Type names, env vars, provider identifiers and model IDs unchanged.
Fonts: `@fontsource-variable/inter`, `@fontsource-variable/bricolage-grotesque`, `@fontsource/ibm-plex-mono` installed via npm (no CDN); stacks point at the Variable families; tabular-nums on tables.
Page budget: header fixed 48px, shell 81px, slim banner margins, main padding 12px; measured h1 top = 149px at 1440x960.
Tooltips: Evidence status, audit events, mass-balance and model summary now carry plain-language titles.
DataTable: mobile-cards + data-labels applied to trace, investigations and audit tables; matches/findings converted to mobile-cards tables with Details disclosures and always-visible actions; shared CopyButton (hover + focus visible) on suspect lot, run ID and audit-adjacent IDs.
Deep links: hash routes `#/<tab>?inv=&run=&claimStatus=&claimQuery=&claimPage=&evidenceQuery=&doc=&match=&finding=&claim=`; validated against loaded data, unknown ids ignored; reload restores tab, investigation, run, filters, drawers and highlights.
Drawers: shared useFocusTrap (Tab cycling, initial focus); all four drawers use it plus existing Escape/close/focus-return.
Tests: 3 new Playwright tests (deep-link reload, unknown ids, focus trap + Shift+Tab); full suite re-run.

## Bounded Pass B (this session)

New Review desk route (investigation nav, after Claims): three panes (queue | claim with ProvenanceRibbon and real source context | decision panel with collapsed history). Shortcuts J/K move the queue, C/E/R open panels, H toggles history, ? opens the shortcut help dialog; no shortcut submits - a decision is only recorded by the visible Submit decision button or by pressing Enter while focus is inside the open panel. Reason required for Reject, Edit and Confirm-without-locator (validated against the real seeded fact `23_photo_supplier_label.bmp`, whose locator is "not recorded"). Decisions update the UI only from the server response; inline errors preserve typed input; results announced via an aria-live region; session counter shows a count ("Decided this session: N"), never a percentage. 200ms slide on claim change (neutralized by the existing prefers-reduced-motion rule). Source context uses the existing `/api/documents/{id}/content` endpoint; when the locator cannot be previewed the pane shows an honest locator card with the server's reason - no fake previews.

Mobile: queue collapses into a top sheet (`.desk-sheet-toggle`), decision actions live in a sticky bottom bar that mirrors the open panel's Submit/Cancel.

Matches rebuilt as MatchComparison cards with A/B columns, per-attribute comparison statements (matching vs differing), the exact consequence line, and reason-gated Confirm as same lot / Reject / Defer; tabs split Proposals from Aliases (alias list carries a no-revert-endpoint footnote instead of an undo control).

Backend (required by the mandate): `FactPatchDto` gained `Reason`; `FactService.PatchAsync` now records review status `Edited`, reviewer, reviewed-at and the reason in the append-only history and audit payload; new test `Edit_marks_fact_edited_and_records_reason_in_history`.

Tests: .NET 23/23; Playwright 24/24 (`workers: 1` because the demo DB is shared state; the alias test resets the seed via `POST /api/admin/seed` and proves claims stay Unreviewed). New Playwright coverage: desk shortcuts never submit (mutating-request tracking), reason-required reject/edit with history verification, locator-less confirm gate on real seed data, mobile sheet + sticky bar, alias-only match confirmation, and screenshot captures (desktop + mobile) for Review desk and Matches viewed during the pass.
