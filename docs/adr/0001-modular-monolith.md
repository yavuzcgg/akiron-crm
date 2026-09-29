# ADR-0001: Modular monolith, one schema per module, one host

Status: Accepted · Date: 2026-09-29

## Context

The product spans a dozen business areas (CRM, jobs, people, finance, messaging, portal, social, inventory, assets) that will be built over ~2 years by one developer. Modules must be sold and enabled per tenant, and the same build must run as SaaS and as a single-company on-prem install. Microservices would multiply deployment and testing cost with no current scaling need; a plain layered monolith would let modules grow into each other's tables.

## Decision

- One deployable host (`backend/src/Akiron.Api`) that loads modules from an explicit list; no reflection-based discovery.
- One project per module under `backend/src/Modules/<Module>/`, plus `Akiron.BuildingBlocks` (cross-cutting code) and `Akiron.Contracts` (integration events, public read interfaces, shared typed ids). Module projects reference only BuildingBlocks and Contracts — never each other. The compiler enforces the boundary; NetArchTest enforces folder and namespace rules inside a project.
- Each module owns a Postgres schema named after it (`crm`, `jobs`, `finance`, …), its own `DbContext` derived from `ModuleDbContext`, and its own migrations with `MigrationsHistoryTable("__EFMigrationsHistory", "<schema>")`. No cross-schema joins in code.
- A module exposes `IModule { Schema; AddModule(services, configuration); MapEndpoints(endpoints); }` and maps its routes under `/api/v1/<module>`.
- Modules communicate through (1) integration events written to the module's own `outbox_messages` table in the same transaction (`ModuleDbContext.Publish`) and delivered by `OutboxProcessor` (a hosted service claiming rows with `FOR UPDATE SKIP LOCKED`, retrying with backoff) to `IIntegrationEventConsumer<T>` implementations in other modules (at-least-once; consumers idempotent on the event id). Hangfire arrives with the first scheduled job, not for the outbox; (2) synchronous reads through interfaces declared in Contracts and implemented by the owning module; (3) denormalized snapshots (e.g. `customer_name` on a work order) kept fresh by events.
- Inside a module: vertical slices `Features/<UseCase>/{Command|Query, Handler, Validator, Endpoint}` plus `Domain/` and `Persistence/`.
- Tenant module flags decide which modules' endpoints and navigation are active for a tenant; code for all modules ships in every build.

## Alternatives considered

- **Microservices per module** — operational cost and distributed-transaction complexity far exceed the benefit at this scale.
- **Single project, folders per module** — cheapest, but boundaries erode; `git blame` on other repos shows this happening within weeks.
- **Separate database per module** — makes on-prem install and backups harder for no gain; schemas give the same ownership clarity.

## Consequences

- Adding a module = new project + schema + `IModule` + registration line in the host; template to be kept in `docs/`.
- Reports that span modules read through Contracts or use a dedicated reporting read model, not ad-hoc joins.
- Test suites: one integration project with per-module folders sharing one Postgres container; architecture tests fail the build on boundary violations.

## Exit strategy

Because modules already own their schema and talk through events and contracts, any module can be extracted into its own service by moving the outbox transport to a broker and the Contracts interfaces to HTTP/gRPC. Nothing in this ADR prevents that; nothing requires it.
