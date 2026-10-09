using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Finance;

/// <summary>A client was charged (a sale or an opening balance), payable in <paramref name="Instalments"/> parts.</summary>
[IntegrationEventName("finance.charge.recorded")]
public sealed record ChargeRecorded(
    TenantId TenantId,
    DateOnly Date,
    DateTimeOffset OccurredAt,
    Guid ChargeId,
    string Number,
    Guid PartyId,
    string PartyName,
    string Description,
    decimal Amount,
    string Currency,
    int Instalments,
    Guid? QuoteId,
    Guid RecordedByUserId,
    string? RecordedByName) : IntegrationEvent(TenantId, OccurredAt);

[IntegrationEventName("finance.charge.cancelled")]
public sealed record ChargeCancelled(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid ChargeId,
    string Number,
    Guid PartyId,
    decimal Amount,
    string Currency,
    string Reason,
    Guid CancelledByUserId,
    string? CancelledByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>Money came in from a client; <paramref name="Unallocated"/> stays on account as an advance.</summary>
[IntegrationEventName("finance.receipt.recorded")]
public sealed record ReceiptRecorded(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid ReceiptId,
    string Number,
    Guid PartyId,
    string PartyName,
    decimal Amount,
    decimal Unallocated,
    string Currency,
    string Method,
    Guid RecordedByUserId,
    string? RecordedByName) : IntegrationEvent(TenantId, OccurredAt);

[IntegrationEventName("finance.receipt.reversed")]
public sealed record ReceiptReversed(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid ReceiptId,
    string Number,
    Guid PartyId,
    decimal Amount,
    string Currency,
    string Reason,
    Guid ReversedByUserId,
    string? ReversedByName) : IntegrationEvent(TenantId, OccurredAt);
