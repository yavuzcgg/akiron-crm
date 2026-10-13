using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Jobs;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>A ticket's life on its own stream, where the client follows it too; its opening also shows on the party's.</summary>
internal sealed class TicketOpenedProjection(TimelineWriter writer) : IIntegrationEventConsumer<TicketOpened>
{
    public Task HandleAsync(TicketOpened integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.TicketOpened,
                integrationEvent.OccurredAt,
                integrationEvent.OpenedByUserId is { } userId && !integrationEvent.FromClient
                    ? TimelineActor.User(userId, integrationEvent.OpenedByName)
                    : TimelineActor.ClientContact(integrationEvent.RequesterName),
                Payload.Of(new
                {
                    ticketId = integrationEvent.TicketId,
                    number = integrationEvent.Number,
                    subject = integrationEvent.Subject,
                    priority = integrationEvent.Priority,
                    fromClient = integrationEvent.FromClient,
                    requesterName = integrationEvent.RequesterName,
                    partyId = integrationEvent.PartyId,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Ticket, integrationEvent.TicketId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ],
                TimelineVisibility.Portal),
            cancellationToken);
}

internal sealed class TicketStatusChangedProjection(TimelineWriter writer) : IIntegrationEventConsumer<TicketStatusChanged>
{
    public Task HandleAsync(TicketStatusChanged integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.TicketStatusChanged,
                integrationEvent.OccurredAt,
                integrationEvent.ByClient
                    ? TimelineActor.ClientContact(integrationEvent.ChangedByName)
                    : integrationEvent.ChangedByUserId is { } userId ? TimelineActor.User(userId, integrationEvent.ChangedByName) : TimelineActor.SystemActor,
                Payload.Of(new
                {
                    ticketId = integrationEvent.TicketId,
                    number = integrationEvent.Number,
                    subject = integrationEvent.Subject,
                    from = integrationEvent.FromStatus,
                    to = integrationEvent.ToStatus,
                }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Ticket, integrationEvent.TicketId)],
                TimelineVisibility.Portal),
            cancellationToken);
}

internal sealed class TicketConvertedProjection(TimelineWriter writer) : IIntegrationEventConsumer<TicketConverted>
{
    public Task HandleAsync(TicketConverted integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.TicketConverted,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.ConvertedByUserId, integrationEvent.ConvertedByName),
                Payload.Of(new
                {
                    ticketId = integrationEvent.TicketId,
                    number = integrationEvent.Number,
                    workOrderId = integrationEvent.WorkOrderId,
                    workOrderNumber = integrationEvent.WorkOrderNumber,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Ticket, integrationEvent.TicketId),
                    new TimelineSubject(TimelineSubjects.WorkOrder, integrationEvent.WorkOrderId),
                ]),
            cancellationToken);
}
