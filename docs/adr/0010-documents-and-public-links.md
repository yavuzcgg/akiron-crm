# ADR-0010: Documents as PDF with QuestPDF; public links for client decisions

Status: Accepted · Date: 2026-10-02

## Context

Quotes (Phase 3), contracts and later invoices go to clients who have no account. They need
a document that looks the same everywhere (PDF), and a way to answer it (accept, reject, pay)
without signing up. Both have to work in a multi-tenant system where the tenant normally
comes from the signed-in user's token (ADR-0002).

## Decision

- **PDF:** QuestPDF, rendered in-process from the same entity the screens show. Community
  licence (free under USD 1M yearly revenue for the business using it). Labels in Turkish by
  default, English on request; amounts in the document's currency with Turkish formatting.
- **Public links:** a 256-bit random secret per sent revision, e-mailed to the recipient and
  returned once to the sender (to share by hand, e.g. WhatsApp). Only its SHA-256 hash is
  stored. Revising a quote clears the hash, so old links stop working.
- **Tenant on public endpoints:** endpoints marked `AsPublicLink()` are anonymous and skip
  token-based tenant resolution; the handler finds the record by hash across tenants (the one
  deliberate `IgnoreQueryFilters`) and binds the scope to that record's tenant before anything
  else is read or written. A signed-in visitor from another agency therefore sees the quote,
  not their own workspace.
- **What was sent is kept:** each send stores a JSON snapshot of the revision.

## Alternatives

- HTML-to-PDF (Playwright/Chromium): pixel-perfect CSS, but a browser in the server image and
  slower cold starts. Rejected for now.
- Signed JWT links instead of stored hashes: no lookup table, but cannot be revoked per
  revision without a deny list. Rejected.
- Client portal accounts for every recipient: heavier for a one-off approval; the portal
  (Phase 4) will sit next to links, not replace them.

## Consequences

- One more cross-tenant code path, kept small and covered by an isolation test.
- Rendering is CPU work on the request thread; fine at quote volumes, revisit for bulk
  invoice runs (move to a background job).

## Exit strategy

`QuotePdf` is a single static renderer; swapping the library touches one file per document.
If revenue passes QuestPDF's community threshold, buy the professional licence or move to the
HTML route behind the same method.
