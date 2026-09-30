using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Crm;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>Turns CRM events into lines on the party's own stream and the workspace stream.</summary>
internal sealed class PartyCreatedProjection(TimelineWriter writer) : IIntegrationEventConsumer<PartyCreated>
{
    public Task HandleAsync(PartyCreated integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.PartyCreated,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.CreatedByUserId, integrationEvent.CreatedByName),
                Payload.Of(new
                {
                    partyId = integrationEvent.PartyId,
                    partyName = integrationEvent.Name,
                    code = integrationEvent.Code,
                    isCustomer = integrationEvent.IsCustomer,
                    isSupplier = integrationEvent.IsSupplier,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                    new TimelineSubject(TimelineSubjects.Workspace, integrationEvent.TenantId.Value),
                ]),
            cancellationToken);
}

internal sealed class PartyUpdatedProjection(TimelineWriter writer) : IIntegrationEventConsumer<PartyUpdated>
{
    public Task HandleAsync(PartyUpdated integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.PartyUpdated,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.UpdatedByUserId, integrationEvent.UpdatedByName),
                Payload.Of(new { partyId = integrationEvent.PartyId, partyName = integrationEvent.Name, fields = integrationEvent.ChangedFields }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId)]),
            cancellationToken);
}

internal sealed class PartyArchivedProjection(TimelineWriter writer) : IIntegrationEventConsumer<PartyArchived>
{
    public Task HandleAsync(PartyArchived integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.PartyArchived,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.ArchivedByUserId, integrationEvent.ArchivedByName),
                Payload.Of(new { partyId = integrationEvent.PartyId, partyName = integrationEvent.Name }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                    new TimelineSubject(TimelineSubjects.Workspace, integrationEvent.TenantId.Value),
                ]),
            cancellationToken);
}

internal sealed class PartyContactAddedProjection(TimelineWriter writer) : IIntegrationEventConsumer<PartyContactAdded>
{
    public Task HandleAsync(PartyContactAdded integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.PartyContactAdded,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.AddedByUserId, integrationEvent.AddedByName),
                Payload.Of(new { partyId = integrationEvent.PartyId, partyName = integrationEvent.PartyName, contactName = integrationEvent.ContactName }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId)]),
            cancellationToken);
}
