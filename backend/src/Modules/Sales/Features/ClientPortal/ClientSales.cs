using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Sales;
using Akiron.Modules.Sales.Domain;
using Akiron.Modules.Sales.Features.Pdf;
using Akiron.Modules.Sales.Features.PublicQuotes;
using Akiron.Modules.Sales.Features.Quotes;
using Akiron.Modules.Sales.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Sales.Features.ClientPortal;

internal sealed record ClientQuoteRow(Guid Id, string Number, int Revision, string Status, string Title, string Currency, decimal GrandTotal, DateOnly IssueDate, DateOnly ValidUntil);

/// <summary>A retainer as its client sees it: what, how much a month, from when until when.</summary>
internal sealed record ClientContract(
    Guid Id,
    string Number,
    string Title,
    string? Scope,
    string Status,
    decimal Amount,
    string Currency,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool AutoRenew,
    DateOnly? NextBillingDate);

internal sealed record ClientDecisionCommand(string? Note = null);

internal sealed class ClientDecisionValidator : AbstractValidator<ClientDecisionCommand>
{
    public ClientDecisionValidator() => RuleFor(command => command.Note).MaximumLength(2000);
}

/// <summary>
/// The signed-in client's quotes and contracts. Drafts never show: a quote exists for the client
/// from the moment it was sent. Answering here is the same decision as through the quote's link,
/// signed with the portal user's name.
/// </summary>
internal sealed class ClientSalesHandler(
    SalesDbContext db,
    IPortalVisitor visitor,
    IWorkspaceDirectory workspace,
    PublicQuoteHandler publicQuotes,
    QuoteReader reader,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ClientQuoteRow>> QuotesAsync(CancellationToken cancellationToken)
    {
        var partyId = visitor.PartyId;
        var quotes = await db.Quotes.AsNoTracking()
            .Where(quote => quote.PartyId == partyId && quote.Status != QuoteStatus.Draft)
            .OrderByDescending(quote => quote.SentAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return quotes
            .Select(quote => new ClientQuoteRow(
                quote.Id.Value, quote.Number, quote.Revision, reader.StatusOf(quote), quote.Title, quote.Currency.Code, quote.GrandTotal, quote.IssueDate, quote.ValidUntil))
            .ToList();
    }

    public async Task<Result<PublicQuoteResponse>> QuoteAsync(Guid id, CancellationToken cancellationToken)
    {
        var quote = await FindAsync(id, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.QuoteNotFound;
        }

        var now = timeProvider.GetUtcNow();
        if (quote.MarkViewed(now))
        {
            db.Publish(new QuoteViewed(quote.TenantId, now, quote.Id.Value, quote.Number, quote.Title, quote.PartyId, quote.PartyName, quote.CreatedBy?.Value));
            await db.SaveChangesAsync(cancellationToken);
        }

        return await publicQuotes.ToResponseAsync(quote, cancellationToken);
    }

    public async Task<Result<(byte[] Content, string FileName)>> PdfAsync(Guid id, string language, CancellationToken cancellationToken)
    {
        var quote = await FindAsync(id, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.QuoteNotFound;
        }

        return (QuotePdf.Render(quote, await workspace.CurrentNameAsync(cancellationToken), language), $"{quote.Number}.pdf");
    }

    public async Task<Result<PublicQuoteResponse>> DecideAsync(Guid id, bool accept, ClientDecisionCommand command, CancellationToken cancellationToken)
    {
        var quote = await FindAsync(id, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.QuoteNotFound;
        }

        var today = SalesCalendar.Today(timeProvider);
        if (quote.IsExpiredOn(today))
        {
            return SalesErrors.Expired;
        }

        var now = timeProvider.GetUtcNow();
        if (!quote.Decide(accept, visitor.Name, command.Note, today, now))
        {
            return SalesErrors.AlreadyDecided;
        }

        db.Publish(new QuoteDecided(
            quote.TenantId, now, quote.Id.Value, quote.Number, quote.Title, quote.PartyId, quote.PartyName, accept,
            quote.DecidedByName!, quote.DecisionNote, quote.GrandTotal, quote.Currency.Code, quote.CreatedBy?.Value));
        await db.SaveChangesAsync(cancellationToken);
        return await publicQuotes.ToResponseAsync(quote, cancellationToken);
    }

    public async Task<IReadOnlyList<ClientContract>> ContractsAsync(CancellationToken cancellationToken)
    {
        var partyId = visitor.PartyId;
        var contracts = await db.Contracts.AsNoTracking()
            .Where(contract => contract.PartyId == partyId)
            .OrderBy(contract => contract.Status).ThenByDescending(contract => contract.StartDate)
            .Take(100)
            .ToListAsync(cancellationToken);
        return contracts
            .Select(contract => new ClientContract(
                contract.Id.Value, contract.Number, contract.Title, contract.Scope, contract.Status.ToString().ToLowerInvariant(), contract.Amount,
                contract.Currency.Code, contract.StartDate, contract.EndDate, contract.AutoRenew, contract.NextBillingDate))
            .ToList();
    }

    /// <summary>A sent (or answered) quote of the visitor's party; anything else does not exist for them.</summary>
    private Task<Quote?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var quoteId = QuoteId.From(id);
        var partyId = visitor.PartyId;
        return db.Quotes.Include(quote => quote.Lines)
            .FirstOrDefaultAsync(quote => quote.Id == quoteId && quote.PartyId == partyId && quote.Status != QuoteStatus.Draft, cancellationToken);
    }
}

internal static class ClientSalesEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var portal = endpoints.MapGroup("/portal");

        portal.MapGet("/quotes", async (ClientSalesHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.QuotesAsync(cancellationToken)))
            .RequirePortal()
            .Produces<IReadOnlyList<ClientQuoteRow>>()
            .WithSummary("Portal: quotes sent to the client, newest first");

        portal.MapGet("/quotes/{id:guid}", async (Guid id, ClientSalesHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.QuoteAsync(id, cancellationToken)))
            .RequirePortal()
            .Produces<PublicQuoteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Portal: one quote as its recipient sees it");

        portal.MapGet("/quotes/{id:guid}/pdf", async (Guid id, string? lang, ClientSalesHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.PdfAsync(id, lang == "en" ? "en" : "tr", cancellationToken);
                return result.IsSuccess ? Results.File(result.Value.Content, "application/pdf", result.Value.FileName) : result.Error.ToProblem();
            })
            .RequirePortal()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Portal: the quote's PDF");

        portal.MapPost("/quotes/{id:guid}/accept", async (Guid id, ClientDecisionCommand command, ClientSalesHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.DecideAsync(id, accept: true, command, cancellationToken)))
            .RequirePortal()
            .Validate<ClientDecisionCommand>()
            .Produces<PublicQuoteResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Portal: accept the quote");

        portal.MapPost("/quotes/{id:guid}/reject", async (Guid id, ClientDecisionCommand command, ClientSalesHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.DecideAsync(id, accept: false, command, cancellationToken)))
            .RequirePortal()
            .Validate<ClientDecisionCommand>()
            .Produces<PublicQuoteResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Portal: decline the quote, optionally saying why");

        portal.MapGet("/contracts", async (ClientSalesHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ContractsAsync(cancellationToken)))
            .RequirePortal()
            .Produces<IReadOnlyList<ClientContract>>()
            .WithSummary("Portal: the client's contracts and retainers");
    }

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
