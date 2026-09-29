# Akiron CRM — working rules

Agency-first business management and pre-accounting platform for the Turkish market (work orders, quotes, invoices, collections, messaging, client portal, content calendar; later inventory, assets, accounting sync). Read [docs/PLAN.md](docs/PLAN.md) for scope, [docs/MODULES.md](docs/MODULES.md) for the module catalog and [docs/adr/](docs/adr/README.md) for decisions before changing architecture.

Talk to the owner in Turkish. Write code, database columns, commits and ADRs in English.

## Non-negotiables

- **No AI attribution anywhere.** No `Co-Authored-By`, no "Generated with" lines in commits, PRs or files.
- **Conventional Commits with a module scope:** `feat(finance): …`, `fix(identity): …`, `docs: …`, `test(crm): …`, `chore(ci): …`.
- **Warnings are errors** (`Directory.Build.props`). Package versions live only in `Directory.Packages.props`.
- **Never** add `TenantId` filters or assignments in handlers; the DbContext interceptor does it (ADR-0002).
- **Never** delete financial records; cancel or reverse (ADR-0004).
- **Never** commit secrets; `.env.example` lists names only.

## Backend (`backend/`)

- .NET 10, Minimal APIs, EF Core + Npgsql, one schema per module (ADR-0001).
- Layout: `src/BuildingBlocks`, `src/Contracts` (integration events, no logic), `src/Modules/<Module>`, `src/Akiron.Api` (host), `tests/{Architecture,Integration}`. A unit-test project arrives with the first logic that needs no database.
- New module: project under `src/Modules/<Name>` referencing only BuildingBlocks and Contracts, an `IModule` class, a line in the host's module list (`Program.cs`) and one in `ArchitectureTests.Modules`.
- Cross-module effects: `db.Publish(new SomethingHappened(...))` before `SaveChanges`; the event record lives in `src/Contracts/<Module>` with an `[IntegrationEventName("module.thing.happened")]`. Consumers implement `IIntegrationEventConsumer<T>` and must be idempotent. Tests call `ApiFixture.DeliverOutboxAsync()` instead of waiting.
- Use `TimeProvider` for the clock, `ITenantContext` for the tenant, `ICurrentUser` for the actor; never `DateTime.Now` or claims directly.
- A use case is a folder `Features/<UseCase>/` holding the command/query record, its validator, a `sealed` handler with `HandleAsync` returning `Result<T>`, and its endpoint mapping. No mediator (ADR-0003).
- Forbidden folder and type names: `Services`, `Repositories`, `Managers`, `Helpers`, `Utils`, `Dtos`. Name things after what they do.
- Entities: private setters, `Create`/`Update` factories, typed GUIDv7 ids (`CustomerId`), domain rules inside the entity.
- Routes: `/api/v1/<module>/<kebab-case>`; paging `page`/`pageSize` capped at 100, invalid values rejected.
- Errors: ProblemDetails with a stable code `module.resource.reason` (ADR-0006). Do not put Turkish text in the API.
- Money: `Money` value object, `numeric(18,2)` totals, `numeric(18,4)` unit prices, rate snapshot per document (ADR-0004).
- Counterparties are one `Party` (Cari) with customer/supplier flags; never create `Customer` or `Supplier` entities (ADR-0008).
- Collections and payments are allocated to open items, never chained after the invoice; balances are computed from the party ledger (ADR-0004).
- A module is not done until its business events are projected onto the activity timeline with a typed entry and TR/EN templates (ADR-0009).
- `docs/vendor/` holds licensed reference material (Logo data dictionary) and is git-ignored; never commit or quote it at length.
- Dates: UTC in the database; `DateOnly` for calendar dates (invoice date, due date).
- Every endpoint declares `.RequirePermission(…)` unless it is in the `Public` group, serves global reference data (exchange rates, code lists), or returns the caller's own data (their notifications); those use `.RequireAuthorization()`.
- Auditable entities (`IAuditable`) get a field-level trail in their module's `audit_changes` automatically; mark secrets and per-request noise with `[AuditIgnore]`.
- Money is `Money`/`Currency` from BuildingBlocks; rates come from `IExchangeRates` (Contracts), never from a module's own HTTP call.
- Migrations: `dotnet ef migrations add <Name> --project src/Modules/<Module> --startup-project src/Akiron.Api --context <Module>DbContext`. Never `EnsureCreated`.
- Tests run against real PostgreSQL (Testcontainers). Names: `Method_WithCondition_DoesThing`. Every module has the tenant isolation and scoped-write tests.

## Frontend (`frontend/`)

- Next.js App Router, TypeScript strict, Tailwind v4, shadcn/ui, TanStack Query + Table, react-hook-form + zod.
- API types are generated (`npm run api:gen`, with the API running) into `src/lib/api/schema.d.ts` and committed; never hand-write response interfaces. Call the API through `api` from `src/lib/api/client.ts` and `unwrap()` results.
- The browser only talks to the Next.js origin; `next.config.ts` rewrites `/api/*` to the backend, so session cookies stay first-party. Never put tokens in JavaScript.
- shadcn/ui here is the Base UI flavour (`base-nova`): compose with `render={<Link … />}`, not `asChild`.
- All user-facing text goes through the typed dictionary (`src/lib/i18n`); `tr` is the default, missing `en` keys are a type error. Error codes map to text there.
- Route groups: `(auth)`, `(app)/<module>`, `(portal)`. Feature code lives in `src/features/<module>/`.
- Money and dates are formatted with `src/lib/format.ts` (`tr-TR`), never inline.

## Commands

Local ports avoid the other Akiron projects on this machine: Postgres 5434, API 5080, web 3100, MinIO 9010 (console 9011, akiron / akiron_dev_minio), Mailpit SMTP 1026 (inbox at http://localhost:8026).

```bash
docker compose up -d                                   # Postgres 17, MinIO, Mailpit
dotnet build backend/Akiron.slnx                       # warnings are errors
dotnet test --solution backend/Akiron.slnx             # needs Docker for Testcontainers
dotnet format backend/Akiron.slnx --verify-no-changes
dotnet run --project backend/src/Akiron.Api            # http://localhost:5080 (Scalar at /scalar, health at /health/ready)
cd frontend && npm run dev                             # http://localhost:3100
cd frontend && npm run api:gen                         # regenerate API types (API must be running)
cd frontend && npm run typecheck && npm run lint && npm test && npm run build
```

## Process

- Phases and sprints are in `docs/PLAN.md`; do not start a later phase's module early.
- Decisions that are hard to reverse get an ADR in `docs/adr/` (Context, Decision, Alternatives, Consequences, Exit strategy).
- Keep `docs/INTEGRATIONS.md` checklist current when a vendor account changes state.
