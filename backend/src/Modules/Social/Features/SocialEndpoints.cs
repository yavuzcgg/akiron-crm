using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.Modules.Social.Features;

internal static class SocialEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapStaff(endpoints.MapGroup("/posts"));
        MapPortal(endpoints.MapGroup("/portal/posts"));
    }

    private static void MapStaff(RouteGroupBuilder posts)
    {
        posts.MapGet("/", async ([AsParameters] ContentFilter filter, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(filter, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentRead)
            .Validate<ContentFilter>()
            .Produces<IReadOnlyList<ContentPostResponse>>()
            .WithSummary("Planned posts in a date range, by day");

        posts.MapGet("/{id:guid}", async (Guid id, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.GetAsync(id, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentRead)
            .Produces<ContentPostResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One post");

        posts.MapPost("/", async (ContentPostCommand command, ContentPostsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.CreateAsync(command, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/social/posts/{result.Value.Id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(SocialPermissions.ContentWrite)
            .Validate<ContentPostCommand>()
            .Produces<ContentPostResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Plan a post as a draft");

        posts.MapPut("/{id:guid}", async (Guid id, ContentPostCommand command, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.UpdateAsync(id, command, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentWrite)
            .Validate<ContentPostCommand>()
            .Produces<ContentPostResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Change a post that is not with the client");

        posts.MapPost("/{id:guid}/reschedule", async (Guid id, RescheduleCommand command, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.RescheduleAsync(id, command, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentWrite)
            .Produces<ContentPostResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Move a post to another day; its approval stays");

        posts.MapPost("/{id:guid}/submit", async (Guid id, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.SubmitAsync(id, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentWrite)
            .Produces<ContentPostResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Ask for an internal check before the client sees it");

        posts.MapPost("/{id:guid}/return", async (Guid id, ContentNoteCommand command, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ReturnAsync(id, command, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentApprove)
            .Validate<ContentNoteCommand>()
            .Produces<ContentPostResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Send a post in internal review back to its author with a note");

        posts.MapPost("/{id:guid}/send", async (Guid id, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.SendAsync(id, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentApprove)
            .Produces<ContentPostResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Send the post to the client for approval");

        posts.MapPost("/send-batch", async (SendBatchCommand command, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.SendBatchAsync(command, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentApprove)
            .Validate<SendBatchCommand>()
            .Produces<SendBatchResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Send every ready post of one client in a period to the client, in one e-mail");

        posts.MapPost("/{id:guid}/approve", async (Guid id, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ApproveAsync(id, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentApprove)
            .Produces<ContentPostResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Record approval without the portal (the client said yes by phone)");

        posts.MapPost("/{id:guid}/revise", async (Guid id, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ReviseAsync(id, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentWrite)
            .Produces<ContentPostResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Reopen a post that is with the client or approved for edits");

        posts.MapPost("/{id:guid}/publish", async (Guid id, PublishCommand command, ContentPostsHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.PublishAsync(id, command, cancellationToken)))
            .RequirePermission(SocialPermissions.ContentWrite)
            .Validate<PublishCommand>()
            .Produces<ContentPostResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Mark an approved post as published, optionally with its link");

        posts.MapDelete("/{id:guid}", async (Guid id, ContentPostsHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
            })
            .RequirePermission(SocialPermissions.ContentWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Remove a post that has not been published");
    }

    private static void MapPortal(RouteGroupBuilder portal)
    {
        portal.MapGet("/", async (ClientContentHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(cancellationToken)))
            .RequirePortal()
            .Produces<IReadOnlyList<ClientContentPost>>()
            .WithSummary("Portal: the posts sent to the client, by planned day");

        portal.MapGet("/{id:guid}", async (Guid id, ClientContentHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.GetAsync(id, cancellationToken)))
            .RequirePortal()
            .Produces<ClientContentPost>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Portal: one post");

        portal.MapPost("/{id:guid}/approve", async (Guid id, ClientContentHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.DecideAsync(id, approve: true, null, cancellationToken)))
            .RequirePortal()
            .Produces<ClientContentPost>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Portal: approve the post");

        portal.MapPost("/{id:guid}/request-changes", async (Guid id, RequestChangesCommand command, ClientContentHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.DecideAsync(id, approve: false, command.Note, cancellationToken)))
            .RequirePortal()
            .Validate<RequestChangesCommand>()
            .Produces<ClientContentPost>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Portal: ask for changes, saying what");
    }

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
