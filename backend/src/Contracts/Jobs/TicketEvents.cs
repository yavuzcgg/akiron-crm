using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Jobs;

/// <summary>
/// A support request was opened: by the client in the portal (<paramref name="FromClient"/>), or by
/// staff on the client's behalf (a phone call, an e-mail).
/// </summary>
[IntegrationEventName("jobs.ticket.opened")]
public sealed record TicketOpened(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid TicketId,
    string Number,
    string Subject,
    Guid PartyId,
    string PartyName,
    string Priority,
    bool FromClient,
    string RequesterName,
    Guid? AssigneeUserId,
    Guid? OpenedByUserId,
    string? OpenedByName = null) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A ticket changed state (<c>open</c>, <c>in_progress</c>, <c>waiting_client</c>, <c>resolved</c>, <c>closed</c>).</summary>
[IntegrationEventName("jobs.ticket.status_changed")]
public sealed record TicketStatusChanged(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid TicketId,
    string Number,
    string Subject,
    Guid PartyId,
    string FromStatus,
    string ToStatus,
    bool ByClient,
    string? ChangedByName,
    Guid? ChangedByUserId,
    Guid? AssigneeUserId) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A ticket was given to someone (not by themselves).</summary>
[IntegrationEventName("jobs.ticket.assigned")]
public sealed record TicketAssigned(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid TicketId,
    string Number,
    string Subject,
    string PartyName,
    Guid AssigneeUserId,
    Guid AssignedByUserId,
    string? AssignedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>Nobody answered the client within the time its priority allows.</summary>
[IntegrationEventName("jobs.ticket.response_overdue")]
public sealed record TicketResponseOverdue(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid TicketId,
    string Number,
    string Subject,
    string PartyName,
    Guid? AssigneeUserId) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A ticket turned out to be work: a work order was opened from it.</summary>
[IntegrationEventName("jobs.ticket.converted")]
public sealed record TicketConverted(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid TicketId,
    string Number,
    Guid PartyId,
    Guid WorkOrderId,
    string WorkOrderNumber,
    Guid ConvertedByUserId,
    string? ConvertedByName) : IntegrationEvent(TenantId, OccurredAt);
