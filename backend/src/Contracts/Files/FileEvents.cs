using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Files;

/// <summary>A file was attached to a record (<paramref name="SubjectType"/> is one of <see cref="Subjects"/>).</summary>
[IntegrationEventName("files.file.uploaded")]
public sealed record FileUploaded(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid FileId,
    string FileName,
    long SizeBytes,
    string SubjectType,
    Guid SubjectId,
    Guid UploadedByUserId,
    string? UploadedByName) : IntegrationEvent(TenantId, OccurredAt);
