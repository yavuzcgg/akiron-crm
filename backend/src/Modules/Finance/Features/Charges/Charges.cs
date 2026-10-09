using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Finance;
using Akiron.Contracts.Sales;
using Akiron.Modules.Finance.Domain;
using Akiron.Modules.Finance.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Finance.Features.Charges;

internal sealed record InstalmentCommand(DateOnly DueDate, decimal Amount);

/// <param name="QuoteId">Charge an accepted quote: its client, currency, rate and total are used.</param>
/// <param name="Instalments">The payment plan; empty means one instalment due on <paramref name="DueDate"/> (or the charge date).</param>
internal sealed record RecordChargeCommand(
    Guid? PartyId = null,
    string? Description = null,
    decimal? Amount = null,
    string Currency = "TRY",
    decimal? ExchangeRate = null,
    DateOnly? Date = null,
    DateOnly? DueDate = null,
    IReadOnlyList<InstalmentCommand>? Instalments = null,
    Guid? QuoteId = null,
    string Kind = "sale",
    string? InvoiceNumber = null);

internal sealed class RecordChargeValidator : AbstractValidator<RecordChargeCommand>
{
    public RecordChargeValidator()
    {
        RuleFor(command => command.PartyId).NotEmpty().When(command => command.QuoteId is null);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(Charge.DescriptionMaxLength).When(command => command.QuoteId is null);
        RuleFor(command => command.Amount).GreaterThan(0m).LessThanOrEqualTo(1_000_000_000m);
        RuleFor(command => command.Amount).NotEmpty()
            .When(command => command.QuoteId is null && (command.Instalments is null || command.Instalments.Count == 0));
        RuleFor(command => command.Currency).Must(Currency.IsSupported).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.ExchangeRate).GreaterThan(0m);
        RuleFor(command => command.Kind).Must(kind => kind is "sale" or "opening").WithErrorCode("validation.invalid_value");
        RuleFor(command => command.InvoiceNumber).MaximumLength(50);
        RuleFor(command => command.Instalments!.Count).LessThanOrEqualTo(Charge.MaxInstalments).When(command => command.Instalments is not null)
            .OverridePropertyName(nameof(RecordChargeCommand.Instalments));
        RuleForEach(command => command.Instalments).ChildRules(instalment =>
            instalment.RuleFor(item => item.Amount).GreaterThan(0m));
    }
}

internal sealed record MarkInvoicedCommand(string InvoiceNumber, DateOnly InvoiceDate);

internal sealed class MarkInvoicedValidator : AbstractValidator<MarkInvoicedCommand>
{
    public MarkInvoicedValidator() => RuleFor(command => command.InvoiceNumber).NotEmpty().MaximumLength(50);
}

internal sealed record CancelCommand(string Reason);

internal sealed class CancelValidator : AbstractValidator<CancelCommand>
{
    public CancelValidator() => RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
}

internal sealed record OpenItemResponse(Guid Id, int Sequence, DateOnly DueDate, decimal Amount, decimal Allocated, decimal Remaining, bool IsOpen, bool IsOverdue);

internal sealed record ChargeResponse(
    Guid Id,
    string Number,
    string Kind,
    string Status,
    Guid PartyId,
    string PartyName,
    DateOnly Date,
    string Description,
    decimal Amount,
    decimal Collected,
    string Currency,
    decimal ExchangeRate,
    Guid? QuoteId,
    Guid? ContractId,
    DateOnly? Period,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    IReadOnlyList<OpenItemResponse> Instalments);

internal static class ChargeViews
{
    public static ChargeResponse ToResponse(Charge charge, DateOnly today) => new(
        charge.Id.Value,
        charge.Number,
        charge.Kind.ToString().ToLowerInvariant(),
        charge.Status.ToString().ToLowerInvariant(),
        charge.PartyId,
        charge.PartyName,
        charge.Date,
        charge.Description,
        charge.Amount,
        charge.Items.Sum(item => item.Allocated),
        charge.Currency.Code,
        charge.ExchangeRate,
        charge.QuoteId,
        charge.ContractId,
        charge.Period,
        charge.InvoiceNumber,
        charge.InvoiceDate,
        charge.Items.OrderBy(item => item.Sequence)
            .Select(item => new OpenItemResponse(item.Id.Value, item.Sequence, item.DueDate, item.Amount, item.Allocated, item.Remaining, item.IsOpen, item.IsOpen && item.DueDate < today))
            .ToList());
}

/// <summary>Records what a client owes, with its payment plan, and posts the debit to the party ledger.</summary>
internal sealed class RecordChargeHandler(FinanceDbContext db, FinanceContext context, IAcceptedQuotes quotes, ITenantContext tenantContext)
{
    public const string NumberSeries = "BRC";

    public async Task<Result<ChargeResponse>> HandleAsync(RecordChargeCommand command, CancellationToken cancellationToken)
    {
        var date = command.Date ?? context.Today;
        Guid partyId;
        string partyName;
        string description;
        Currency currency;
        decimal? rate;
        decimal? amount = command.Amount;

        if (command.QuoteId is { } quoteId)
        {
            var quote = await quotes.FindAsync(quoteId, cancellationToken);
            if (quote is null)
            {
                return FinanceErrors.QuoteNotAccepted;
            }

            if (await db.Charges.AnyAsync(charge => charge.QuoteId == quoteId && charge.Status == ChargeStatus.Active, cancellationToken))
            {
                return FinanceErrors.QuoteAlreadyCharged;
            }

            (partyId, partyName) = (quote.PartyId, quote.PartyName);
            description = string.IsNullOrWhiteSpace(command.Description) ? $"{quote.Number} {quote.Title}" : command.Description;
            currency = Currency.From(quote.Currency);
            rate = quote.ExchangeRate ?? 1m;
            amount ??= quote.GrandTotal;
        }
        else
        {
            var party = await context.PartyAsync(command.PartyId!.Value, cancellationToken);
            if (party is null)
            {
                return FinanceErrors.PartyNotFound;
            }

            (partyId, partyName, description) = (party.PartyId, party.Name, command.Description!);
            currency = Currency.From(command.Currency);
            rate = await context.RateAsync(currency, command.ExchangeRate, date, cancellationToken);
            if (rate is null)
            {
                return FinanceErrors.RateMissing;
            }
        }

        var instalments = command.Instalments is { Count: > 0 }
            ? command.Instalments.Select(item => new Instalment(item.DueDate, FinanceApi.Round(item.Amount))).ToList()
            : [new Instalment(command.DueDate ?? date, FinanceApi.Round(amount!.Value))];
        if (amount is { } total && instalments.Sum(item => item.Amount) != FinanceApi.Round(total))
        {
            return Error.Validation("finance.charge.plan_mismatch", "The instalments must add up to the amount.");
        }

        var now = context.Now;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var sequence = await db.NextDocumentSequenceAsync(NumberSeries, date.Year, cancellationToken);
        var charge = Charge.Record(
            DocumentNumber.Format(NumberSeries, date.Year, sequence),
            command.Kind == "opening" ? ChargeKind.Opening : ChargeKind.Sale,
            partyId, partyName, date, description, currency, rate.Value, instalments, command.QuoteId);
        if (!string.IsNullOrWhiteSpace(command.InvoiceNumber))
        {
            charge.MarkInvoiced(command.InvoiceNumber, date);
        }

        db.Charges.Add(charge);
        db.Ledger.Add(LedgerEntry.Post(partyId, date, LedgerSource.Charge, charge.Id.Value, charge.Number, charge.Description, charge.Amount, 0m, currency, charge.ExchangeRate, now));
        db.Publish(new ChargeRecorded(
            tenantContext.TenantId, date, now, charge.Id.Value, charge.Number, partyId, partyName, charge.Description,
            charge.Amount, currency.Code, instalments.Count, command.QuoteId, context.UserId, context.UserName));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ChargeViews.ToResponse(charge, context.Today);
    }
}

internal sealed class ChargeActionsHandler(FinanceDbContext db, FinanceContext context)
{
    public async Task<Result<ChargeResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var charge = await FindAsync(id, cancellationToken);
        return charge is null ? FinanceErrors.ChargeNotFound : ChargeViews.ToResponse(charge, context.Today);
    }

    public async Task<Result<ChargeResponse>> MarkInvoicedAsync(Guid id, MarkInvoicedCommand command, CancellationToken cancellationToken)
    {
        var charge = await FindAsync(id, cancellationToken);
        if (charge is null)
        {
            return FinanceErrors.ChargeNotFound;
        }

        charge.MarkInvoiced(command.InvoiceNumber, command.InvoiceDate);
        await db.SaveChangesAsync(cancellationToken);
        return ChargeViews.ToResponse(charge, context.Today);
    }

    /// <summary>Cancels with a mirror ledger line (financial records are never deleted, ADR-0004).</summary>
    public async Task<Result<ChargeResponse>> CancelAsync(Guid id, CancelCommand command, CancellationToken cancellationToken)
    {
        var charge = await FindAsync(id, cancellationToken);
        if (charge is null)
        {
            return FinanceErrors.ChargeNotFound;
        }

        if (!charge.Cancel(command.Reason))
        {
            return FinanceErrors.ChargeNotCancellable;
        }

        var posted = await db.Ledger.FirstAsync(entry => entry.Source == LedgerSource.Charge && entry.SourceId == charge.Id.Value && entry.ReversesId == null, cancellationToken);
        var now = context.Now;
        db.Ledger.Add(posted.Reverse(context.Today, charge.Description, now));
        db.Publish(new ChargeCancelled(charge.TenantId, now, charge.Id.Value, charge.Number, charge.PartyId, charge.Amount, charge.Currency.Code, command.Reason, context.UserId, context.UserName));
        await db.SaveChangesAsync(cancellationToken);
        return ChargeViews.ToResponse(charge, context.Today);
    }

    /// <summary>The accountant's list: active sales not yet invoiced (until e-invoicing arrives in Phase 5).</summary>
    public async Task<IReadOnlyList<ChargeResponse>> ToInvoiceAsync(CancellationToken cancellationToken)
    {
        var charges = await db.Charges.AsNoTracking().Include(charge => charge.Items)
            .Where(charge => charge.Status == ChargeStatus.Active && charge.Kind == ChargeKind.Sale && charge.InvoiceNumber == null)
            .OrderBy(charge => charge.Date)
            .Take(500)
            .ToListAsync(cancellationToken);
        return charges.Select(charge => ChargeViews.ToResponse(charge, context.Today)).ToList();
    }

    /// <summary>A contract's or a party's charges, newest first (what a retainer has billed so far).</summary>
    public async Task<IReadOnlyList<ChargeResponse>> ListAsync(Guid? contractId, Guid? partyId, CancellationToken cancellationToken)
    {
        var query = db.Charges.AsNoTracking().Include(charge => charge.Items).AsQueryable();
        if (contractId is { } contract)
        {
            query = query.Where(charge => charge.ContractId == contract);
        }

        if (partyId is { } party)
        {
            query = query.Where(charge => charge.PartyId == party);
        }

        var charges = await query.OrderByDescending(charge => charge.Date).ThenByDescending(charge => charge.Number).Take(200).ToListAsync(cancellationToken);
        return charges.Select(charge => ChargeViews.ToResponse(charge, context.Today)).ToList();
    }

    private Task<Charge?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var chargeId = ChargeId.From(id);
        return db.Charges.Include(charge => charge.Items).FirstOrDefaultAsync(charge => charge.Id == chargeId, cancellationToken);
    }
}
