using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Finance;
using Akiron.Modules.Finance.Domain;
using Akiron.Modules.Finance.Features.Charges;
using Akiron.Modules.Finance.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Finance.Features.Receipts;

internal sealed record AllocationCommand(Guid OpenItemId, decimal Amount);

/// <param name="Allocations">Where the money goes; empty with <paramref name="AutoAllocate"/> fills the oldest due items first.</param>
internal sealed record RecordReceiptCommand(
    Guid PartyId,
    decimal Amount,
    string Method,
    string Currency = "TRY",
    decimal? ExchangeRate = null,
    DateOnly? Date = null,
    string? Account = null,
    string? Reference = null,
    string? Note = null,
    IReadOnlyList<AllocationCommand>? Allocations = null,
    bool AutoAllocate = true);

internal sealed class RecordReceiptValidator : AbstractValidator<RecordReceiptCommand>
{
    public RecordReceiptValidator()
    {
        RuleFor(command => command.Amount).GreaterThan(0m).LessThanOrEqualTo(1_000_000_000m);
        RuleFor(command => command.Method).Must(method => FinanceApi.Methods.Contains(method)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.Currency).Must(Currency.IsSupported).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.ExchangeRate).GreaterThan(0m);
        RuleFor(command => command.Account).MaximumLength(100);
        RuleFor(command => command.Reference).MaximumLength(100);
        RuleFor(command => command.Note).MaximumLength(Receipt.NoteMaxLength);
        RuleForEach(command => command.Allocations).ChildRules(allocation => allocation.RuleFor(item => item.Amount).GreaterThan(0m));
    }
}

internal sealed record AllocateCommand(IReadOnlyList<AllocationCommand>? Allocations = null, bool AutoAllocate = true);

internal sealed record AllocationResponse(Guid OpenItemId, string ChargeNumber, int Sequence, DateOnly DueDate, decimal Amount, bool IsReversed);

internal sealed record ReceiptResponse(
    Guid Id,
    string Number,
    string Status,
    Guid PartyId,
    string PartyName,
    DateOnly Date,
    decimal Amount,
    decimal Unallocated,
    string Currency,
    decimal ExchangeRate,
    string Method,
    string? Account,
    string? Reference,
    string? Note,
    string? ReversalReason,
    IReadOnlyList<AllocationResponse> Allocations);

internal sealed record ReceiptFilter([FromQuery(Name = "partyId")] Guid? PartyId = null);

/// <summary>Collections: record, allocate, reverse; each posts to the party ledger.</summary>
internal sealed class ReceiptsHandler(FinanceDbContext db, FinanceContext context, ITenantContext tenantContext)
{
    public const string NumberSeries = "TAH";

    public async Task<Result<ReceiptResponse>> RecordAsync(RecordReceiptCommand command, CancellationToken cancellationToken)
    {
        var party = await context.PartyAsync(command.PartyId, cancellationToken);
        if (party is null)
        {
            return FinanceErrors.PartyNotFound;
        }

        var date = command.Date ?? context.Today;
        var currency = Currency.From(command.Currency);
        var rate = await context.RateAsync(currency, command.ExchangeRate, date, cancellationToken);
        if (rate is null)
        {
            return FinanceErrors.RateMissing;
        }

        var now = context.Now;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var sequence = await db.NextDocumentSequenceAsync(NumberSeries, date.Year, cancellationToken);
        var receipt = Receipt.Record(
            DocumentNumber.Format(NumberSeries, date.Year, sequence), party.PartyId, party.Name, date, FinanceApi.Round(command.Amount),
            currency, rate.Value, FinanceApi.ParseMethod(command.Method), command.Account, command.Reference, command.Note);
        db.Receipts.Add(receipt);

        var allocated = await AllocateAsync(receipt, command.Allocations, command.AutoAllocate, now, cancellationToken);
        if (!allocated.IsSuccess)
        {
            return allocated.Error;
        }

        db.Ledger.Add(LedgerEntry.Post(receipt.PartyId, date, LedgerSource.Receipt, receipt.Id.Value, receipt.Number, receipt.Note ?? string.Empty, 0m, receipt.Amount, currency, receipt.ExchangeRate, now));
        db.Publish(new ReceiptRecorded(
            tenantContext.TenantId, now, receipt.Id.Value, receipt.Number, receipt.PartyId, receipt.PartyName, receipt.Amount,
            receipt.Unallocated, currency.Code, FinanceApi.Method(receipt.Method), context.UserId, context.UserName));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ToResponseAsync(receipt, cancellationToken);
    }

    /// <summary>Allocates what is still unallocated (an advance) to open items, e.g. after a new charge.</summary>
    public async Task<Result<ReceiptResponse>> AllocateAsync(Guid id, AllocateCommand command, CancellationToken cancellationToken)
    {
        var receipt = await FindAsync(id, cancellationToken);
        if (receipt is null)
        {
            return FinanceErrors.ReceiptNotFound;
        }

        if (receipt.Status == ReceiptStatus.Reversed)
        {
            return FinanceErrors.ReceiptReversed;
        }

        var allocated = await AllocateAsync(receipt, command.Allocations, command.AutoAllocate, context.Now, cancellationToken);
        if (!allocated.IsSuccess)
        {
            return allocated.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(receipt, cancellationToken);
    }

    public async Task<Result<ReceiptResponse>> ReverseAsync(Guid id, CancelCommand command, CancellationToken cancellationToken)
    {
        var receipt = await FindAsync(id, cancellationToken);
        if (receipt is null)
        {
            return FinanceErrors.ReceiptNotFound;
        }

        var itemIds = receipt.Allocations.Select(allocation => allocation.OpenItemId).ToList();
        var items = await db.OpenItems.Where(item => itemIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (!receipt.Reverse(command.Reason, items))
        {
            return FinanceErrors.ReceiptReversed;
        }

        var posted = await db.Ledger.FirstAsync(entry => entry.Source == LedgerSource.Receipt && entry.SourceId == receipt.Id.Value && entry.ReversesId == null, cancellationToken);
        var now = context.Now;
        db.Ledger.Add(posted.Reverse(context.Today, posted.Description, now));
        db.Publish(new ReceiptReversed(receipt.TenantId, now, receipt.Id.Value, receipt.Number, receipt.PartyId, receipt.Amount, receipt.Currency.Code, command.Reason, context.UserId, context.UserName));
        await db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(receipt, cancellationToken);
    }

    public async Task<Result<ReceiptResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await FindAsync(id, cancellationToken);
        return receipt is null ? FinanceErrors.ReceiptNotFound : await ToResponseAsync(receipt, cancellationToken);
    }

    public async Task<PagedResult<ReceiptResponse>> ListAsync(PageRequest page, ReceiptFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Receipts.AsNoTracking().Include(receipt => receipt.Allocations).AsQueryable();
        if (filter.PartyId is { } partyId)
        {
            query = query.Where(receipt => receipt.PartyId == partyId);
        }

        var result = await query.OrderByDescending(receipt => receipt.Date).ThenByDescending(receipt => receipt.Number).ToPagedResultAsync(page, cancellationToken);
        var items = new List<ReceiptResponse>(result.Items.Count);
        foreach (var receipt in result.Items)
        {
            items.Add(await ToResponseAsync(receipt, cancellationToken));
        }

        return new PagedResult<ReceiptResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    /// <summary>
    /// Explicit allocations are checked item by item (same party, same currency, still open, within
    /// what is left on both sides); automatic allocation takes the party's oldest due items first.
    /// </summary>
    private async Task<Result<bool>> AllocateAsync(Receipt receipt, IReadOnlyList<AllocationCommand>? requested, bool auto, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (requested is { Count: > 0 })
        {
            var ids = requested.Select(allocation => OpenItemId.From(allocation.OpenItemId)).ToList();
            var items = await OpenItemsQuery().Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            if (requested.Sum(allocation => allocation.Amount) > receipt.Unallocated)
            {
                return FinanceErrors.OverAllocated;
            }

            foreach (var allocation in requested)
            {
                if (!items.TryGetValue(OpenItemId.From(allocation.OpenItemId), out var item) || item.PartyId != receipt.PartyId)
                {
                    return FinanceErrors.OpenItemNotFound;
                }

                if (item.Currency != receipt.Currency)
                {
                    return FinanceErrors.CurrencyMismatch;
                }

                var amount = FinanceApi.Round(allocation.Amount);
                if (amount > item.Remaining || receipt.AllocateTo(item, amount, now) != amount)
                {
                    return FinanceErrors.OverAllocated;
                }
            }

            return true;
        }

        if (!auto)
        {
            return true;
        }

        var open = await OpenItemsQuery()
            .Where(item => item.PartyId == receipt.PartyId && item.Currency == receipt.Currency)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        foreach (var item in open)
        {
            if (receipt.Unallocated <= 0)
            {
                break;
            }

            receipt.AllocateTo(item, receipt.Unallocated, now);
        }

        return true;
    }

    /// <summary>Open items of active charges only; a cancelled charge's items are closed.</summary>
    private IQueryable<OpenItem> OpenItemsQuery() => db.OpenItems.Where(item => item.IsOpen);

    private Task<Receipt?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var receiptId = ReceiptId.From(id);
        return db.Receipts.Include(receipt => receipt.Allocations).FirstOrDefaultAsync(receipt => receipt.Id == receiptId, cancellationToken);
    }

    private async Task<ReceiptResponse> ToResponseAsync(Receipt receipt, CancellationToken cancellationToken)
    {
        var itemIds = receipt.Allocations.Select(allocation => allocation.OpenItemId).ToList();
        var items = await db.OpenItems.AsNoTracking()
            .Where(item => itemIds.Contains(item.Id))
            .Join(db.Charges, item => item.ChargeId, charge => charge.Id, (item, charge) => new { item.Id, item.Sequence, item.DueDate, charge.Number })
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        return new ReceiptResponse(
            receipt.Id.Value,
            receipt.Number,
            receipt.Status.ToString().ToLowerInvariant(),
            receipt.PartyId,
            receipt.PartyName,
            receipt.Date,
            receipt.Amount,
            receipt.Unallocated,
            receipt.Currency.Code,
            receipt.ExchangeRate,
            FinanceApi.Method(receipt.Method),
            receipt.Account,
            receipt.Reference,
            receipt.Note,
            receipt.ReversalReason,
            receipt.Allocations
                .Where(allocation => items.ContainsKey(allocation.OpenItemId))
                .Select(allocation =>
                {
                    var item = items[allocation.OpenItemId];
                    return new AllocationResponse(allocation.OpenItemId.Value, item.Number, item.Sequence, item.DueDate, allocation.Amount, allocation.IsReversed);
                })
                .ToList());
    }
}
