using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Sales;

/// <summary>A contract or retainer started: <paramref name="Amount"/> billed every month from <paramref name="StartDate"/>.</summary>
[IntegrationEventName("sales.contract.started")]
public sealed record ContractStarted(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid ContractId,
    string Number,
    string Title,
    Guid PartyId,
    string PartyName,
    decimal Amount,
    string Currency,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid? QuoteId,
    Guid CreatedByUserId,
    string? CreatedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>
/// One month of a retainer is due: Finance records the charge. Keyed by contract and period, so a
/// redelivery records nothing new.
/// </summary>
[IntegrationEventName("sales.contract.period_due")]
public sealed record RetainerPeriodDue(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid ContractId,
    string Number,
    string Title,
    Guid PartyId,
    string PartyName,
    DateOnly PeriodStart,
    DateOnly DueDate,
    decimal Amount,
    string Currency,
    decimal? ExchangeRate) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A contract's end date is near (its notice period began); <paramref name="AutoRenew"/> says what happens then.</summary>
[IntegrationEventName("sales.contract.ending")]
public sealed record ContractEnding(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid ContractId,
    string Number,
    string Title,
    string PartyName,
    DateOnly EndDate,
    bool AutoRenew,
    Guid? OwnerUserId) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>An auto-renewing contract reached its end date and now runs until <paramref name="NewEndDate"/>.</summary>
[IntegrationEventName("sales.contract.renewed")]
public sealed record ContractRenewed(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid ContractId,
    string Number,
    string Title,
    Guid PartyId,
    DateOnly NewEndDate) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>
/// A contract stopped billing: ended by someone (<paramref name="EndedByUserId"/> set, with a reason) or
/// reached its end date without auto-renewal.
/// </summary>
[IntegrationEventName("sales.contract.ended")]
public sealed record ContractEnded(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid ContractId,
    string Number,
    string Title,
    Guid PartyId,
    DateOnly EndDate,
    string? Reason,
    Guid? EndedByUserId,
    string? EndedByName) : IntegrationEvent(TenantId, OccurredAt);
