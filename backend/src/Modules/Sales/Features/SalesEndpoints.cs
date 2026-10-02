using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Sales.Features.Catalog;
using Akiron.Modules.Sales.Features.PublicQuotes;
using Akiron.Modules.Sales.Features.Quotes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.Modules.Sales.Features;

internal static class SalesEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapCatalog(endpoints.MapGroup("/catalog"));
        MapQuotes(endpoints.MapGroup("/quotes"));
        MapPublic(endpoints.MapGroup("/public/quotes"));
    }

    private static void MapCatalog(RouteGroupBuilder catalog)
    {
        catalog.MapGet("/", async (string? search, CatalogHandler handler, CancellationToken cancellationToken) =>
                search is { Length: > 100 }
                    ? (IResult)Error.Validation("common.validation.failed", "Search at most 100 characters.").ToProblem()
                    : TypedResults.Ok(await handler.ListAsync(search, cancellationToken)))
            .RequirePermission(SalesPermissions.QuotesRead)
            .Produces<IReadOnlyList<CatalogItemResponse>>()
            .WithSummary("Services and products, by name");

        catalog.MapPost("/", async (CatalogItemCommand command, CatalogHandler handler, CancellationToken cancellationToken) =>
            {
                var item = await handler.CreateAsync(command, cancellationToken);
                return TypedResults.Created($"/api/v1/sales/catalog/{item.Id}", item);
            })
            .RequirePermission(SalesPermissions.CatalogManage)
            .Validate<CatalogItemCommand>()
            .Produces<CatalogItemResponse>(StatusCodes.Status201Created)
            .WithSummary("Add a service or product");

        catalog.MapPut("/{id:guid}", async (Guid id, CatalogItemCommand command, CatalogHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.UpdateAsync(id, command, cancellationToken)))
            .RequirePermission(SalesPermissions.CatalogManage)
            .Validate<CatalogItemCommand>()
            .Produces<CatalogItemResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Change an item; quotes already written keep their values");

        catalog.MapDelete("/{id:guid}", async (Guid id, CatalogHandler handler, CancellationToken cancellationToken) =>
                NoContent(await handler.RemoveAsync(id, cancellationToken)))
            .RequirePermission(SalesPermissions.CatalogManage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Archive an item");
    }

    private static void MapQuotes(RouteGroupBuilder quotes)
    {
        quotes.MapGet("/", async ([AsParameters] PageRequest page, [AsParameters] QuoteFilter filter, ListQuotesHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.HandleAsync(page, filter, cancellationToken)))
            .RequirePermission(SalesPermissions.QuotesRead)
            .Validate<PageRequest>()
            .Validate<QuoteFilter>()
            .Produces<PagedResult<QuoteListItem>>()
            .WithSummary("Quotes, newest first");

        quotes.MapGet("/{id:guid}", async (Guid id, GetQuoteHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.HandleAsync(id, cancellationToken)))
            .RequirePermission(SalesPermissions.QuotesRead)
            .Produces<QuoteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One quote with its lines and totals");

        quotes.MapGet("/{id:guid}/pdf", async (Guid id, string? lang, QuotePdfHandler handler, CancellationToken cancellationToken) =>
                File(await handler.HandleAsync(id, lang == "en" ? "en" : "tr", cancellationToken)))
            .RequirePermission(SalesPermissions.QuotesRead)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("The quote as a PDF");

        quotes.MapPost("/", async (QuoteCommand command, CreateQuoteHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(command, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/sales/quotes/{result.Value.Id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(SalesPermissions.QuotesWrite)
            .Validate<QuoteCommand>()
            .Produces<QuoteResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Start a quote as a draft");

        quotes.MapPut("/{id:guid}", async (Guid id, QuoteCommand command, UpdateQuoteHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.HandleAsync(id, command, cancellationToken)))
            .RequirePermission(SalesPermissions.QuotesWrite)
            .Validate<QuoteCommand>()
            .Produces<QuoteResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Change a draft");

        quotes.MapPost("/{id:guid}/send", async (Guid id, SendQuoteCommand command, QuoteLifecycleHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.SendAsync(id, command, cancellationToken)))
            .RequirePermission(SalesPermissions.QuotesWrite)
            .Produces<SendQuoteResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Freeze the draft, open its public link and e-mail it to the recipient");

        quotes.MapPost("/{id:guid}/revise", async (Guid id, QuoteLifecycleHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ReviseAsync(id, cancellationToken)))
            .RequirePermission(SalesPermissions.QuotesWrite)
            .Produces<QuoteResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Turn a sent or rejected quote into the next revision's draft; the old link stops working");

        quotes.MapDelete("/{id:guid}", async (Guid id, QuoteLifecycleHandler handler, CancellationToken cancellationToken) =>
                NoContent(await handler.DeleteAsync(id, cancellationToken)))
            .RequirePermission(SalesPermissions.QuotesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Delete a draft that was never sent");
    }

    /// <summary>The client's side: no account, the link's secret is the key (rate-limited against guessing).</summary>
    private static void MapPublic(RouteGroupBuilder links)
    {
        links.MapGet("/{token}", async (string token, PublicQuoteHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ViewAsync(token, cancellationToken)))
            .AsPublicLink()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces<PublicQuoteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("A quote as its recipient sees it");

        links.MapGet("/{token}/pdf", async (string token, string? lang, PublicQuoteHandler handler, CancellationToken cancellationToken) =>
                File(await handler.PdfAsync(token, lang == "en" ? "en" : "tr", cancellationToken)))
            .AsPublicLink()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("The quote's PDF for its recipient");

        links.MapPost("/{token}/accept", async (string token, DecideQuoteCommand command, PublicQuoteHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.DecideAsync(token, accept: true, command, cancellationToken)))
            .AsPublicLink()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<DecideQuoteCommand>()
            .Produces<PublicQuoteResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Accept the quote");

        links.MapPost("/{token}/reject", async (string token, DecideQuoteCommand command, PublicQuoteHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.DecideAsync(token, accept: false, command, cancellationToken)))
            .AsPublicLink()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<DecideQuoteCommand>()
            .Produces<PublicQuoteResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Decline the quote, optionally saying why");
    }

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();

    private static IResult NoContent<T>(Result<T> result) => result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();

    /// <summary>Shown in the browser (inline) so the client can read it before downloading.</summary>
    private static IResult File(Result<(byte[] Content, string FileName)> result) =>
        result.IsSuccess
            ? TypedResults.File(result.Value.Content, "application/pdf", result.Value.FileName)
            : result.Error.ToProblem();
}
