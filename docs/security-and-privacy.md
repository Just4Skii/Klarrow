# Security and privacy

- Synthetic data only; no real supplier or personal data in the seed pack.
- File uploads validate extension and size, use generated storage names, and reject path traversal.
- Original file bytes are stored outside the DB; hash is recorded.
- API keys are only read from environment variables; the UI and health/status endpoints never receive keys.
- Document text is treated as untrusted data and framed separately from extraction instructions; extraction uses bounded schema-constrained JSON.
- No unsafe document rendering; PDF text extraction occurs server-side.
- React UI never renders document HTML directly.
- Large model services are disabled unless configured; status endpoints report unavailable rather than claiming readiness.
- Remaining risk: local unstructured document text may contain confidential information; aggregate before log or export.
