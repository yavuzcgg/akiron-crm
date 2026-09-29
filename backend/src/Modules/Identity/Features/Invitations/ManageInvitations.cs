using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Features.Invitations;

/// <summary>Invitations that are still open or expired unused; accepted and revoked ones drop off the list.</summary>
internal sealed class ListInvitationsHandler(IdentityDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InvitationResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await db.Invitations
            .Where(invitation => invitation.AcceptedAt == null && invitation.RevokedAt == null)
            .Join(db.Roles, invitation => invitation.RoleId, role => role.Id, (invitation, role) => new { invitation, role.Name })
            .Join(db.Users, pair => pair.invitation.InvitedBy, user => user.Id, (pair, inviter) => new { pair.invitation, Role = pair.Name, Inviter = inviter.FullName })
            .OrderByDescending(row => row.invitation.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new InvitationResponse(
                row.invitation.Id.Value,
                row.invitation.Email,
                row.Role,
                row.invitation.StatusAt(now).ToApiValue(),
                row.invitation.ExpiresAt,
                row.invitation.CreatedAt,
                row.Inviter))
            .ToList();
    }
}

internal sealed class RevokeInvitationHandler(IdentityDbContext db, TimeProvider timeProvider)
{
    public async Task<Result<bool>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var invitationId = InvitationId.From(id);
        var invitation = await db.Invitations.FirstOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);
        if (invitation is null)
        {
            return IdentityErrors.InvitationNotFound;
        }

        invitation.Revoke(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

internal static class ManageInvitationsEndpoints
{
    public static void Map(IEndpointRouteBuilder invitations)
    {
        invitations.MapGet("/", async (ListInvitationsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.HandleAsync(cancellationToken)))
            .RequirePermission(IdentityPermissions.MembersRead)
            .WithSummary("Open invitations of the organisation");

        invitations.MapDelete("/{id:guid}", async (Guid id, RevokeInvitationHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
            })
            .RequirePermission(IdentityPermissions.MembersManage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Revoke an invitation");
    }
}
