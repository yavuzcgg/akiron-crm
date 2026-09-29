# ADR-0004: Document chain, money, tax and numbering rules

Status: Accepted · Date: 2026-09-29

## Context

Turkish bookkeeping has hard rules: invoices are legal e-documents issued through a GİB-licensed integrator with gapless per-series numbering; VAT has several rates plus partial withholding (tevkifat) codes; many agencies bill in USD/EUR; finalized documents must not change. Getting these wrong later means data migrations across every finance table.

## Decision

**Document chain (canonical; PLAN.md and MODULES.md follow this)**

Commercial documents form a chain; money does not. Collections and payments are a separate axis, linked to documents by allocations.

```text
Commercial axis                  Money axis
Quote ──► Contract? ──► WorkOrder ──► Invoice
  │           │             │            │
  └───────────┴─────────────┴────────────┴──◄ Allocation ──► Collection / Payment
```

- Commercial chain, agency: Quote → Contract (optional; retainers and larger jobs) → Work order → Invoice. Product chain (phase 7): Quote → Sales order → Delivery note → Invoice. Each document stores its source document id; nothing is copied without a link. One contract can produce many work orders and many invoices (monthly retainer).
- **A collection is never a step in the chain.** It is recorded against the party (cari) and then allocated, in part or in full, to any open item: a quote or contract (advance/deposit), a payment-plan instalment, or an invoice. One collection can be split across several items; one invoice can be settled by several collections.
- An unallocated collection is a **credit on the party's account** (advance). When the invoice is issued later, the advance is matched to it: automatically by the order agreed on the payment plan, or by hand. This mirrors Logo's model: every module posts debit/credit lines to one account ledger (`LG_CLFLINE`), and open items with their paid amount and the item that closed them are tracked separately (`LG_PAYTRANS`).
- Therefore the finance core is three things: the **party ledger** (append-only debit/credit lines from invoices, collections, payments, checks, openings), **open items** (what is due and when: invoice lines, instalments, deposits requested), and **allocations** (which money closed which item, with the exchange rate used). Balances and aging are computed from these, never stored on the party.
- Status flow for every commercial document: `Draft → Sent → Approved | Rejected → Converted`. A finalized document (sent invoice, approved quote) is immutable; changes create a new revision or a reversing document (return invoice, cancellation). Reversing a collection reverses its allocations with it.
- Financial records are never deleted; they are cancelled or reversed. Other entities soft-delete.
- A tenant-level **lock date** blocks edits to documents dated before it.

**Money**
- `Money` value object (from akiron-commerce) with currency; totals stored as `numeric(18,2)`, unit prices as `numeric(18,4)`.
- Every document carries `Currency` and `ExchangeRate` captured at creation from the TCMB rate of that day; the rate never changes afterwards. Reports show both the original currency and the TRY equivalent.
- Half-away-from-zero rounding, applied once per line and once per total, matching integrator expectations.

**Tax**
- Tax lives on lines: VAT rate (0/1/10/20), withholding (tevkifat) code and fraction from the GİB list (seeded as data), stopaj rate for e-SMM/gider pusulası, exemption code. Document totals (net, VAT, withheld VAT, payable) are stored, not recomputed, once finalized.
- Price lists declare whether prices include VAT.

**Numbering**
- Internal documents (quotes, work orders, receipts) use per-tenant, per-series, per-year counters formatted like `TKL-2026-0001`, allocated with a row-locked counter table (`UPDATE … RETURNING`), so numbers reset yearly and do not skip on rollback.
- **Invoice numbers are assigned by the e-invoice integrator** (series + year + 9 digits, gapless as required by GİB). The system stores the internal draft number, the GİB number and the ETTN/UUID separately. Draft invoices never carry a GİB number.

**Documents received**
- Purchase invoices, e-SMM (freelancer receipts with stopaj) and gider pusulası are first-class "incoming documents" that post to the supplier's account and to expenses.

## Alternatives considered

- Database sequences for numbering (lastik-depo) — skip numbers on rollback and do not reset yearly; fine internally, unacceptable for invoices.
- Storing amounts only in TRY — loses the original currency needed for exchange-difference invoices later.
- Collection as the step after the invoice (Invoice → Collection) — cannot represent deposits taken when the quote is approved, instalments paid before invoicing, or one transfer that pays three invoices; all three are everyday cases for agencies.
- A running balance column on the party — fast to read, but it drifts from the ledger the first time a bug or a concurrent write slips through; computed balances (with an index, later a snapshot) stay correct by construction.

## Consequences

- Finance tables are append-heavy; storage is cheap, audit is free.
- Opening balances (onboarding from Excel/another tool) are ordinary "opening" documents, not edits to history.

## Exit strategy

Rules are encoded in BuildingBlocks value objects and one numbering service; a change in GİB rules is a change in data (rate/code tables) or in the integrator adapter, not in module code.
