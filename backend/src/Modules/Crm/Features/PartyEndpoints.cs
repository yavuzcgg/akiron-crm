using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Crm.Features.ArchiveParty;
using Akiron.Modules.Crm.Features.Contacts;
using Akiron.Modules.Crm.Features.CreateParty;
using Akiron.Modules.Crm.Features.GetParty;
using Akiron.Modules.Crm.Features.ListParties;
using Akiron.Modules.Crm.Features.UpdateParty;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.Modules.Crm.Features;

internal static class PartyEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var parties = endpoints.MapGroup("/parties");

        parties.MapGet("/", async ([AsParameters] PageRequest page, [AsParameters] PartyFilter filter, ListPartiesHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.HandleAsync(page, filter, cancellationToken)))
            .RequirePermission(CrmPermissions.PartiesRead)
            .Validate<PageRequest>()
            .Validate<PartyFilter>()
            .Produces<PagedResult<PartyListItem>>()
            .WithSummary("Customers and suppliers, sorted by name; search matches name, code, tax number, e-mail and phone");

        parties.MapGet("/{id:guid}", async (Guid id, GetPartyHandler handler, CancellationToken cancellationToken) =>
                ToResult(await handler.HandleAsync(id, cancellationToken)))
            .RequirePermission(CrmPermissions.PartiesRead)
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One party with its contacts");

        parties.MapPost("/", async (CreatePartyCommand command, CreatePartyHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(command, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/crm/parties/{result.Value.Id}", result.Value)
                    : result.Error.ToProblem();
            })
            .RequirePermission(CrmPermissions.PartiesWrite)
            .Validate<CreatePartyCommand>()
            .Produces<PartyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Add a customer or supplier");

        parties.MapPut("/{id:guid}", async (Guid id, UpdatePartyCommand command, UpdatePartyHandler handler, CancellationToken cancellationToken) =>
                ToResult(await handler.HandleAsync(id, command, cancellationToken)))
            .RequirePermission(CrmPermissions.PartiesWrite)
            .Validate<UpdatePartyCommand>()
            .Produces<PartyResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Change a party's details");

        parties.MapDelete("/{id:guid}", async (Guid id, ArchivePartyHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
            })
            .RequirePermission(CrmPermissions.PartiesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Archive a party; its history is kept");

        parties.MapPost("/{id:guid}/contacts", async (Guid id, ContactCommand command, ContactsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.AddAsync(id, command, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/crm/parties/{id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(CrmPermissions.PartiesWrite)
            .Validate<ContactCommand>()
            .Produces<PartyContactResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add a contact person to a party");

        parties.MapPut("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId, ContactCommand command, ContactsHandler handler, CancellationToken cancellationToken) =>
                ToResult(await handler.UpdateAsync(id, contactId, command, cancellationToken)))
            .RequirePermission(CrmPermissions.PartiesWrite)
            .Validate<ContactCommand>()
            .Produces<PartyContactResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Change a contact person");

        parties.MapDelete("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId, ContactsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.RemoveAsync(id, contactId, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
            })
            .RequirePermission(CrmPermissions.PartiesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a contact person");
    }

    private static IResult ToResult<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
