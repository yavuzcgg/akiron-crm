# ADR-0006: Errors carry stable codes; the client translates

Status: Accepted · Date: 2026-09-29 · Adopted from akiron-commerce ADR-0018

## Context

The UI is Turkish first with English planned; error text produced by the server would have to be localized server-side and would leak internal wording (akiron-seo maps `InvalidOperationException` to a 400 that echoes the exception message).

## Decision

- All errors are RFC 9457 ProblemDetails with a stable `code` of the form `module.resource.reason` (e.g. `finance.invoice.locked_period`, `crm.customer.duplicate_tax_number`) and optional `params` for interpolation. The frontend dictionary maps codes to text.
- Validation failures return `400` with `errors: { field: [code, …] }`.
- Mapping: domain rule → 422 with code; not found → 404; permission → 403; conflict/unique violation → 409 with a named code (Postgres violation names are translated by an exception handler, as in akiron-commerce `CatalogExceptionHandler`); anything unmapped → 500 with a correlation id and no internal message.
- Handlers return `Result<T>` with an `Error(code, params)`; exceptions are for truly exceptional paths.
- Every response includes `X-Correlation-Id`; the same id is on the log line.

## Consequences

- Error codes are part of the API contract and are listed per module in the OpenAPI document (`x-error-codes`).
- Frontend tests assert on codes, never on Turkish strings.
