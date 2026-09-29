using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Features.GetSession;

/// <summary>
/// The signed-in user in the current tenant, read fresh from the database rather than from the
/// token, so a renamed organisation or changed role shows up without signing in again.
/// </summary>
internal sealed class GetSessionHandler(IdentityDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<SessionResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return IdentityErrors.SessionExpired;
        }

        // Memberships and roles are tenant-filtered: only the current tenant's row can match.
        var session = await db.Memberships
            .Where(membership => membership.UserId == userId && membership.IsActive)
            .Join(db.Users, membership => membership.UserId, user => user.Id, (membership, user) => new { membership, user })
            .Join(db.Tenants, pair => pair.membership.TenantId, tenant => tenant.Id, (pair, tenant) => new { pair.membership, pair.user, tenant })
            .Join(db.Roles, triple => triple.membership.RoleId, role => role.Id, (triple, role) => new SessionResponse(
                triple.user.Id.Value,
                triple.user.Email,
                triple.user.FullName,
                triple.tenant.Id.Value,
                triple.tenant.Name,
                role.Name,
                role.Permissions))
            .FirstOrDefaultAsync(cancellationToken);

        return session is null ? IdentityErrors.SessionExpired : session;
    }
}

internal static class GetSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) =>
        auth.MapGet("/session", HandleAsync)
            .RequireAuthorization()
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("The signed-in user, organisation, role and permissions");

    private static async Task<IResult> HandleAsync(GetSessionHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
