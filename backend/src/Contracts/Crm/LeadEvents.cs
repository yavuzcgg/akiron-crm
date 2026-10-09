using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Crm;

/// <summary>
/// A lead entered the funnel. <paramref name="Source"/> is e.g. <c>referral</c> or <c>web_form</c>;
/// <paramref name="CreatedByUserId"/> is null when it came from the public web form.
/// </summary>
[IntegrationEventName("crm.lead.created")]
public sealed record LeadCreated(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid LeadId,
    string Title,
    string? CompanyName,
    string? ContactName,
    string Source,
    Guid? PartyId,
    Guid? OwnerUserId,
    Guid? CreatedByUserId,
    string? CreatedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>
/// A lead changed stage (<c>new</c>, <c>contacted</c>, <c>meeting</c>, <c>proposal</c>, <c>won</c>, <c>lost</c>).
/// <paramref name="MovedByUserId"/> is null when a quote moved it.
/// </summary>
[IntegrationEventName("crm.lead.moved")]
public sealed record LeadMoved(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid LeadId,
    string Title,
    Guid? PartyId,
    string FromStage,
    string ToStage,
    string? LostReason,
    Guid? MovedByUserId,
    string? MovedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A lead became (or was linked to) a party; its history continues on the party.</summary>
[IntegrationEventName("crm.lead.converted")]
public sealed record LeadConverted(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid LeadId,
    string Title,
    Guid PartyId,
    string PartyName,
    bool PartyCreated,
    Guid ConvertedByUserId,
    string? ConvertedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A lead's next step is due; its owner gets a reminder.</summary>
[IntegrationEventName("crm.lead.follow_up_due")]
public sealed record LeadFollowUpDue(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid LeadId,
    string Title,
    string? NextStep,
    DateOnly Date,
    Guid OwnerUserId) : IntegrationEvent(TenantId, OccurredAt);
