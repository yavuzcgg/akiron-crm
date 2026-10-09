using Akiron.Modules.Finance.Domain;
using Akiron.Modules.Finance.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Finance.Features.Statements;

/// <param name="Balance">Debits minus credits: positive, the party owes us; negative, we hold an advance.</param>
internal sealed record CurrencyBalance(string Currency, decimal Debit, decimal Credit, decimal Balance, decimal Overdue, decimal Advance);

/// <param name="Balance">The running balance in this line's currency after the line.</param>
internal sealed record StatementLine(
    Guid Id,
    DateOnly Date,
    string Source,
    Guid SourceId,
    string? DocumentNumber,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    string Currency,
    bool IsReversal);

internal sealed record OpenItemRow(
    Guid Id,
    Guid ChargeId,
    string ChargeNumber,
    string Description,
    Guid PartyId,
    string PartyName,
    int Sequence,
    int InstalmentCount,
    DateOnly DueDate,
    decimal Amount,
    decimal Remaining,
    string Currency,
    bool IsOverdue,
    int DaysOverdue);

internal sealed record AdvanceRow(Guid ReceiptId, string Number, DateOnly Date, decimal Unallocated, string Currency);

/// <summary>A party's account (cari ekstre): balances per currency, ledger lines, what is open, what is on account.</summary>
internal sealed record StatementResponse(
    Guid PartyId,
    IReadOnlyList<CurrencyBalance> Balances,
    IReadOnlyList<StatementLine> Lines,
    IReadOnlyList<OpenItemRow> OpenItems,
    IReadOnlyList<AdvanceRow> Advances);

internal sealed record PartyBalanceRow(Guid PartyId, string PartyName, string Currency, decimal Balance, decimal Overdue, DateOnly? OldestDue);

internal sealed record FinanceSummary(IReadOnlyList<SummaryAmount> Receivable, IReadOnlyList<SummaryAmount> Overdue, IReadOnlyList<SummaryAmount> CollectedThisMonth, int OverdueCount, int ToInvoiceCount);

internal sealed record SummaryAmount(string Currency, decimal Amount);

internal sealed record OpenItemFilter(
    [FromQuery(Name = "partyId")] Guid? PartyId = null,
    [FromQuery(Name = "overdue")] bool Overdue = false);

/// <summary>Read side of Finance: balances and aging are always computed from the ledger and open items (ADR-0004).</summary>
internal sealed class StatementsHandler(FinanceDbContext db, FinanceContext context)
{
    private const int MaxLines = 1000;

    public async Task<StatementResponse> StatementAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var today = context.Today;
        var entries = await db.Ledger.AsNoTracking()
            .Where(entry => entry.PartyId == partyId)
            .OrderBy(entry => entry.Date).ThenBy(entry => entry.Sequence)
            .Take(MaxLines)
            .ToListAsync(cancellationToken);

        var running = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var lines = entries.Select(entry =>
            {
                var balance = running.GetValueOrDefault(entry.Currency.Code) + entry.Debit - entry.Credit;
                running[entry.Currency.Code] = balance;
                return new StatementLine(
                    entry.Id.Value, entry.Date, entry.Source.ToString().ToLowerInvariant(), entry.SourceId, entry.DocumentNumber,
                    entry.Description, entry.Debit, entry.Credit, balance, entry.Currency.Code, entry.ReversesId is not null);
            })
            .Reverse()
            .ToList();

        var open = await OpenRowsAsync(new OpenItemFilter(partyId), today, cancellationToken);
        var advances = (await db.Receipts.AsNoTracking().Include(receipt => receipt.Allocations)
                .Where(receipt => receipt.PartyId == partyId && receipt.Status == ReceiptStatus.Recorded)
                .OrderBy(receipt => receipt.Date)
                .ToListAsync(cancellationToken))
            .Where(receipt => receipt.Unallocated > 0)
            .Select(receipt => new AdvanceRow(receipt.Id.Value, receipt.Number, receipt.Date, receipt.Unallocated, receipt.Currency.Code))
            .ToList();

        var balances = entries
            .GroupBy(entry => entry.Currency.Code)
            .Select(group => new CurrencyBalance(
                group.Key,
                group.Sum(entry => entry.Debit),
                group.Sum(entry => entry.Credit),
                group.Sum(entry => entry.Debit - entry.Credit),
                open.Where(row => row.Currency == group.Key && row.IsOverdue).Sum(row => row.Remaining),
                advances.Where(row => row.Currency == group.Key).Sum(row => row.Unallocated)))
            .OrderBy(balance => balance.Currency == "TRY" ? 0 : 1).ThenBy(balance => balance.Currency)
            .ToList();

        return new StatementResponse(partyId, balances, lines, open, advances);
    }

    public Task<IReadOnlyList<OpenItemRow>> OpenItemsAsync(OpenItemFilter filter, CancellationToken cancellationToken) =>
        OpenRowsAsync(filter, context.Today, cancellationToken);

    /// <summary>Every party with a non-zero balance, per currency, largest first.</summary>
    public async Task<IReadOnlyList<PartyBalanceRow>> BalancesAsync(CancellationToken cancellationToken)
    {
        var today = context.Today;
        var balances = await db.Ledger.AsNoTracking()
            .GroupBy(entry => new { entry.PartyId, entry.Currency })
            .Select(group => new { group.Key.PartyId, group.Key.Currency, Balance = group.Sum(entry => entry.Debit - entry.Credit) })
            .Where(row => row.Balance != 0)
            .ToListAsync(cancellationToken);

        var open = await OpenRowsAsync(new OpenItemFilter(), today, cancellationToken);
        var names = await db.Charges.AsNoTracking().Select(charge => new { charge.PartyId, charge.PartyName })
            .Union(db.Receipts.AsNoTracking().Select(receipt => new { receipt.PartyId, receipt.PartyName }))
            .ToListAsync(cancellationToken);
        var nameOf = names.GroupBy(row => row.PartyId).ToDictionary(group => group.Key, group => group.Last().PartyName);

        return balances
            .Select(row =>
            {
                var overdue = open.Where(item => item.PartyId == row.PartyId && item.Currency == row.Currency.Code && item.IsOverdue).ToList();
                return new PartyBalanceRow(
                    row.PartyId, nameOf.GetValueOrDefault(row.PartyId, string.Empty), row.Currency.Code, row.Balance,
                    overdue.Sum(item => item.Remaining), overdue.Count == 0 ? null : overdue.Min(item => item.DueDate));
            })
            .OrderByDescending(row => row.Overdue).ThenByDescending(row => row.Balance)
            .Take(500)
            .ToList();
    }

    public async Task<FinanceSummary> SummaryAsync(CancellationToken cancellationToken)
    {
        var today = context.Today;
        var open = await OpenRowsAsync(new OpenItemFilter(), today, cancellationToken);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var collected = await db.Receipts.AsNoTracking()
            .Where(receipt => receipt.Status == ReceiptStatus.Recorded && receipt.Date >= monthStart)
            .GroupBy(receipt => receipt.Currency)
            .Select(group => new { group.Key, Amount = group.Sum(receipt => receipt.Amount) })
            .ToListAsync(cancellationToken);
        var toInvoice = await db.Charges.CountAsync(charge => charge.Status == ChargeStatus.Active && charge.Kind == ChargeKind.Sale && charge.InvoiceNumber == null, cancellationToken);

        static List<SummaryAmount> Sum(IEnumerable<OpenItemRow> rows) =>
            rows.GroupBy(row => row.Currency).Select(group => new SummaryAmount(group.Key, group.Sum(row => row.Remaining))).OrderBy(row => row.Currency == "TRY" ? 0 : 1).ToList();

        var overdue = open.Where(row => row.IsOverdue).ToList();
        return new FinanceSummary(
            Sum(open),
            Sum(overdue),
            collected.Select(row => new SummaryAmount(row.Key.Code, row.Amount)).ToList(),
            overdue.Count,
            toInvoice);
    }

    private async Task<IReadOnlyList<OpenItemRow>> OpenRowsAsync(OpenItemFilter filter, DateOnly today, CancellationToken cancellationToken)
    {
        var query = db.OpenItems.AsNoTracking().Where(item => item.IsOpen);
        if (filter.PartyId is { } partyId)
        {
            query = query.Where(item => item.PartyId == partyId);
        }

        if (filter.Overdue)
        {
            query = query.Where(item => item.DueDate < today);
        }

        var rows = await query
            .Join(db.Charges, item => item.ChargeId, charge => charge.Id, (item, charge) => new { item, charge.Number, charge.Description, charge.PartyName, Count = charge.Items.Count })
            .OrderBy(row => row.item.DueDate)
            .Take(1000)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new OpenItemRow(
                row.item.Id.Value,
                row.item.ChargeId.Value,
                row.Number,
                row.Description,
                row.item.PartyId,
                row.PartyName,
                row.item.Sequence,
                row.Count,
                row.item.DueDate,
                row.item.Amount,
                row.item.Amount - row.item.Allocated,
                row.item.Currency.Code,
                row.item.DueDate < today,
                Math.Max(0, today.DayNumber - row.item.DueDate.DayNumber)))
            .ToList();
    }
}
