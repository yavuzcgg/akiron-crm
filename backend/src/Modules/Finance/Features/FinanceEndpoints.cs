using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Finance.Features.Charges;
using Akiron.Modules.Finance.Features.Receipts;
using Akiron.Modules.Finance.Features.Reminders;
using Akiron.Modules.Finance.Features.Statements;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.Modules.Finance.Features;

internal static class FinanceEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ClientPortal.ClientAccountEndpoints.Map(endpoints);

        endpoints.MapGet("/summary", async (StatementsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.SummaryAsync(cancellationToken)))
            .RequirePermission(FinancePermissions.Read)
            .Produces<FinanceSummary>()
            .WithSummary("Receivable, overdue and collected this month, per currency");

        endpoints.MapGet("/balances", async (StatementsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.BalancesAsync(cancellationToken)))
            .RequirePermission(FinancePermissions.Read)
            .Produces<IReadOnlyList<PartyBalanceRow>>()
            .WithSummary("Parties with a balance, overdue first");

        endpoints.MapGet("/parties/{partyId:guid}/statement", async (Guid partyId, StatementsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.StatementAsync(partyId, cancellationToken)))
            .RequirePermission(FinancePermissions.Read)
            .Produces<StatementResponse>()
            .WithSummary("A party's account: balances, ledger lines (newest first), open items, advances");

        endpoints.MapGet("/open-items", async ([AsParameters] OpenItemFilter filter, StatementsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.OpenItemsAsync(filter, cancellationToken)))
            .RequirePermission(FinancePermissions.Read)
            .Produces<IReadOnlyList<OpenItemRow>>()
            .WithSummary("Instalments still to collect, earliest due first");

        var charges = endpoints.MapGroup("/charges");

        charges.MapPost("/", async (RecordChargeCommand command, RecordChargeHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(command, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/finance/charges/{result.Value.Id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(FinancePermissions.Write)
            .Validate<RecordChargeCommand>()
            .Produces<ChargeResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Charge a client (manually or from an accepted quote), with a payment plan");

        charges.MapGet("/", async (Guid? contractId, Guid? partyId, ChargeActionsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(contractId, partyId, cancellationToken)))
            .RequirePermission(FinancePermissions.Read)
            .Produces<IReadOnlyList<ChargeResponse>>()
            .WithSummary("A contract's or a party's charges, newest first");

        endpoints.MapPost("/open-items/{id:guid}/remind", async (Guid id, RemindCommand command, RemindersHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.SendAsync(id, command, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(FinancePermissions.Write)
            .Validate<RemindCommand>()
            .Produces<ReminderResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("E-mail the client a reminder about one open instalment");

        charges.MapGet("/to-invoice", async (ChargeActionsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ToInvoiceAsync(cancellationToken)))
            .RequirePermission(FinancePermissions.Read)
            .Produces<IReadOnlyList<ChargeResponse>>()
            .WithSummary("Sales not yet invoiced (for the accountant)");

        charges.MapGet("/{id:guid}", async (Guid id, ChargeActionsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.GetAsync(id, cancellationToken)))
            .RequirePermission(FinancePermissions.Read)
            .Produces<ChargeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One charge with its instalments");

        charges.MapPost("/{id:guid}/invoiced", async (Guid id, MarkInvoicedCommand command, ChargeActionsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.MarkInvoicedAsync(id, command, cancellationToken)))
            .RequirePermission(FinancePermissions.Write)
            .Validate<MarkInvoicedCommand>()
            .Produces<ChargeResponse>()
            .WithSummary("Note the invoice number the accountant issued for this charge");

        charges.MapPost("/{id:guid}/cancel", async (Guid id, CancelCommand command, ChargeActionsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.CancelAsync(id, command, cancellationToken)))
            .RequirePermission(FinancePermissions.Write)
            .Validate<CancelCommand>()
            .Produces<ChargeResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Cancel a charge nothing was collected against (a reversing ledger line is posted)");

        var receipts = endpoints.MapGroup("/receipts");

        receipts.MapGet("/", async ([AsParameters] PageRequest page, [AsParameters] ReceiptFilter filter, ReceiptsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(page, filter, cancellationToken)))
            .RequirePermission(FinancePermissions.Read)
            .Validate<PageRequest>()
            .Produces<PagedResult<ReceiptResponse>>()
            .WithSummary("Collections, newest first");

        receipts.MapGet("/{id:guid}", async (Guid id, ReceiptsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.GetAsync(id, cancellationToken)))
            .RequirePermission(FinancePermissions.Read)
            .Produces<ReceiptResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One collection with its allocations");

        receipts.MapPost("/", async (RecordReceiptCommand command, ReceiptsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.RecordAsync(command, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/finance/receipts/{result.Value.Id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(FinancePermissions.Write)
            .Validate<RecordReceiptCommand>()
            .Produces<ReceiptResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Record money received and allocate it (oldest due first unless told otherwise)");

        receipts.MapPost("/{id:guid}/allocate", async (Guid id, AllocateCommand command, ReceiptsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.AllocateAsync(id, command, cancellationToken)))
            .RequirePermission(FinancePermissions.Write)
            .Produces<ReceiptResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Allocate the unallocated part (an advance) to open items");

        receipts.MapPost("/{id:guid}/reverse", async (Guid id, CancelCommand command, ReceiptsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ReverseAsync(id, command, cancellationToken)))
            .RequirePermission(FinancePermissions.Write)
            .Validate<CancelCommand>()
            .Produces<ReceiptResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Reverse a collection and its allocations (a reversing ledger line is posted)");
    }

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
