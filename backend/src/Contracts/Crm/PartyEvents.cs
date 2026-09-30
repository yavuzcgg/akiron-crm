using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Crm;

/// <summary>A counterparty (cari) was added. <paramref name="Kind"/> is <c>company</c> or <c>person</c>.</summary>
[IntegrationEventName("crm.party.created")]
public sealed record PartyCreated(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PartyId,
    string Code,
    string Name,
    string Kind,
    bool IsCustomer,
    bool IsSupplier,
    Guid CreatedByUserId,
    string? CreatedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A counterparty's details changed; <paramref name="ChangedFields"/> are camelCase field names.</summary>
[IntegrationEventName("crm.party.updated")]
public sealed record PartyUpdated(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PartyId,
    string Name,
    IReadOnlyList<string> ChangedFields,
    Guid UpdatedByUserId,
    string? UpdatedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A counterparty was archived; it disappears from lists but its history stays.</summary>
[IntegrationEventName("crm.party.archived")]
public sealed record PartyArchived(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PartyId,
    string Name,
    Guid ArchivedByUserId,
    string? ArchivedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A contact person was added to a counterparty.</summary>
[IntegrationEventName("crm.contact.added")]
public sealed record PartyContactAdded(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PartyId,
    Guid ContactId,
    string PartyName,
    string ContactName,
    Guid AddedByUserId,
    string? AddedByName) : IntegrationEvent(TenantId, OccurredAt);
