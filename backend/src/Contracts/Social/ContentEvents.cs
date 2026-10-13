using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Social;

/// <summary>A planned post as other modules name it: enough for a line, a link and a date.</summary>
public sealed record ContentRef(Guid PostId, string Title, DateOnly PlannedDate);

/// <summary>Someone asked for a post to be checked inside the agency before the client sees it.</summary>
[IntegrationEventName("social.content.review_requested")]
public sealed record ContentReviewRequested(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PostId,
    string Title,
    string PartyName,
    DateOnly PlannedDate,
    Guid RequestedByUserId,
    string? RequestedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>The internal reviewer sent a post back to its author with a note.</summary>
[IntegrationEventName("social.content.returned")]
public sealed record ContentReturned(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PostId,
    string Title,
    string Note,
    Guid? OwnerUserId,
    Guid ReturnedByUserId,
    string? ReturnedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>
/// One or more posts went to the client for approval in one go; the client gets a single e-mail
/// however many posts there are.
/// </summary>
[IntegrationEventName("social.content.sent_to_client")]
public sealed record ContentSentToClient(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PartyId,
    string PartyName,
    IReadOnlyList<ContentRef> Posts,
    Guid SentByUserId,
    string? SentByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>
/// A post was approved or sent back for changes: by the client in the portal
/// (<paramref name="ByClient"/>), or recorded by staff on the client's word.
/// </summary>
[IntegrationEventName("social.content.decided")]
public sealed record ContentDecided(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PostId,
    string Title,
    Guid PartyId,
    string PartyName,
    DateOnly PlannedDate,
    bool Approved,
    string? Note,
    string DecidedByName,
    bool ByClient,
    Guid? DecidedByUserId,
    Guid? OwnerUserId) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A post went live (marked by hand until publishing through the networks' APIs arrives).</summary>
[IntegrationEventName("social.content.published")]
public sealed record ContentPublished(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PostId,
    string Title,
    Guid PartyId,
    string? Url,
    Guid PublishedByUserId,
    string? PublishedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>An approved post is planned for today and somebody has to publish it.</summary>
[IntegrationEventName("social.content.publish_due")]
public sealed record ContentPublishDue(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PostId,
    string Title,
    string PartyName,
    DateOnly PlannedDate,
    Guid? OwnerUserId) : IntegrationEvent(TenantId, OccurredAt);
