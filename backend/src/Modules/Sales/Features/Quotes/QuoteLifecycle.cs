using System.Text.Json;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Email;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Sales;
using Akiron.Modules.Sales.Domain;
using Akiron.Modules.Sales.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Akiron.Modules.Sales.Features.Quotes;

/// <param name="Email">Send the link by e-mail to the recipient; otherwise only the link comes back (to share by hand).</param>
internal sealed record SendQuoteCommand(bool Email = true);

/// <param name="Link">The public link; shown once, since only its hash is stored.</param>
internal sealed record SendQuoteResponse(QuoteResponse Quote, string Link, bool Emailed);

/// <summary>Sending, revising and deleting quotes; the moves that change what the client sees.</summary>
internal sealed class QuoteLifecycleHandler(
    SalesDbContext db,
    QuoteReader reader,
    IEmailSender emailSender,
    IConfiguration configuration,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<SendQuoteResponse>> SendAsync(Guid id, SendQuoteCommand command, CancellationToken cancellationToken)
    {
        var quote = await FindAsync(id, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.QuoteNotFound;
        }

        if (quote.Status != QuoteStatus.Draft)
        {
            return SalesErrors.NotEditable;
        }

        var now = timeProvider.GetUtcNow();
        var secret = quote.Send(now);
        if (secret is null)
        {
            return SalesErrors.NoLines;
        }

        var response = reader.ToResponse(quote);
        db.QuoteSnapshots.Add(QuoteSnapshot.Of(quote, JsonSerializer.Serialize(response, JsonSerializerOptions.Web)));

        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        db.Publish(new QuoteSent(
            quote.TenantId, now, quote.Id.Value, quote.Number, quote.Revision, quote.Title, quote.PartyId, quote.PartyName,
            quote.GrandTotal, quote.Currency.Code, quote.RecipientEmail, userId.Value, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);

        var link = $"{configuration["App:WebUrl"]?.TrimEnd('/') ?? "http://localhost:3100"}/q/{secret}";
        var emailed = command.Email && quote.RecipientEmail is not null;
        if (emailed)
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    quote.RecipientEmail!,
                    $"Teklif {quote.Number}: {quote.Title}",
                    $"""
                    Merhaba{(quote.RecipientName is null ? string.Empty : " " + quote.RecipientName)},

                    {quote.Title} teklifimizi aşağıdaki bağlantıdan inceleyip onaylayabilirsiniz:
                    {link}

                    Geçerlilik: {quote.ValidUntil:dd.MM.yyyy}

                    ---
                    You can review and accept our quote "{quote.Title}" here: {link}
                    """),
                cancellationToken);
        }

        return new SendQuoteResponse(response, link, emailed);
    }

    public async Task<Result<QuoteResponse>> ReviseAsync(Guid id, CancellationToken cancellationToken)
    {
        var quote = await FindAsync(id, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.QuoteNotFound;
        }

        if (!quote.Revise())
        {
            return SalesErrors.NotRevisable;
        }

        await db.SaveChangesAsync(cancellationToken);
        return reader.ToResponse(quote);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var quote = await FindAsync(id, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.QuoteNotFound;
        }

        if (quote.Status != QuoteStatus.Draft || quote.Revision > 1)
        {
            return SalesErrors.OnlyDrafts;
        }

        db.Quotes.Remove(quote);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<Quote?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var quoteId = QuoteId.From(id);
        return db.Quotes.Include(candidate => candidate.Lines).FirstOrDefaultAsync(candidate => candidate.Id == quoteId, cancellationToken);
    }
}

internal sealed record QuoteFilter(
    [FromQuery(Name = "search")] string? Search = null,
    [FromQuery(Name = "status")] string? Status = null,
    [FromQuery(Name = "partyId")] Guid? PartyId = null);

internal sealed class QuoteFilterValidator : AbstractValidator<QuoteFilter>
{
    public static readonly string[] Statuses = ["draft", "sent", "accepted", "rejected", "expired"];

    public QuoteFilterValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(100);
        RuleFor(filter => filter.Status).Must(status => status is null || Statuses.Contains(status)).WithErrorCode("validation.invalid_value");
    }
}

internal sealed class ListQuotesHandler(SalesDbContext db, QuoteReader reader, TimeProvider timeProvider)
{
    public async Task<PagedResult<QuoteListItem>> HandleAsync(PageRequest page, QuoteFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Quotes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = "%" + filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(quote => EF.Functions.ILike(quote.Title, pattern, "\\")
                || EF.Functions.ILike(quote.Number, pattern, "\\")
                || EF.Functions.ILike(quote.PartyName, pattern, "\\"));
        }

        if (filter.PartyId is { } partyId)
        {
            query = query.Where(quote => quote.PartyId == partyId);
        }

        var today = SalesCalendar.Today(timeProvider);
        query = filter.Status switch
        {
            "draft" => query.Where(quote => quote.Status == QuoteStatus.Draft),
            "sent" => query.Where(quote => quote.Status == QuoteStatus.Sent && quote.ValidUntil >= today),
            "expired" => query.Where(quote => quote.Status == QuoteStatus.Sent && quote.ValidUntil < today),
            "accepted" => query.Where(quote => quote.Status == QuoteStatus.Accepted),
            "rejected" => query.Where(quote => quote.Status == QuoteStatus.Rejected),
            _ => query,
        };

        var result = await query.OrderByDescending(quote => quote.CreatedAt).ToPagedResultAsync(page, cancellationToken);
        return new PagedResult<QuoteListItem>(result.Items.Select(reader.ToListItem).ToList(), result.Page, result.PageSize, result.TotalCount);
    }
}

internal sealed class GetQuoteHandler(SalesDbContext db, QuoteReader reader)
{
    public async Task<Result<QuoteResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var quoteId = QuoteId.From(id);
        var quote = await db.Quotes.AsNoTracking().Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.Id == quoteId, cancellationToken);
        return quote is null ? SalesErrors.QuoteNotFound : reader.ToResponse(quote);
    }
}
