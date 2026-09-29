# Architecture Decision Records

Short records of decisions that are expensive to reverse. Format: Context, Decision, Alternatives, Consequences, Exit strategy. Product-level scope lives in [PLAN.md](../PLAN.md) and [MODULES.md](../MODULES.md).

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-modular-monolith.md) | Modular monolith, one schema per module, one host | Accepted |
| [0002](0002-multi-tenancy.md) | Shared database, `TenantId` column, filters and interceptors | Accepted |
| [0003](0003-plain-handlers-no-mediator.md) | Plain handlers, no mediator library | Accepted |
| [0004](0004-document-chain-money-tax.md) | Document chain (collections allocated, not chained), money, tax and numbering | Accepted |
| [0005](0005-integrations-behind-providers.md) | Integrations behind provider interfaces; encrypted tenant credentials | Accepted |
| [0006](0006-error-codes-not-messages.md) | Errors carry stable codes; the client translates | Accepted |
| [0007](0007-auth-cookie-jwt-permissions.md) | Custom cookie JWT auth with refresh rotation; permission-based authorization | Accepted |
| [0008](0008-single-party-cari.md) | One party model (Cari) for customers, suppliers and both | Accepted |
| [0009](0009-activity-timeline.md) | Activity timeline as a first-class read model | Accepted |
