using System.Text.Json;
using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Social;
using Akiron.Modules.Notifications.Domain;

namespace Akiron.Modules.Notifications.Features;

/// <summary>
/// The content calendar's nudges: reviewers hear that a post waits for them, authors hear what the
/// reviewer or the client said, and on the planned day the owner is reminded to publish.
/// </summary>
internal sealed class ContentNotifications(NotificationSender sender, IMemberDirectory members)
    : IIntegrationEventConsumer<ContentReviewRequested>,
        IIntegrationEventConsumer<ContentReturned>,
        IIntegrationEventConsumer<ContentDecided>,
        IIntegrationEventConsumer<ContentPublishDue>
{
    public const string ReviewRequested = "social.content.review_requested";
    public const string Returned = "social.content.returned";
    public const string Decided = "social.content.decided";
    public const string PublishDue = "social.content.publish_due";

    private const string Approve = "social.content.approve";

    public async Task HandleAsync(ContentReviewRequested integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                postId = integrationEvent.PostId,
                title = integrationEvent.Title,
                partyName = integrationEvent.PartyName,
                plannedDate = integrationEvent.PlannedDate,
                requestedByName = integrationEvent.RequestedByName,
            },
            JsonSerializerOptions.Web);
        foreach (var reviewer in await members.WithPermissionAsync(Approve, cancellationToken))
        {
            if (reviewer.UserId != integrationEvent.RequestedByUserId)
            {
                await sender.SendAsync(Notification.Create(integrationEvent.TenantId, reviewer.UserId, ReviewRequested, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
            }
        }
    }

    public Task HandleAsync(ContentReturned integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.OwnerUserId is not { } owner || owner == integrationEvent.ReturnedByUserId)
        {
            return Task.CompletedTask;
        }

        var payload = JsonSerializer.Serialize(
            new { postId = integrationEvent.PostId, title = integrationEvent.Title, note = integrationEvent.Note, returnedByName = integrationEvent.ReturnedByName },
            JsonSerializerOptions.Web);
        return sender.SendAsync(Notification.Create(integrationEvent.TenantId, owner, Returned, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
    }

    public async Task HandleAsync(ContentDecided integrationEvent, CancellationToken cancellationToken)
    {
        // Staff recording an approval know they did; only the client's own answers are news.
        if (!integrationEvent.ByClient)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                postId = integrationEvent.PostId,
                title = integrationEvent.Title,
                partyName = integrationEvent.PartyName,
                approved = integrationEvent.Approved,
                note = integrationEvent.Note,
                clientName = integrationEvent.DecidedByName,
            },
            JsonSerializerOptions.Web);
        foreach (var userId in await OwnerOrAsync(integrationEvent.OwnerUserId, "social.content.write", cancellationToken))
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, userId, Decided, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }

    public async Task HandleAsync(ContentPublishDue integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new { postId = integrationEvent.PostId, title = integrationEvent.Title, partyName = integrationEvent.PartyName, plannedDate = integrationEvent.PlannedDate },
            JsonSerializerOptions.Web);
        foreach (var userId in await OwnerOrAsync(integrationEvent.OwnerUserId, Approve, cancellationToken))
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, userId, PublishDue, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }

    /// <summary>The post's owner while they are still on the team; otherwise everyone holding <paramref name="permission"/>.</summary>
    private async Task<IReadOnlyCollection<Guid>> OwnerOrAsync(Guid? owner, string permission, CancellationToken cancellationToken)
    {
        if (owner is { } ownerId && (await members.FindAsync([ownerId], cancellationToken)).ContainsKey(ownerId))
        {
            return [ownerId];
        }

        return (await members.WithPermissionAsync(permission, cancellationToken)).Select(member => member.UserId).ToList();
    }
}
