using System.Text.Json;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Akiron.Modules.Crm.Features.Leads;

internal static class LeadEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var leads = endpoints.MapGroup("/leads");

        leads.MapGet("/", async ([AsParameters] LeadFilter filter, LeadsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(filter, cancellationToken)))
            .RequirePermission(CrmPermissions.LeadsRead)
            .Validate<LeadFilter>()
            .Produces<IReadOnlyList<LeadResponse>>()
            .WithSummary("Leads: view=open for the board (by stage and rank), view=closed for won and lost");

        leads.MapGet("/{id:guid}", async (Guid id, LeadsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.GetAsync(id, cancellationToken)))
            .RequirePermission(CrmPermissions.LeadsRead)
            .Produces<LeadResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One lead");

        leads.MapPost("/", async (LeadCommand command, LeadsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.CreateAsync(command, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/crm/leads/{result.Value.Id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(CrmPermissions.LeadsWrite)
            .Validate<LeadCommand>()
            .Produces<LeadResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Add a lead to the first column");

        leads.MapPut("/{id:guid}", async (Guid id, LeadCommand command, LeadsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.UpdateAsync(id, command, cancellationToken)))
            .RequirePermission(CrmPermissions.LeadsWrite)
            .Validate<LeadCommand>()
            .Produces<LeadResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Change a lead's details");

        leads.MapPost("/{id:guid}/move", async (Guid id, MoveLeadCommand command, LeadsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.MoveAsync(id, command, cancellationToken)))
            .RequirePermission(CrmPermissions.LeadsWrite)
            .Validate<MoveLeadCommand>()
            .Produces<LeadResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Move on the board: another stage, another place, won, or lost (with a reason)");

        leads.MapPost("/{id:guid}/convert", async (Guid id, ConvertLeadCommand command, LeadsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ConvertAsync(id, command, cancellationToken)))
            .RequirePermission(CrmPermissions.LeadsWrite)
            .RequirePermission(CrmPermissions.PartiesWrite)
            .Produces<ConvertLeadResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Create a customer from the lead (or link an existing party)");

        leads.MapDelete("/{id:guid}", async (Guid id, LeadsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
            })
            .RequirePermission(CrmPermissions.LeadsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a lead (archived, not erased)");

        var form = endpoints.MapGroup("/lead-form");

        form.MapGet("/", async (LeadFormsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.GetAsync(cancellationToken)))
            .RequirePermission(CrmPermissions.LeadsRead)
            .Produces<LeadFormResponse>()
            .WithSummary("The web form's settings and embed key");

        form.MapPut("/", async (LeadFormCommand command, LeadFormsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.SaveAsync(command, cancellationToken)))
            .RequirePermission(CrmPermissions.LeadsWrite)
            .Validate<LeadFormCommand>()
            .Produces<LeadFormResponse>()
            .WithSummary("Turn the web form on or off, set where visitors land and who gets the leads");

        form.MapPost("/rotate", async (LeadFormsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.RotateAsync(cancellationToken)))
            .RequirePermission(CrmPermissions.LeadsWrite)
            .Produces<LeadFormResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Issue a new key; embeds with the old one stop working");

        // A visitor's browser posts a plain HTML form here from the agency's site (or a script sends JSON).
        endpoints.MapPost("/public/forms/{key}", async (string key, HttpRequest request, PublicLeadFormHandler handler, IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                var isForm = request.HasFormContentType;
                var input = isForm ? await ReadFormAsync(request, cancellationToken) : await ReadJsonAsync(request, cancellationToken);
                if (input is null)
                {
                    return Error.Validation("common.validation.failed", "Send the form fields as a form post or a JSON object.").ToProblem();
                }

                var result = await handler.CaptureAsync(key, input, cancellationToken);
                if (!result.IsSuccess)
                {
                    return result.Error.ToProblem();
                }

                if (!isForm)
                {
                    return Results.Ok(new { received = true });
                }

                var thanks = $"{configuration["App:WebUrl"]?.TrimEnd('/') ?? "http://localhost:3100"}/forms/thanks";
                return Results.Redirect(result.Value ?? thanks);
            })
            .AsPublicLink()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .DisableAntiforgery()
            .Accepts<WebLead>("application/json", "application/x-www-form-urlencoded")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Public: a website visitor leaves their details; a lead appears on the board");
    }

    private static async Task<WebLead?> ReadFormAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        return new WebLead(form["name"], form["email"], form["phone"], form["company"], form["message"], form["website"]);
    }

    private static async Task<WebLead?> ReadJsonAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<WebLead>(request.Body, JsonSerializerOptions.Web, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
