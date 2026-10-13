using Akiron.BuildingBlocks.Security;
using Akiron.Modules.Finance.Domain;
using Akiron.Modules.Finance.Features.Statements;
using Akiron.Modules.Finance.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Finance.Features.ClientPortal;

internal sealed record ClientBalance(string Currency, decimal Balance, decimal Overdue, decimal Advance);

internal sealed record ClientOpenItem(Guid Id, string ChargeNumber, string Description, int Sequence, int InstalmentCount, DateOnly DueDate, decimal Amount, decimal Remaining, string Currency, bool IsOverdue, int DaysOverdue);

internal sealed record ClientPayment(string Number, DateOnly Date, decimal Amount, string Currency, string Method);

/// <summary>The client's own account: what they owe and when, what they paid, and the statement behind it.</summary>
internal sealed record ClientAccountResponse(
    IReadOnlyList<ClientBalance> Balances,
    IReadOnlyList<ClientOpenItem> OpenItems,
    IReadOnlyList<ClientPayment> Payments,
    IReadOnlyList<StatementLine> Lines);

/// <summary>Reads the party ledger for the signed-in client only; the same numbers staff see (ADR-0004).</summary>
internal sealed class ClientAccountHandler(FinanceDbContext db, StatementsHandler statements, IPortalVisitor visitor)
{
    public async Task<ClientAccountResponse> GetAsync(CancellationToken cancellationToken)
    {
        var partyId = visitor.PartyId;
        var statement = await statements.StatementAsync(partyId, cancellationToken);
        var payments = await db.Receipts.AsNoTracking()
            .Where(receipt => receipt.PartyId == partyId && receipt.Status == ReceiptStatus.Recorded)
            .OrderByDescending(receipt => receipt.Date).ThenByDescending(receipt => receipt.Number)
            .Take(50)
            .ToListAsync(cancellationToken);

        return new ClientAccountResponse(
            statement.Balances.Select(balance => new ClientBalance(balance.Currency, balance.Balance, balance.Overdue, balance.Advance)).ToList(),
            statement.OpenItems
                .Select(item => new ClientOpenItem(
                    item.Id, item.ChargeNumber, item.Description, item.Sequence, item.InstalmentCount, item.DueDate, item.Amount, item.Remaining, item.Currency, item.IsOverdue, item.DaysOverdue))
                .ToList(),
            payments.Select(receipt => new ClientPayment(receipt.Number, receipt.Date, receipt.Amount, receipt.Currency.Code, FinanceApi.Method(receipt.Method))).ToList(),
            statement.Lines);
    }
}

internal static class ClientAccountEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/portal/account", async (ClientAccountHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.GetAsync(cancellationToken)))
            .RequirePortal()
            .Produces<ClientAccountResponse>()
            .WithSummary("Portal: the client's balance, due items, payments and statement");
}
