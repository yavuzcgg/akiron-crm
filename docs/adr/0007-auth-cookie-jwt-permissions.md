# ADR-0007: Custom cookie JWT auth with refresh rotation; permission-based authorization

Status: Accepted · Date: 2026-09-29

## Context

akiron-seo and lastik-depo both run a small custom auth stack the owner has already hardened and tested: PBKDF2-SHA512 hashing, HS256 access tokens in an HttpOnly cookie, hashed refresh tokens rotated per use with family-wide revocation on reuse, startup secret validation, per-IP rate limits on auth endpoints. ASP.NET Identity would add its schema and abstractions without adding anything these need. Roles alone were not enough in either project: finance data must be hidden from staff who can still manage jobs.

## Decision

- Reuse the akiron-seo stack (cookie manager, JWT service, refresh rotation with `FamilyId`, secrets validator, hasher behind `IPasswordHasher`); no ASP.NET Identity.
- Two cookies: access token (path `/api`, 30 min) and refresh token (path `/api/v1/identity/auth`, long-lived, rotated). `SameSite=Lax`, `Secure` outside Development. Tokens never appear in JSON bodies.
- Users are global; `Membership` links a user to a tenant with a role. The token carries the active `tenant_id`; switching tenants issues a new token pair.
- **Permission-based authorization.** A static permission catalog (`crm.customers.read`, `finance.invoices.write`, `people.leave.approve`, …) is the unit of access. Roles are named permission sets: `Owner` (all), `Admin` (all except billing/tenant deletion), `Member` (tenant-configurable), plus custom roles. Endpoints declare `.RequirePermission(...)`; the policy provider creates policies on demand (lastik-depo pattern). Permissions are embedded in the access token as a compact claim and re-validated against the database on refresh.
- Client portal users are a separate principal type (`PortalUser`) with their own login and cookie names; they never receive staff permissions.
- 2FA (TOTP) and SSO are planned for phase 8; the token/claims model leaves room for `amr` claims.

## Alternatives considered

- **ASP.NET Identity** — heavy schema, awkward multi-tenant membership, no benefit for a cookie-JWT API.
- **External IdP (Keycloak, Auth0)** — extra moving part for on-prem installs and monthly cost for SaaS; can front this stack later via OIDC if a customer requires SSO.
- **Roles only** — insufficient for finance visibility.

## Consequences

- Every endpoint names its permission; architecture tests fail on anonymous non-public endpoints.
- Permission catalog is generated into the frontend for menu/button visibility.

## Exit strategy

The API is cookie-based OIDC-shaped enough that an external IdP can be introduced by replacing the token issuance only.
