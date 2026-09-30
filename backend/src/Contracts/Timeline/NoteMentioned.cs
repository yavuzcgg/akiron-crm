using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Timeline;

/// <summary>
/// Someone was @mentioned in a note on a record (<paramref name="SubjectType"/> is one of
/// <see cref="Subjects"/>); each of <paramref name="MentionedUserIds"/> gets told.
/// </summary>
[IntegrationEventName("timeline.note.mentioned")]
public sealed record NoteMentioned(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid EntryId,
    string SubjectType,
    Guid SubjectId,
    IReadOnlyList<Guid> MentionedUserIds,
    string Excerpt,
    Guid AuthorUserId,
    string? AuthorName) : IntegrationEvent(TenantId, OccurredAt);
