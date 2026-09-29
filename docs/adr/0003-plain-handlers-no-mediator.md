# ADR-0003: Plain handlers, no mediator library

Status: Accepted · Date: 2026-09-29

## Context

akiron-seo uses MediatR; akiron-commerce (ADR-0006 there) pins MediatR to 12.5.0 because v13 became commercial, and limits it to services with real command/query asymmetry, using plain handler classes elsewhere. This monolith will have many modules written by one developer; uniformity matters more than a pipeline abstraction, and a license pin is a liability in a product meant to be sold and self-hosted.

## Decision

- No mediator. A use case is a `sealed` class with a single `HandleAsync(command, ct)` returning `Result<T>`, registered scoped by a small `AddHandlersFromAssembly()` helper in BuildingBlocks (convention: class name ends with `Handler`).
- Endpoints inject the handler directly: `group.MapPost("/customers", (CreateCustomerCommand cmd, CreateCustomerHandler h, CancellationToken ct) => …)`.
- Cross-cutting behavior that MediatR pipelines would provide is done with endpoint filters and decorators: validation (`.Validate<T>()` FluentValidation filter), authorization (`.RequirePermission(…)`), transactions (`IUnitOfWork` around the handler where a use case touches several aggregates), logging/tracing (middleware + OpenTelemetry).
- Domain events are raised on aggregates and collected by the `SaveChanges` interceptor; integration events go to the outbox (ADR-0001). No in-process notification bus is needed.

## Alternatives considered

- **MediatR 12.5.0 pinned** — works, but adds indirection to every slice and a frozen dependency.
- **Wolverine / MassTransit mediator** — couples the mediator choice to a messaging framework we do not need.
- **Hand-written pipeline abstraction** — reinvents MediatR; the filter/decorator approach covers the actual needs.

## Consequences

- Handlers are trivially unit-testable (constructor + one method).
- If a module ever needs a real pipeline (e.g. idempotency keys on payment commands), it is added as an endpoint filter or a decorator on that handler, not globally.

## Exit strategy

Handlers already have the `Command → Result` shape; adapting them to any mediator later is mechanical.
