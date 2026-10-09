using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Finance;

/// <summary>
/// A client was charged (a sale or an opening balance), payable in <paramref name="Instalments"/> parts.
/// <paramref name="RecordedByUserId"/> is null when the system charged a retainer period.
/// </summary>
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
    Guid? RecordedByUserId,
    string? RecordedByName,
    Guid? ContractId = null) : IntegrationEvent(TenantId, OccurredAt);

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

/// <summary>Once a day per tenant with money due: what falls due today, what is late, what comes this week.</summary>
[IntegrationEventName("finance.payments.digest")]
public sealed record PaymentsDueDigest(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    DateOnly Day,
    int DueTodayCount,
    int OverdueCount,
    int UpcomingCount,
    IReadOnlyList<DueAmount> Overdue,
    IReadOnlyList<DueAmount> DueSoon) : IntegrationEvent(TenantId, OccurredAt);

public sealed record DueAmount(string Currency, decimal Amount);

/// <summary>Someone e-mailed a client a reminder about an open item.</summary>
[IntegrationEventName("finance.reminder.sent")]
public sealed record PaymentReminderSent(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PartyId,
    string ChargeNumber,
    decimal Remaining,
    string Currency,
    DateOnly DueDate,
    string Email,
    Guid SentByUserId,
    string? SentByName) : IntegrationEvent(TenantId, OccurredAt);
