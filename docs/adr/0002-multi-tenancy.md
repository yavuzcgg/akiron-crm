# ADR-0002: Shared database, `TenantId` column, filters and interceptors

Status: Accepted · Date: 2026-09-29

## Context

SaaS with many small tenants (agencies), plus an on-prem mode with exactly one tenant. Tenant data must never leak; developers must not be able to forget the tenant filter. akiron-seo proved the query-filter approach but set `TenantId` by hand in every handler, and one dropped middleware once produced rows with `Guid.Empty` (recorded in its `TenantScopedWriteTests`).

## Decision

- Shared Postgres database; every tenant-scoped table has a `tenant_id` column and implements `ITenantScoped`. `BranchId` is optional for multi-branch tenants.
- `ITenantContext` is scoped per request. The tenant is resolved from the JWT `tenant_id` claim after authentication; anonymous requests resolve to no tenant and filtered queries return nothing.
- Public-link pages (quote approval, payment page) and inbound webhooks (PayTR callback, WhatsApp) resolve the tenant from a signed token or provider identifier and set `ITenantContext` explicitly; these endpoints live in a dedicated `Public` route group with their own signature checks.
- `ModuleDbContext` applies global query filters for `ITenantScoped` and `ISoftDeletable` to every entity via the reflection loop taken from akiron-seo.
- A `SaveChanges` interceptor (new here): stamps `TenantId` on added rows, **rejects** modified or added rows whose `TenantId` differs from the current tenant, stamps `CreatedAt/By` and `UpdatedAt/By`, converts deletes of `ISoftDeletable` entities into soft deletes, and writes audit change rows for `IAuditable` entities.
- Handlers never set `TenantId` and never add `.Where(x => x.TenantId == …)`; the architecture tests flag both.
- Uniqueness is always per tenant: partial unique indexes on `(tenant_id, …) WHERE is_deleted = false`.
- Super-admin/admin endpoints that must cross tenants use `IgnoreQueryFilters()` behind a dedicated policy and are the only place allowed to do so.
- Postgres row-level security is not enabled now; the interceptor and tests are the guard. RLS can be added later without changing application code.

## Alternatives considered

- **Database per tenant** — strongest isolation, but migrations, backups and connection pooling become a per-tenant operation; too heavy for one developer.
- **Schema per tenant** — same operational cost with EF Core migrations; schemas are already used per module.
- **Tenant from subdomain/header** — planned for later (custom domains for the client portal); the claim stays the source of truth.

## Consequences

- One user can belong to several tenants (memberships); the token carries the active one, and switching issues a new token.
- Every module's integration tests include the two akiron-seo suites adapted: isolation (tenant A cannot read B) and scoped writes (rows get the right tenant through the full HTTP pipeline).
- On-prem installs run the same code with a single tenant created at setup.

## Exit strategy

Moving a large tenant to its own database is possible by pointing its connection string elsewhere at the `ITenantContext` level; the column-based model stays valid in both places.
