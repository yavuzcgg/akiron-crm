using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.People;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>Leave lines on the person's own stream, visible to approvers only (see TimelineEntryTypes).</summary>
internal sealed class LeaveRequestedProjection(TimelineWriter writer) : IIntegrationEventConsumer<LeaveRequested>
{
    public Task HandleAsync(LeaveRequested integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.LeaveRequested,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.UserId, integrationEvent.UserName),
                Payload.Of(new { type = integrationEvent.Type, startDate = integrationEvent.StartDate, endDate = integrationEvent.EndDate, days = integrationEvent.Days }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.User, integrationEvent.UserId)]),
            cancellationToken);
}

internal sealed class LeaveDecidedProjection(TimelineWriter writer) : IIntegrationEventConsumer<LeaveDecided>
{
    public Task HandleAsync(LeaveDecided integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.LeaveDecided,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.DecidedByUserId, integrationEvent.DecidedByName),
                Payload.Of(new
                {
                    approved = integrationEvent.Approved,
                    type = integrationEvent.Type,
                    startDate = integrationEvent.StartDate,
                    endDate = integrationEvent.EndDate,
                    days = integrationEvent.Days,
                }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.User, integrationEvent.UserId)]),
            cancellationToken);
}
