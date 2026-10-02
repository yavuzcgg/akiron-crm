using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Sales;
using Akiron.Modules.Sales.Domain;
using Akiron.Modules.Sales.Features.Pdf;
using Akiron.Modules.Sales.Features.Quotes;
using Akiron.Modules.Sales.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Sales.Features.PublicQuotes;

internal sealed record PublicQuoteLine(string Name, string? Description, decimal Quantity, string Unit, decimal UnitPrice, decimal DiscountPercent, int VatRate, decimal Net);

/// <summary>What the client sees through the link: the document, nothing internal (no notes of the team, no ids).</summary>
internal sealed record PublicQuoteResponse(
    string WorkspaceName,
    string Number,
    int Revision,
    string Status,
    string Title,
    string PartyName,
    string? RecipientName,
    string Currency,
    decimal? ExchangeRate,
    DateOnly IssueDate,
    DateOnly ValidUntil,
    string? Notes,
    IReadOnlyList<PublicQuoteLine> Lines,
    QuoteTotals Totals,
    DateTimeOffset? DecidedAt,
    string? DecidedByName);

internal sealed record DecideQuoteCommand(string Name, string? Note = null);

internal sealed class DecideQuoteValidator : AbstractValidator<DecideQuoteCommand>
{
    public DecideQuoteValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Note).MaximumLength(2000);
    }
}

/// <summary>
/// The public side of a quote, reached with the link's secret and no account. The secret names
/// the tenant: the quote is found by its hash across tenants, then the scope is bound to that
/// tenant before anything else is read or written (ADR-0002).
/// </summary>
internal sealed class PublicQuoteHandler(
    SalesDbContext db,
    ITenantContext tenantContext,
    IWorkspaceDirectory workspace,
    QuoteReader reader,
    TimeProvider timeProvider)
{
    public async Task<Result<PublicQuoteResponse>> ViewAsync(string token, CancellationToken cancellationToken)
    {
        var quote = await ResolveAsync(token, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.LinkInvalid;
        }

        var now = timeProvider.GetUtcNow();
        if (quote.MarkViewed(now))
        {
            db.Publish(new QuoteViewed(quote.TenantId, now, quote.Id.Value, quote.Number, quote.Title, quote.PartyId, quote.PartyName, quote.CreatedBy?.Value));
            await db.SaveChangesAsync(cancellationToken);
        }

        return await ToResponseAsync(quote, cancellationToken);
    }

    public async Task<Result<PublicQuoteResponse>> DecideAsync(string token, bool accept, DecideQuoteCommand command, CancellationToken cancellationToken)
    {
        var quote = await ResolveAsync(token, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.LinkInvalid;
        }

        var today = SalesCalendar.Today(timeProvider);
        if (quote.IsExpiredOn(today))
        {
            return SalesErrors.Expired;
        }

        var now = timeProvider.GetUtcNow();
        if (!quote.Decide(accept, command.Name, command.Note, today, now))
        {
            return SalesErrors.AlreadyDecided;
        }

        db.Publish(new QuoteDecided(
            quote.TenantId, now, quote.Id.Value, quote.Number, quote.Title, quote.PartyId, quote.PartyName, accept,
            quote.DecidedByName!, quote.DecisionNote, quote.GrandTotal, quote.Currency.Code, quote.CreatedBy?.Value));
        await db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(quote, cancellationToken);
    }

    public async Task<Result<(byte[] Content, string FileName)>> PdfAsync(string token, string language, CancellationToken cancellationToken)
    {
        var quote = await ResolveAsync(token, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.LinkInvalid;
        }

        return (QuotePdf.Render(quote, await workspace.CurrentNameAsync(cancellationToken), language), $"{quote.Number}.pdf");
    }

    private async Task<Quote?> ResolveAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            return null;
        }

        var hash = Quote.HashToken(token);

        // No tenant is bound yet: the link is the only key, so this one lookup crosses tenants.
        var quote = await db.Quotes.IgnoreQueryFilters().Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.LinkTokenHash == hash && !candidate.IsDeleted, cancellationToken);
        if (quote is not null)
        {
            tenantContext.Bind(quote.TenantId);
        }

        return quote;
    }

    private async Task<PublicQuoteResponse> ToResponseAsync(Quote quote, CancellationToken cancellationToken) => new(
        await workspace.CurrentNameAsync(cancellationToken),
        quote.Number,
        quote.Revision,
        reader.StatusOf(quote),
        quote.Title,
        quote.PartyName,
        quote.RecipientName,
        quote.Currency.Code,
        quote.ExchangeRate,
        quote.IssueDate,
        quote.ValidUntil,
        quote.Notes,
        quote.Lines.OrderBy(line => line.Position)
            .Select(line => new PublicQuoteLine(line.Name, line.Description, line.Quantity, line.Unit, line.UnitPrice, line.DiscountPercent, line.VatRate, line.Net))
            .ToList(),
        QuoteReader.Totals(quote),
        quote.DecidedAt,
        quote.DecidedByName);
}

/// <summary>The staff side of the PDF: any quote of the tenant, draft or sent.</summary>
internal sealed class QuotePdfHandler(SalesDbContext db, IWorkspaceDirectory workspace)
{
    public async Task<Result<(byte[] Content, string FileName)>> HandleAsync(Guid id, string language, CancellationToken cancellationToken)
    {
        var quoteId = QuoteId.From(id);
        var quote = await db.Quotes.AsNoTracking().Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.Id == quoteId, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.QuoteNotFound;
        }

        return (QuotePdf.Render(quote, await workspace.CurrentNameAsync(cancellationToken), language), $"{quote.Number}.pdf");
    }
}
