using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Identity.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Features.RefreshSession;

/// <summary>
/// Trades a refresh token for a new access token and a new refresh token in the same family.
/// Reusing a revoked token revokes the family: whoever holds the other copy is signed out too.
/// </summary>
internal sealed class RefreshSessionHandler(
    IdentityDbContext db,
    SessionIssuer sessionIssuer,
    TimeProvider timeProvider)
{
    public async Task<Result<IssuedSession>> HandleAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return IdentityErrors.SessionExpired;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = RefreshTokenSecret.Hash(rawToken);
        var stored = await db.RefreshTokens.AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (stored is null)
        {
            return IdentityErrors.SessionExpired;
        }

        if (stored.RevokedAt is not null || stored.ExpiresAt <= now)
        {
            if (stored.RevokedAt is not null)
            {
                await RevokeFamilyAsync(db, stored.FamilyId, now, cancellationToken);
            }

            return IdentityErrors.SessionExpired;
        }

        // The user may have been deactivated or removed from the organisation since sign-in.
        var access = await db.Memberships.IgnoreQueryFilters()
            .Where(membership => membership.UserId == stored.UserId && membership.TenantId == stored.TenantId && membership.IsActive)
            .Join(db.Users, membership => membership.UserId, user => user.Id, (membership, user) => new { membership, user })
            .Where(pair => pair.user.IsActive)
            .Join(db.Tenants, pair => pair.membership.TenantId, tenant => tenant.Id, (pair, tenant) => new { pair.membership, pair.user, tenant })
            .Join(db.Roles.IgnoreQueryFilters(), triple => triple.membership.RoleId, role => role.Id, (triple, role) => new { triple.user, triple.tenant, role })
            .FirstOrDefaultAsync(cancellationToken);

        if (access is null)
        {
            await RevokeFamilyAsync(db, stored.FamilyId, now, cancellationToken);
            return IdentityErrors.SessionExpired;
        }

        var session = sessionIssuer.Issue(access.user, access.tenant, access.role, stored.FamilyId);

        // Claim the old token atomically: of two concurrent refreshes with the same token, only
        // one can flip RevokedAt from null. The loser is treated as reuse.
        var claimed = await db.RefreshTokens
            .Where(token => token.Id == stored.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.ReplacedByTokenHash, session.RefreshTokenHash),
                cancellationToken);

        if (claimed == 0)
        {
            db.ChangeTracker.Clear();
            await RevokeFamilyAsync(db, stored.FamilyId, now, cancellationToken);
            return IdentityErrors.SessionExpired;
        }

        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    internal static Task<int> RevokeFamilyAsync(
        IdentityDbContext db,
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
}

internal static class RefreshSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) =>
        auth.MapPost("/refresh", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Rotate the session cookies using the refresh cookie");

    private static async Task<IResult> HandleAsync(
        RefreshSessionHandler handler,
        AuthCookies cookies,
        TimeProvider timeProvider,
        HttpContext httpContext)
    {
        httpContext.Request.Cookies.TryGetValue(AuthCookies.RefreshCookieName, out var rawToken);

        var result = await handler.HandleAsync(rawToken, httpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            cookies.Clear(httpContext.Response);
            return result.Error.ToProblem();
        }

        cookies.Write(httpContext.Response, result.Value.Tokens, timeProvider.GetUtcNow());
        return TypedResults.NoContent();
    }
}
