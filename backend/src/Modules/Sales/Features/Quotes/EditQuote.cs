using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Reference;
using Akiron.Modules.Sales.Domain;
using Akiron.Modules.Sales.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Sales.Features.Quotes;

/// <param name="UnitPrice">As typed; <paramref name="PriceIncludesVat"/> says whether VAT is inside it (the line stores the net price).</param>
internal sealed record QuoteLineCommand(
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    string Unit = Units.Piece,
    Guid? CatalogItemId = null,
    string? Description = null,
    decimal DiscountPercent = 0,
    int VatRate = 20,
    int WithholdingTenths = 0,
    bool PriceIncludesVat = false);

/// <param name="ExchangeRate">Leave empty to take the TCMB döviz alış rate of the business day before <paramref name="IssueDate"/>.</param>
internal sealed record QuoteCommand(
    Guid PartyId,
    string Title,
    IReadOnlyList<QuoteLineCommand> Lines,
    string Currency = "TRY",
    decimal? ExchangeRate = null,
    DateOnly? IssueDate = null,
    DateOnly? ValidUntil = null,
    string? RecipientName = null,
    string? RecipientEmail = null,
    string? Notes = null);

internal sealed class QuoteValidator : AbstractValidator<QuoteCommand>
{
    public QuoteValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Quote.TitleMaxLength);
        RuleFor(command => command.Lines.Count).LessThanOrEqualTo(Quote.MaxLines).OverridePropertyName(nameof(QuoteCommand.Lines));
        RuleFor(command => command.Currency).Must(Currency.IsSupported).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.ExchangeRate).GreaterThan(0m);
        RuleFor(command => command.ValidUntil).GreaterThanOrEqualTo(command => command.IssueDate).When(command => command.IssueDate is not null);
        RuleFor(command => command.RecipientName).MaximumLength(200);
        RuleFor(command => command.RecipientEmail!).EmailAddress().MaximumLength(254).When(command => !string.IsNullOrWhiteSpace(command.RecipientEmail));
        RuleFor(command => command.Notes).MaximumLength(Quote.NotesMaxLength);
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(item => item.Name).NotEmpty().MaximumLength(QuoteLine.NameMaxLength);
            line.RuleFor(item => item.Description).MaximumLength(2000);
            line.RuleFor(item => item.Quantity).GreaterThan(0m).LessThanOrEqualTo(1_000_000m);
            line.RuleFor(item => item.UnitPrice).InclusiveBetween(0m, 1_000_000_000m);
            line.RuleFor(item => item.Unit).Must(unit => Units.All.Contains(unit)).WithErrorCode("validation.invalid_value");
            line.RuleFor(item => item.DiscountPercent).InclusiveBetween(0m, 100m);
            line.RuleFor(item => item.VatRate).Must(rate => VatRates.All.Contains(rate)).WithErrorCode("validation.invalid_value");
            line.RuleFor(item => item.WithholdingTenths).InclusiveBetween(0, 10);
        });
    }
}

/// <summary>Turns a command into quote details: the client's name, dates, and the rate snapshot for foreign currencies.</summary>
internal sealed class QuoteDetailsBuilder(IPartyDirectory parties, IExchangeRates exchangeRates, TimeProvider timeProvider)
{
    public const int DefaultValidityDays = 30;

    public async Task<Result<QuoteDetails>> BuildAsync(QuoteCommand command, CancellationToken cancellationToken)
    {
        var party = await parties.FindAsync(command.PartyId, cancellationToken);
        if (party is null)
        {
            return SalesErrors.PartyNotFound;
        }

        var issueDate = command.IssueDate ?? SalesCalendar.Today(timeProvider);
        var currency = Currency.From(command.Currency);
        decimal? rate = null;
        DateOnly? rateDate = null;
        if (currency != Currency.Try)
        {
            if (command.ExchangeRate is { } typed)
            {
                (rate, rateDate) = (decimal.Round(typed, 6), issueDate);
            }
            else
            {
                // Turkish practice: the rate of the business day before the document date.
                var quote = await exchangeRates.FindAsync(currency, issueDate.AddDays(-1), cancellationToken);
                if (quote is null)
                {
                    return SalesErrors.RateMissing;
                }

                (rate, rateDate) = (quote.ForexBuying, quote.BulletinDate);
            }
        }

        var lines = command.Lines.Select(line => new QuoteLineInput(
                line.CatalogItemId,
                line.Name,
                line.Description,
                line.Quantity,
                line.Unit,
                line.PriceIncludesVat ? QuoteMath.WithoutVat(line.UnitPrice, line.VatRate) : line.UnitPrice,
                line.DiscountPercent,
                line.VatRate,
                line.WithholdingTenths))
            .ToList();

        return new QuoteDetails(
            party.PartyId,
            party.Name,
            command.Title,
            command.RecipientName,
            command.RecipientEmail,
            currency,
            rate,
            rateDate,
            issueDate,
            command.ValidUntil ?? issueDate.AddDays(DefaultValidityDays),
            command.Notes,
            lines);
    }
}

internal sealed class CreateQuoteHandler(SalesDbContext db, QuoteDetailsBuilder builder, QuoteReader reader, TimeProvider timeProvider)
{
    public const string NumberSeries = "TKL";

    public async Task<Result<QuoteResponse>> HandleAsync(QuoteCommand command, CancellationToken cancellationToken)
    {
        var details = await builder.BuildAsync(command, cancellationToken);
        if (!details.IsSuccess)
        {
            return details.Error;
        }

        var year = timeProvider.GetUtcNow().Year;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var sequence = await db.NextDocumentSequenceAsync(NumberSeries, year, cancellationToken);
        var quote = Quote.Draft(DocumentNumber.Format(NumberSeries, year, sequence), details.Value);
        db.Quotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return reader.ToResponse(quote);
    }
}

internal sealed class UpdateQuoteHandler(SalesDbContext db, QuoteDetailsBuilder builder, QuoteReader reader)
{
    public async Task<Result<QuoteResponse>> HandleAsync(Guid id, QuoteCommand command, CancellationToken cancellationToken)
    {
        var quoteId = QuoteId.From(id);
        var quote = await db.Quotes.Include(candidate => candidate.Lines).FirstOrDefaultAsync(candidate => candidate.Id == quoteId, cancellationToken);
        if (quote is null)
        {
            return SalesErrors.QuoteNotFound;
        }

        if (quote.Status != QuoteStatus.Draft)
        {
            return SalesErrors.NotEditable;
        }

        var details = await builder.BuildAsync(command, cancellationToken);
        if (!details.IsSuccess)
        {
            return details.Error;
        }

        // Lines are replaced wholesale: remove the old rows explicitly so EF deletes them.
        db.QuoteLines.RemoveRange(quote.Lines);
        quote.Edit(details.Value);
        db.QuoteLines.AddRange(quote.Lines);
        await db.SaveChangesAsync(cancellationToken);
        return reader.ToResponse(quote);
    }
}
