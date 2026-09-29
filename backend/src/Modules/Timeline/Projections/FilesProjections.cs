using Akiron.BuildingBlocks.Events;
using Akiron.Contracts;
using Akiron.Contracts.Files;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

internal sealed class FileUploadedProjection(TimelineWriter writer) : IIntegrationEventConsumer<FileUploaded>
{
    public Task HandleAsync(FileUploaded integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.FileUploaded,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.UploadedByUserId, integrationEvent.UploadedByName),
                Payload.Of(new { fileId = integrationEvent.FileId, fileName = integrationEvent.FileName, sizeBytes = integrationEvent.SizeBytes }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(integrationEvent.SubjectType, integrationEvent.SubjectId),
                    new TimelineSubject(Subjects.Workspace, integrationEvent.TenantId.Value),
                ]),
            cancellationToken);
}
