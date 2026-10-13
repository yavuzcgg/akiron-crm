using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Jobs.Features.ClientPortal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.Modules.Jobs.Features.Tickets;

internal static class TicketEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var tickets = endpoints.MapGroup("/tickets");

        tickets.MapGet("/", async ([AsParameters] TicketFilter filter, TicketsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(filter, cancellationToken)))
            .RequirePermission(JobsPermissions.TicketsRead)
            .Validate<TicketFilter>()
            .Produces<IReadOnlyList<TicketResponse>>()
            .WithSummary("Support tickets: view=open for the queue (most urgent first), view=closed for resolved ones");

        tickets.MapGet("/{id:guid}", async (Guid id, TicketsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.GetAsync(id, cancellationToken)))
            .RequirePermission(JobsPermissions.TicketsRead)
            .Produces<TicketResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One ticket");

        tickets.MapPost("/", async (TicketCommand command, TicketsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.OpenAsync(command, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/jobs/tickets/{result.Value.Id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(JobsPermissions.TicketsWrite)
            .Validate<TicketCommand>()
            .Produces<TicketResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Open a ticket on a client's word (a call, an e-mail)");

        tickets.MapPut("/{id:guid}", async (Guid id, TicketCommand command, TicketsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.UpdateAsync(id, command, cancellationToken)))
            .RequirePermission(JobsPermissions.TicketsWrite)
            .Validate<TicketCommand>()
            .Produces<TicketResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Change a ticket's text, priority or assignee");

        tickets.MapPost("/{id:guid}/status", async (Guid id, TicketStatusCommand command, TicketsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.MoveAsync(id, command, cancellationToken)))
            .RequirePermission(JobsPermissions.TicketsWrite)
            .Validate<TicketStatusCommand>()
            .Produces<TicketResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Move a ticket: in progress, waiting for the client, resolved, closed, or open again");

        tickets.MapPost("/{id:guid}/convert", async (Guid id, TicketsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ConvertAsync(id, cancellationToken)))
            .RequirePermission(JobsPermissions.TicketsWrite)
            .RequirePermission(JobsPermissions.WorkOrdersWrite)
            .Produces<TicketResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Open a work order from the ticket");

        tickets.MapDelete("/{id:guid}", async (Guid id, TicketsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
            })
            .RequirePermission(JobsPermissions.TicketsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a ticket (archived, not erased)");

        var portal = endpoints.MapGroup("/portal/tickets");

        portal.MapGet("/", async (ClientTicketsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(cancellationToken)))
            .RequirePortal()
            .Produces<IReadOnlyList<ClientTicket>>()
            .WithSummary("Portal: the client's support tickets, open ones first");

        portal.MapGet("/{id:guid}", async (Guid id, ClientTicketsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.GetAsync(id, cancellationToken)))
            .RequirePortal()
            .Produces<ClientTicket>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Portal: one ticket");

        portal.MapPost("/", async (ClientTicketCommand command, ClientTicketsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.OpenAsync(command, cancellationToken);
                return result.IsSuccess ? Results.Created((string?)null, result.Value) : result.Error.ToProblem();
            })
            .RequirePortal()
            .Validate<ClientTicketCommand>()
            .Produces<ClientTicket>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Portal: ask the team for help");

        portal.MapPost("/{id:guid}/close", async (Guid id, ClientTicketsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.CloseAsync(id, cancellationToken)))
            .RequirePortal()
            .Produces<ClientTicket>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Portal: the client closes their ticket");
    }

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
