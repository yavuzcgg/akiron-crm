using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Jobs;

/// <summary>
/// A stage as events carry it. System stages have a <paramref name="Key"/> and no name (the client
/// translates the key); stages a tenant created or renamed have a name.
/// </summary>
public sealed record StageRef(Guid StageId, string? Key, string? Name);

[IntegrationEventName("jobs.work_order.created")]
public sealed record WorkOrderCreated(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid WorkOrderId,
    string Number,
    string Title,
    Guid? PartyId,
    string? PartyName,
    IReadOnlyList<Guid> AssigneeIds,
    Guid CreatedByUserId,
    string? CreatedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A work order changed stage; <paramref name="Completed"/> when the new stage is a done stage.</summary>
[IntegrationEventName("jobs.work_order.moved")]
public sealed record WorkOrderMoved(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid WorkOrderId,
    string Number,
    string Title,
    Guid? PartyId,
    StageRef From,
    StageRef To,
    bool Completed,
    Guid MovedByUserId,
    string? MovedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>People were added to a work order (at creation or later); each gets notified.</summary>
[IntegrationEventName("jobs.work_order.assigned")]
public sealed record WorkOrderAssigned(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid WorkOrderId,
    string Number,
    string Title,
    IReadOnlyList<Guid> AddedUserIds,
    Guid AssignedByUserId,
    string? AssignedByName) : IntegrationEvent(TenantId, OccurredAt);
