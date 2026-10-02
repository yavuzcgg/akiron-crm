using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Sales;

/// <summary>A quote went out to the client with a public link (or a new revision of it did).</summary>
[IntegrationEventName("sales.quote.sent")]
public sealed record QuoteSent(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid QuoteId,
    string Number,
    int Revision,
    string Title,
    Guid PartyId,
    string PartyName,
    decimal GrandTotal,
    string Currency,
    string? RecipientEmail,
    Guid SentByUserId,
    string? SentByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>The client opened the link of this revision for the first time.</summary>
[IntegrationEventName("sales.quote.viewed")]
public sealed record QuoteViewed(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid QuoteId,
    string Number,
    string Title,
    Guid PartyId,
    string PartyName,
    Guid? OwnerUserId) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>The client accepted (<paramref name="Accepted"/>) or rejected the quote through its link.</summary>
[IntegrationEventName("sales.quote.decided")]
public sealed record QuoteDecided(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid QuoteId,
    string Number,
    string Title,
    Guid PartyId,
    string PartyName,
    bool Accepted,
    string DecidedByName,
    string? Note,
    decimal GrandTotal,
    string Currency,
    Guid? OwnerUserId) : IntegrationEvent(TenantId, OccurredAt);

public sealed record AcceptedQuote(Guid QuoteId, string Number, string Title, Guid PartyId, string PartyName, decimal NetTotalTry, IReadOnlyList<string> LineNames);

/// <summary>Read side of Sales for Jobs: an accepted quote a work order can be opened from.</summary>
public interface IAcceptedQuotes
{
    /// <summary>The quote when it exists in the current tenant and was accepted; null otherwise.</summary>
    Task<AcceptedQuote?> FindAsync(Guid quoteId, CancellationToken cancellationToken);
}
