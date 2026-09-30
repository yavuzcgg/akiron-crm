using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Jobs;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>Work order lines: on the work order's stream, and on its client's stream when it has one.</summary>
internal sealed class WorkOrderCreatedProjection(TimelineWriter writer) : IIntegrationEventConsumer<WorkOrderCreated>
{
    public Task HandleAsync(WorkOrderCreated integrationEvent, CancellationToken cancellationToken)
    {
        List<TimelineSubject> subjects =
        [
            new(TimelineSubjects.WorkOrder, integrationEvent.WorkOrderId),
            new(TimelineSubjects.Workspace, integrationEvent.TenantId.Value),
        ];
        if (integrationEvent.PartyId is { } partyId)
        {
            subjects.Add(new TimelineSubject(TimelineSubjects.Party, partyId));
        }

        return writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.WorkOrderCreated,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.CreatedByUserId, integrationEvent.CreatedByName),
                Payload.Of(new
                {
                    workOrderId = integrationEvent.WorkOrderId,
                    number = integrationEvent.Number,
                    title = integrationEvent.Title,
                    partyName = integrationEvent.PartyName,
                }),
                integrationEvent.EventId.ToString(),
                subjects),
            cancellationToken);
    }
}

internal sealed class WorkOrderMovedProjection(TimelineWriter writer) : IIntegrationEventConsumer<WorkOrderMoved>
{
    public Task HandleAsync(WorkOrderMoved integrationEvent, CancellationToken cancellationToken)
    {
        List<TimelineSubject> subjects = [new(TimelineSubjects.WorkOrder, integrationEvent.WorkOrderId)];

        // Only finishing a job is news for the client's stream and the workspace; every drag is not.
        if (integrationEvent.Completed)
        {
            subjects.Add(new TimelineSubject(TimelineSubjects.Workspace, integrationEvent.TenantId.Value));
            if (integrationEvent.PartyId is { } partyId)
            {
                subjects.Add(new TimelineSubject(TimelineSubjects.Party, partyId));
            }
        }

        return writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.WorkOrderMoved,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.MovedByUserId, integrationEvent.MovedByName),
                Payload.Of(new
                {
                    workOrderId = integrationEvent.WorkOrderId,
                    number = integrationEvent.Number,
                    title = integrationEvent.Title,
                    fromKey = integrationEvent.From.Key,
                    fromName = integrationEvent.From.Name,
                    toKey = integrationEvent.To.Key,
                    toName = integrationEvent.To.Name,
                    completed = integrationEvent.Completed,
                }),
                integrationEvent.EventId.ToString(),
                subjects),
            cancellationToken);
    }
}
