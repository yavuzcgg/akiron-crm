using Akiron.Contracts.Sales;
using Akiron.Modules.Sales.Domain;
using Akiron.Modules.Sales.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Sales.Features.Quotes;

internal sealed record QuoteLineResponse(
    Guid Id,
    Guid? CatalogItemId,
    string Name,
    string? Description,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal DiscountPercent,
    int VatRate,
    int WithholdingTenths,
    decimal Gross,
    decimal Discount,
    decimal Net,
    decimal Vat,
    decimal Withholding,
    decimal Total);

internal sealed record QuoteTotals(decimal Gross, decimal Discount, decimal Net, decimal Vat, decimal Withholding, decimal Grand, decimal NetTry);

/// <param name="Status">draft, sent, accepted, rejected; or expired for a sent quote past its validity.</param>
internal sealed record QuoteResponse(
    Guid Id,
    string Number,
    int Revision,
    string Status,
    Guid PartyId,
    string PartyName,
    string Title,
    string? RecipientName,
    string? RecipientEmail,
    string Currency,
    decimal? ExchangeRate,
    DateOnly? RateDate,
    DateOnly IssueDate,
    DateOnly ValidUntil,
    string? Notes,
    IReadOnlyList<QuoteLineResponse> Lines,
    QuoteTotals Totals,
    DateTimeOffset? SentAt,
    DateTimeOffset? ViewedAt,
    DateTimeOffset? DecidedAt,
    string? DecidedByName,
    string? DecisionNote,
    DateTimeOffset CreatedAt);

internal sealed record QuoteListItem(
    Guid Id,
    string Number,
    int Revision,
    string Status,
    string Title,
    Guid PartyId,
    string PartyName,
    string Currency,
    decimal GrandTotal,
    DateOnly IssueDate,
    DateOnly ValidUntil,
    DateTimeOffset? SentAt,
    DateTimeOffset? ViewedAt);

internal static class SalesCalendar
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>Today in Türkiye: a quote valid "until the 30th" is valid all of the 30th in Istanbul.</summary>
    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Istanbul).DateTime);
}

internal sealed class QuoteReader(TimeProvider timeProvider)
{
    public string StatusOf(Quote quote) =>
        quote.IsExpiredOn(SalesCalendar.Today(timeProvider)) ? "expired" : quote.Status.ToString().ToLowerInvariant();

    public QuoteResponse ToResponse(Quote quote) => new(
        quote.Id.Value,
        quote.Number,
        quote.Revision,
        StatusOf(quote),
        quote.PartyId,
        quote.PartyName,
        quote.Title,
        quote.RecipientName,
        quote.RecipientEmail,
        quote.Currency.Code,
        quote.ExchangeRate,
        quote.RateDate,
        quote.IssueDate,
        quote.ValidUntil,
        quote.Notes,
        quote.Lines.OrderBy(line => line.Position).Select(ToResponse).ToList(),
        Totals(quote),
        quote.SentAt,
        quote.ViewedAt,
        quote.DecidedAt,
        quote.DecidedByName,
        quote.DecisionNote,
        quote.CreatedAt);

    public QuoteListItem ToListItem(Quote quote) => new(
        quote.Id.Value,
        quote.Number,
        quote.Revision,
        StatusOf(quote),
        quote.Title,
        quote.PartyId,
        quote.PartyName,
        quote.Currency.Code,
        quote.GrandTotal,
        quote.IssueDate,
        quote.ValidUntil,
        quote.SentAt,
        quote.ViewedAt);

    public static QuoteTotals Totals(Quote quote) =>
        new(quote.GrossTotal, quote.DiscountTotal, quote.NetTotal, quote.VatTotal, quote.WithholdingTotal, quote.GrandTotal, quote.NetTotalTry);

    public static QuoteLineResponse ToResponse(QuoteLine line) => new(
        line.Id.Value,
        line.CatalogItemId,
        line.Name,
        line.Description,
        line.Quantity,
        line.Unit,
        line.UnitPrice,
        line.DiscountPercent,
        line.VatRate,
        line.WithholdingTenths,
        line.Gross,
        line.Discount,
        line.Net,
        line.Vat,
        line.Withholding,
        line.Total);
}

/// <summary>Implements <see cref="IAcceptedQuotes"/>: Jobs opens work orders from accepted quotes.</summary>
internal sealed class AcceptedQuotes(SalesDbContext db) : IAcceptedQuotes
{
    public async Task<AcceptedQuote?> FindAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var id = QuoteId.From(quoteId);
        var quote = await db.Quotes.AsNoTracking().Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.Status == QuoteStatus.Accepted, cancellationToken);

        return quote is null
            ? null
            : new AcceptedQuote(
                quote.Id.Value,
                quote.Number,
                quote.Title,
                quote.PartyId,
                quote.PartyName,
                quote.NetTotalTry,
                quote.Lines.OrderBy(line => line.Position).Select(line => line.Name).ToList());
    }
}
