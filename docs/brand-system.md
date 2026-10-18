# Klarrow Brand System

Canonical reference for the Klarrow visual identity. The live implementation is `src/frontend/src/App.css` (single styling surface). If this document and the CSS disagree, the CSS wins and this document must be updated.

## Brand Basics

- Product name: Klarrow
- Tagline: Evidence Connected
- Descriptor: evidence reconciliation workbench
- Positioning: evidence-first reconciliation of disconnected supplier, production and dispatch records. Uncertainty stays visible; the product never claims regulatory clearance or autonomous food-safety determination.

## Logo And Favicon

- The K mark is the supplied standalone image. It must remain on a transparent background, unmodified: never redrawn, recolored, stretched or placed on a solid dark tile.
- Source file: `Logos/Metallic Ribbon K favicon.png` (workspace asset, not shipped to the web root).
- Web assets (generated from the source with alpha preserved, cropped to the mark, 2 percent transparent margin):
  - `src/frontend/public/klarrow-k-64.png` (header mark and browser favicon)
  - `src/frontend/public/klarrow-k-180.png` (apple-touch icon)
  - `src/frontend/public/klarrow-k-512.png` (high-resolution use)
- Header usage: `App.tsx` renders `<span className="brand-mark"><img src="/klarrow-k-64.png" alt="" /></span>`; `.brand-mark` is a transparent flex container (36 by 32 px) with `object-fit: contain`.
- The header surface is light (white) so the dark navy mark reads without a background tile.
- Wordmark: "Klarrow" in the display font, ink color. Subtitle: "Evidence Connected" in muted color.

## Core Color Tokens

Defined in `App.css` `:root`. Hex values are canonical.

| Token | Value | Use |
| --- | --- | --- |
| `--canvas` | `#f4f6fa` | Page background (Soft Slate) |
| `--surface` | `#ffffff` | Panels, cards, header |
| `--surface-sunken` | `#edf0f6` | Inset wells, chips on white |
| `--surface-alt` | `#f7f9fc` | Alternating rows, subtle fills |
| `--surface-hover` | `#eef1f7` | Hover fills |
| `--border` | `#d6dbe5` | Standard borders and dividers |
| `--border-strong` | `#b9c1d0` | Control borders, stronger separation |
| `--text` / `--ink` / `--ink-strong` | `#2d2f38` | Primary text (Ink) |
| `--text-muted` / `--muted` | `#626b7b` | Secondary text (Muted) |
| `--primary` | `#39466c` | Brand Indigo: primary actions, active states, links |
| `--primary-hover` / `--primary-strong` | `#2d3858` | Indigo hover and emphasis |
| `--primary-subtle` / `--primary-soft` | `#e7ebf4` | Indigo tint fills (active nav, hover) |
| `--on-primary` | `#ffffff` | Text on indigo |
| `--silver` | `#a0a6b4` | Mineral Silver accent (brand strip, neutral accents) |
| `--cloud` | `#e5e8f0` | Cloud: table headers, quiet chips |
| `--focus-ring` | `#39466c` | Keyboard focus outline |
| `--overlay` | `rgba(45, 47, 56, 0.45)` | Modal and drawer scrims |
| `--disabled-fg` | `#9ba2b2` | Disabled text |
| `--disabled-bg` | `#eef1f7` | Disabled fills |
| `--danger` | `#9c3d3d` | Destructive action fill (muted red) |
| `--danger-hover` | `#833232` | Destructive hover |
| `--on-danger` | `#ffffff` | Text on destructive fill |

Radii: `--radius-sm: 6px` (controls, chips), `--radius: 8px`, `--radius-lg: 12px` (panels). Badges remain pill-shaped; buttons and interactive chips use `--radius-sm`, not full pills.

Shadows: `--shadow-sm` and `--shadow` are neutral ink-tinted shadows (`rgba(45, 47, 56, ...)`), never teal or indigo tinted.

Brand strip: the 3 px `.pattern-strip` under the header is `linear-gradient(90deg, var(--primary) 0%, var(--silver) 100%)`.

## Semantic Status Tokens

Status colors are functional, not brand decoration, and must never be replaced by indigo. Color is always paired with a text label or icon so state is not conveyed by color alone.

| State | Foreground | Background | Border |
| --- | --- | --- | --- |
| Confirmed | `#14532d` | `#dcfce7` | `#86efac` |
| Candidate / pending review | `#78350f` | `#fef3c7` | `#fcd34d` |
| Inferred | `#1e3a8a` | `#dbeafe` | `#93c5fd` |
| Rejected | `#565e6e` | `#eef1f7` | `#d6dbe5` |
| Conflict / failed | `#991b1b` | `#fee2e2` | `#fca5a5` |
| Missing | `#7f1d1d` | `#fff5f5` | `#f87171` |
| Unknown | `#565e6e` | `#eef1f7` | `#d6dbe5` |

Graph edges: confirmed `#15803d`, candidate `#b45309`, inferred `#2563eb`, missing `#b91c1c`. Graph node stroke uses `--border-strong`; node label uses `--ink-strong`.

Amber warning treatments (candidate chips, export watermarks, lot-diff highlight `#fde68a`) stay amber on purpose: they signal review-needed state.

## Typography

- Display/sans: Inter Variable stack (declared in `--font-display` / `--font-sans`).
- Mono: IBM Plex Mono stack (declared in `--font-mono`), used for identifiers, hashes, lot codes.
- No font changes were introduced by the Klarrow migration.

## Controls

- `.button` (secondary): white surface, `--border-strong` border, indigo text (`--primary-strong`), min-height 38 px. Hover: `--primary-subtle` fill with indigo border.
- `.button-primary`: indigo fill (`--primary`), white text. Hover: `--primary-hover`.
- `.button-small`: min-height 30 px.
- `.button-danger`: solid muted red (`--danger`), white text. Hover: `--danger-hover`. Destructive actions are solid, not tinted outlines.
- `.text-button`: ghost button; no underline at rest, underline on hover.
- Disabled state (all variants): `--disabled-bg` fill, `--border` border, `--disabled-fg` text, `cursor: not-allowed`.
- Navigation items, decision links, filter chips and view tabs use `--radius-sm`. Badges and count pills stay rounded.

## Accessibility

- Focus: `:where(button, a, input, select, summary, [tabindex]):focus-visible` gets a 2 px `--focus-ring` outline with 2 px offset. There is no longer a white focus override in the header because the header is a light surface.
- Reduced motion: global `prefers-reduced-motion` block shortens all animation and transition durations.
- Header text pairs (ink on white, muted on white) and indigo on white all meet WCAG AA for their sizes; status badge foreground/background pairs are unchanged from the audited original set.
- Skip-link is retained.

## Surfaces And Layout

- Header: white surface, `--border` bottom border, ink text, subtle neutral shadow. Active nav item: `--primary-subtle` fill, `--primary-strong` text, indigo-tinted border.
- Mode chips in the header use the global amber/pilot styles; key chips use `--surface-sunken`.
- No dark mode is defined; the product assumes the light theme only.

## Exports And Reports

- HTML export (`ExportFormatter.BuildHtml`): ink body text `#2d2f38`, Cloud table headers `#e5e8f0`, `#d6dbe5` borders, report title "Klarrow Trace & Evidence Reconciliation Report", neutral notice text `#626b7b`.
- The amber provenance watermark is retained and must stay amber: it communicates synthetic/pilot data or regulatory limits.
- CSV export: unchanged structure; the provenance comment line now begins `# Klarrow export ...`. Quantities remain invariant-culture decimals (period separator) with a UTF-8 BOM for spreadsheet tools.
- Exported measurements, findings, caveats and audit content are not altered by branding.

## Files

- `src/frontend/src/App.css`: all tokens and component styles.
- `src/frontend/index.html`: title "Klarrow | Evidence Reconciliation Workbench", theme-color `#39466C`, PNG favicon links.
- `src/frontend/src/App.tsx`: wordmark, tagline, header mark, footer copy.
- `src/frontend/public/klarrow-k-*.png`: generated mark assets.
- `src/RecallGraph.Infrastructure/ExportFormatter.cs`: HTML/CSV export styling and copy.

Internal identifiers (namespaces, database tables, API routes, config keys such as `RecallGraph:ApiKey`, sessionStorage keys such as `recallgraph.apiKey`, and the `service` field of `/api/health`) intentionally keep their original names. They are not user-facing branding.

## Migration Notes

- The Klarrow migration replaced the previous teal and dark-header system (old primary `#0d6864`, dark header `#0f172a`, warm canvas `#f4f6f8`) with the indigo, silver and slate palette above.
- Styling remains token-driven: no component may hard-code the old hexes. `rg "0d6864|094e4b|14b8a6|f4f6f8|0f172a" src/frontend/src` must return nothing outside intentional semantic tokens.
- En dashes and em dashes are removed from product copy and authored documents. Use hyphens or restructure the sentence.
