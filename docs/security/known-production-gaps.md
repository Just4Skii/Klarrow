# Klarrow - Known Production-Security Gaps (Prototype Disclosure)

Status: prototype / pilot-demo disclosure document.
Scope: the Klarrow build submitted for the Meta AI Academy South Africa Pitchathon (October 2026).
Purpose: state, without exaggeration, what the current access-control and data-boundary implementation does and does not do, so pilot hosts and judges can judge it honestly.

Klarrow is a single-tenant, self-hosted prototype. It is designed to demonstrate evidence-grounded recall investigation on a local machine. It is **not** production-hardened, and it must not be presented as compliant with any regulatory or enterprise security standard.

## What is implemented

| Control | Implementation | Honest description |
| --- | --- | --- |
| API access control | `ApiKeyMiddleware` compares a shared key (`RecallGraph__ApiKey`, header `X-Api-Key`) using a fixed-time comparison | A single shared token per deployment. Not per-user accounts. |
| Public endpoints | `/api/health`, `/api/extraction/status`, `/api/capabilities` are unauthenticated | Needed for liveness and capability disclosure; they expose configuration state only, not evidence. |
| Access-mode visibility | `/api/health` returns `accessControl.mode` (`token` or `none`) and a notice; the UI shows a banner for both modes | Operators can see whether protection is on. The banner is informational, not a security control. |
| Data locality | Files and SQLite database live at operator-configured local paths (`Storage__*` env vars) | Local-first by default. Evidence text leaves the machine only if the AI endpoint is pointed at a remote host. |
| Audit trail | Append-only audit events for ingestion, review decisions, trace runs, deletions, and exports | An operator-visible activity log. Not tamper-evident, not externally notarized. |
| Deletion | `DELETE /api/investigations/{id}` removes scoped documents, claims, review events, links, runs, findings, and stored files; response lists declared limitations; deletion is audited | Closest-safe-delete in a prototype. Files shared outside the investigation scope (e.g., identical SHA-256 uploads in other investigations) are deliberately retained and reported in `limitations`. |
| Spreadsheet safety | Exports neutralize formula-injection prefixes (`=`, `+`, `-`, `@`) as text | CSV/Excel outputs are safe to open in standard tooling. |
| Transport | HTTP on localhost by default | No TLS termination is included. Any networked deployment must add a TLS reverse proxy. |

## What is NOT implemented (do not claim otherwise)

- **No user accounts, SSO, OIDC, SAML, or multi-factor authentication.** One shared API key at most.
- **No role-based access control (RBAC).** Any caller with the key (or, when no key is configured, any caller with network access) has full read/write access, including deletion.
- **No transport encryption built in.** HTTP only; TLS must be provided externally.
- **No multi-tenant isolation.** A single workspace per deployment; investigation scoping is an application-level view, not a security boundary.
- **No tamper-evident audit.** Audit rows live in the same SQLite database as the data and could be altered by anyone with filesystem access.
- **No OCR.** Scanned/binary images are stored as evidence for manual inspection only; no text is extracted from them, and no accuracy claims exist for image-based extraction.
- **No rate limiting, lockout, or anomaly detection** on the API.
- **No secrets management.** The API key is provided via environment/configuration; it is not stored in a vault.
- **No automated data-retention policy.** Deletion is operator-initiated only.
- **No independent security audit or penetration test** has been performed on this build.

## Deployment guidance for a pilot host

1. Bind the API to localhost or run it behind a TLS reverse proxy with network access restricted to the pilot team.
2. Configure `RecallGraph__ApiKey` and distribute it only to pilot operators; rotate it between pilot sessions.
3. Keep the AI endpoint local (`Ollama` on `localhost:11434`) so document text does not leave the machine; if a remote endpoint is used, treat that as a data-sharing decision and confirm it with the data owner first.
4. Treat the workspace as demo/pilot evidence only: never make operational food-safety decisions from unconfirmed claims, and never feed real confidential evidence without owner consent.

## How this document is used in the submission

- The pitch deck and application answers reference this file as the authoritative, non-marketing statement of the security boundary.
- The in-app "Data & Security Boundary" panel (System & AI tab) and the access-mode banners summarize these points for operators.
- All wording here is deliberately conservative; anything not listed under "What is implemented" must be described as planned or absent.
