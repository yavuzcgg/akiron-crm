using Akiron.BuildingBlocks.Domain;

namespace Akiron.BuildingBlocks.Events;

/// <summary>
/// Something that happened in one module that others may react to (ADR-0001). Written to the
/// emitting module's outbox in the same transaction as the change, then delivered at least once;
/// consumers must be idempotent, keyed on <see cref="EventId"/>.
/// </summary>
/// <remarks>
/// Payloads are snapshots: they carry what a consumer needs (names, numbers, amounts) so it never
/// has to call back into the emitting module. Declared in Akiron.Contracts, named with
/// <see cref="IntegrationEventNameAttribute"/>.
/// </remarks>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    TenantId TenantId { get; }

    DateTimeOffset OccurredAt { get; }
}

public abstract record IntegrationEvent(TenantId TenantId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
}

/// <summary>
/// The stable name an event is stored under, e.g. <c>identity.member.joined</c>. Renaming the C#
/// type is safe; renaming this breaks undelivered outbox rows.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
