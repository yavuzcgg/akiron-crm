using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Social;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>
/// A post's approval history on its own stream, where the client reads it too. A month sent in one
/// go is one line on the party's stream, not thirty.
/// </summary>
internal sealed class ContentSentToClientProjection(TimelineWriter writer) : IIntegrationEventConsumer<ContentSentToClient>
{
    public async Task HandleAsync(ContentSentToClient integrationEvent, CancellationToken cancellationToken)
    {
        var actor = TimelineActor.User(integrationEvent.SentByUserId, integrationEvent.SentByName);
        foreach (var post in integrationEvent.Posts)
        {
            await writer.WriteAsync(
                TimelineEntry.Create(
                    integrationEvent.TenantId,
                    TimelineEntryTypes.ContentSent,
                    integrationEvent.OccurredAt,
                    actor,
                    Payload.Of(new { postId = post.PostId, title = post.Title, plannedDate = post.PlannedDate }),
                    $"{integrationEvent.EventId}:{post.PostId}",
                    [new TimelineSubject(TimelineSubjects.ContentPost, post.PostId)],
                    TimelineVisibility.Portal),
                cancellationToken);
        }

        await writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ContentBatchSent,
                integrationEvent.OccurredAt,
                actor,
                Payload.Of(new { count = integrationEvent.Posts.Count, partyName = integrationEvent.PartyName, partyId = integrationEvent.PartyId }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId)]),
            cancellationToken);
    }
}

internal sealed class ContentDecidedProjection(TimelineWriter writer) : IIntegrationEventConsumer<ContentDecided>
{
    public Task HandleAsync(ContentDecided integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ContentDecided,
                integrationEvent.OccurredAt,
                integrationEvent.ByClient || integrationEvent.DecidedByUserId is null
                    ? TimelineActor.ClientContact(integrationEvent.DecidedByName)
                    : TimelineActor.User(integrationEvent.DecidedByUserId.Value, integrationEvent.DecidedByName),
                Payload.Of(new
                {
                    postId = integrationEvent.PostId,
                    title = integrationEvent.Title,
                    approved = integrationEvent.Approved,
                    note = integrationEvent.Note,
                    byClient = integrationEvent.ByClient,
                    partyId = integrationEvent.PartyId,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.ContentPost, integrationEvent.PostId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ],
                TimelineVisibility.Portal),
            cancellationToken);
}

internal sealed class ContentPublishedProjection(TimelineWriter writer) : IIntegrationEventConsumer<ContentPublished>
{
    public Task HandleAsync(ContentPublished integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ContentPublished,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.PublishedByUserId, integrationEvent.PublishedByName),
                Payload.Of(new { postId = integrationEvent.PostId, title = integrationEvent.Title, url = integrationEvent.Url, partyId = integrationEvent.PartyId }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.ContentPost, integrationEvent.PostId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ],
                TimelineVisibility.Portal),
            cancellationToken);
}
