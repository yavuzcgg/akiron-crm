using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.Modules.Identity.Features.ListMembers;

internal sealed record MemberResponse(Guid UserId, string FullName, string Email, string Role, bool IsActive, DateTimeOffset JoinedAt);

/// <summary>People in the current organisation. No tenant condition here: the query filter supplies it.</summary>
internal sealed class ListMembersHandler(IdentityDbContext db)
{
    public Task<PagedResult<MemberResponse>> HandleAsync(PageRequest page, CancellationToken cancellationToken) =>
        db.Memberships
            .Join(db.Users, membership => membership.UserId, user => user.Id, (membership, user) => new { membership, user })
            .Join(db.Roles, pair => pair.membership.RoleId, role => role.Id, (pair, role) => new { pair.membership, pair.user, role })
            .OrderBy(row => row.user.FullName)
            .ThenBy(row => row.user.Email)
            .Select(row => new MemberResponse(
                row.user.Id.Value,
                row.user.FullName,
                row.user.Email,
                row.role.Name,
                row.membership.IsActive,
                row.membership.CreatedAt))
            .ToPagedResultAsync(page, cancellationToken);
}

internal static class ListMembersEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/members", HandleAsync)
            .RequirePermission(IdentityPermissions.MembersRead)
            .Validate<PageRequest>()
            .Produces<PagedResult<MemberResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("People in the current organisation");

    private static async Task<IResult> HandleAsync(
        [AsParameters] PageRequest page,
        ListMembersHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(page, cancellationToken));
}
