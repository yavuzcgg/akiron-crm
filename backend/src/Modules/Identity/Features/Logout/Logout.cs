using Akiron.Modules.Identity.Features.RefreshSession;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Identity.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Features.Logout;

/// <summary>Signs this device out: revokes its token family. Other devices stay signed in.</summary>
internal sealed class LogoutHandler(IdentityDbContext db, TimeProvider timeProvider)
{
    public async Task HandleAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var tokenHash = RefreshTokenSecret.Hash(rawToken);
        var familyId = await db.RefreshTokens
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => (Guid?)token.FamilyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (familyId is { } family)
        {
            await RefreshSessionHandler.RevokeFamilyAsync(db, family, timeProvider.GetUtcNow(), cancellationToken);
        }
    }
}

internal static class LogoutEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) =>
        auth.MapPost("/logout", HandleAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Sign this device out");

    private static async Task<IResult> HandleAsync(LogoutHandler handler, AuthCookies cookies, HttpContext httpContext)
    {
        httpContext.Request.Cookies.TryGetValue(AuthCookies.RefreshCookieName, out var rawToken);

        await handler.HandleAsync(rawToken, httpContext.RequestAborted);
        cookies.Clear(httpContext.Response);

        return TypedResults.NoContent();
    }
}
