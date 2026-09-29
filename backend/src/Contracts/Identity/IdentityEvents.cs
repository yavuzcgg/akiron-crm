using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Identity;

/// <summary>A tenant was created by registration; <paramref name="OwnerUserId"/> is its first member.</summary>
[IntegrationEventName("identity.workspace.created")]
public sealed record WorkspaceCreated(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    string WorkspaceName,
    Guid OwnerUserId,
    string OwnerName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>Someone joined the tenant by accepting an invitation.</summary>
[IntegrationEventName("identity.member.joined")]
public sealed record MemberJoined(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    Guid? InvitedByUserId,
    string? InvitedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>An invitation was sent. Carries no token: the link only ever travels by e-mail.</summary>
[IntegrationEventName("identity.invitation.sent")]
public sealed record InvitationSent(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid InvitationId,
    string Email,
    string Role,
    Guid InvitedByUserId,
    string InvitedByName) : IntegrationEvent(TenantId, OccurredAt);
