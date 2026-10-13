using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Portal.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.Modules.Portal.Features;

internal static class PortalEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapStaff(endpoints);
        MapAccess(endpoints.MapGroup("/auth"));

        endpoints.MapGet("/me", async (PortalAccessHandler handler, IPortalVisitor visitor, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.MeAsync(visitor, cancellationToken)))
            .RequirePortal()
            .Produces<PortalMeResponse>()
            .WithSummary("Portal: who is signed in, whose portal it is, and how to pay");
    }

    private static void MapStaff(IEndpointRouteBuilder endpoints)
    {
        var users = endpoints.MapGroup("/users");

        users.MapGet("/", async (Guid? partyId, PortalUsersHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(partyId, cancellationToken)))
            .RequirePermission(PortalPermissions.Read)
            .Produces<IReadOnlyList<PortalUserResponse>>()
            .WithSummary("Who has portal access, optionally for one party");

        users.MapPost("/", async (InvitePortalUserCommand command, PortalUsersHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.InviteAsync(command, cancellationToken)))
            .RequirePermission(PortalPermissions.Manage)
            .Validate<InvitePortalUserCommand>()
            .Produces<PortalInvitationResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Give a client contact portal access and e-mail the sign-in link");

        users.MapPost("/{id:guid}/invite", async (Guid id, PortalUsersHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ResendAsync(id, cancellationToken)))
            .RequirePermission(PortalPermissions.Manage)
            .Produces<PortalInvitationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Send a fresh sign-in link");

        users.MapPost("/{id:guid}/disable", async (Guid id, PortalUsersHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.SetAccessAsync(id, enabled: false, cancellationToken)))
            .RequirePermission(PortalPermissions.Manage)
            .Produces<PortalUserResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Switch access off; open sessions end at once");

        users.MapPost("/{id:guid}/enable", async (Guid id, PortalUsersHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.SetAccessAsync(id, enabled: true, cancellationToken)))
            .RequirePermission(PortalPermissions.Manage)
            .Produces<PortalUserResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Switch access back on");

        endpoints.MapGet("/settings", async (PortalUsersHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.SettingsAsync(cancellationToken)))
            .RequirePermission(PortalPermissions.Read)
            .Produces<PortalSettingsResponse>()
            .WithSummary("The greeting and payment instructions clients see");

        endpoints.MapPut("/settings", async (PortalSettingsCommand command, PortalUsersHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.SaveSettingsAsync(command, cancellationToken)))
            .RequirePermission(PortalPermissions.Manage)
            .Validate<PortalSettingsCommand>()
            .Produces<PortalSettingsResponse>()
            .WithSummary("Change the greeting and payment instructions");
    }

    /// <summary>Signing in and out. Anonymous by nature; rate-limited against guessing and mail flooding.</summary>
    private static void MapAccess(RouteGroupBuilder auth)
    {
        auth.MapPost("/request-link", async (RequestLinkCommand command, PortalAccessHandler handler, CancellationToken cancellationToken) =>
            {
                await handler.RequestLinkAsync(command, cancellationToken);

                // The same answer whether or not the address is known.
                return Results.NoContent();
            })
            .AsPublicLink()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<RequestLinkCommand>()
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("E-mail a sign-in link to a portal user");

        auth.MapPost("/enter", async (EnterPortalCommand command, PortalAccessHandler handler, PortalCookies cookies, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var result = await handler.EnterAsync(command, cancellationToken);
                if (!result.IsSuccess)
                {
                    return result.Error.ToProblem();
                }

                cookies.Write(httpContext.Response, result.Value.SessionSecret, result.Value.ExpiresAt);
                return Results.NoContent();
            })
            .AsPublicLink()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<EnterPortalCommand>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Trade a sign-in link for a portal session");

        auth.MapPost("/logout", async (PortalAccessHandler handler, PortalCookies cookies, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                httpContext.Request.Cookies.TryGetValue(PortalCookies.SessionCookieName, out var secret);
                await handler.LeaveAsync(secret, cancellationToken);
                cookies.Clear(httpContext.Response);
                return Results.NoContent();
            })
            .RequirePortal()
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("End the portal session");
    }

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
