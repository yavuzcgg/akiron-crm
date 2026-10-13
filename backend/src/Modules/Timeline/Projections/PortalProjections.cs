using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Portal;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>Who was let into the portal, on the party's stream (staff only).</summary>
internal sealed class PortalUserInvitedProjection(TimelineWriter writer) : IIntegrationEventConsumer<PortalUserInvited>
{
    public Task HandleAsync(PortalUserInvited integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.PortalUserInvited,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.InvitedByUserId, integrationEvent.InvitedByName),
                Payload.Of(new { name = integrationEvent.FullName, email = integrationEvent.Email, partyId = integrationEvent.PartyId }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId)]),
            cancellationToken);
}

internal sealed class PortalAccessChangedProjection(TimelineWriter writer) : IIntegrationEventConsumer<PortalAccessChanged>
{
    public Task HandleAsync(PortalAccessChanged integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.PortalAccessChanged,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.ChangedByUserId, integrationEvent.ChangedByName),
                Payload.Of(new { name = integrationEvent.FullName, enabled = integrationEvent.Enabled, partyId = integrationEvent.PartyId }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId)]),
            cancellationToken);
}

/// <summary>Files crossing the staff/client line show on the record's thread, on both sides of it.</summary>
internal sealed class ClientFileUploadedProjection(TimelineWriter writer) : IIntegrationEventConsumer<ClientFileUploaded>
{
    public Task HandleAsync(ClientFileUploaded integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ClientFileUploaded,
                integrationEvent.OccurredAt,
                TimelineActor.ClientContact(integrationEvent.ClientName),
                Payload.Of(new { fileId = integrationEvent.FileId, fileName = integrationEvent.FileName, sizeBytes = integrationEvent.SizeBytes, partyId = integrationEvent.PartyId }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(integrationEvent.SubjectType, integrationEvent.SubjectId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ],
                TimelineVisibility.Portal),
            cancellationToken);
}

internal sealed class FileSharedWithClientProjection(TimelineWriter writer) : IIntegrationEventConsumer<FileSharedWithClient>
{
    public Task HandleAsync(FileSharedWithClient integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.FileSharedWithClient,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.SharedByUserId, integrationEvent.SharedByName),
                Payload.Of(new { fileId = integrationEvent.FileId, fileName = integrationEvent.FileName, partyId = integrationEvent.PartyId }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(integrationEvent.SubjectType, integrationEvent.SubjectId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ],
                TimelineVisibility.Portal),
            cancellationToken);
}
