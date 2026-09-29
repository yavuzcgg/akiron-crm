using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Email;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Identity.Security;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Akiron.Modules.Identity.Features.Invitations;

/// <param name="Role">A role name in the organisation (<c>admin</c>, <c>member</c> or a custom one); not <c>owner</c>.</param>
internal sealed record CreateInvitationCommand(string Email, string Role);

internal sealed class CreateInvitationValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(User.EmailMaxLength);
        RuleFor(command => command.Role).NotEmpty().MaximumLength(Role.NameMaxLength);
    }
}

internal sealed class CreateInvitationHandler(
    IdentityDbContext db,
    ICurrentUser currentUser,
    IEmailSender emailSender,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    public async Task<Result<InvitationResponse>> HandleAsync(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var email = User.NormalizeEmail(command.Email);
        var inviterId = currentUser.UserId ?? throw new InvalidOperationException("Invitations need a signed-in user.");

        if (command.Role == SystemRoles.Owner)
        {
            return IdentityErrors.OwnerRoleNotInvitable;
        }

        var role = await db.Roles.FirstOrDefaultAsync(candidate => candidate.Name == command.Role, cancellationToken);
        if (role is null)
        {
            return IdentityErrors.RoleNotFound;
        }

        var alreadyMember = await db.Memberships
            .Join(db.Users, membership => membership.UserId, user => user.Id, (membership, user) => user.Email)
            .AnyAsync(memberEmail => memberEmail == email, cancellationToken);
        if (alreadyMember)
        {
            return IdentityErrors.AlreadyMember;
        }

        // Re-inviting replaces the open invitation, so the newest link is the only one that works.
        var open = await db.Invitations
            .Where(invitation => invitation.Email == email && invitation.AcceptedAt == null && invitation.RevokedAt == null)
            .ToListAsync(cancellationToken);
        open.ForEach(invitation => invitation.Revoke(now));
        await db.SaveChangesAsync(cancellationToken);

        var inviter = await db.Users.SingleAsync(user => user.Id == inviterId, cancellationToken);
        var tenant = await db.Tenants.SingleAsync(candidate => candidate.Id == role.TenantId, cancellationToken);
        var (rawToken, tokenHash) = RefreshTokenSecret.Create();
        var created = Invitation.Create(role.TenantId, email, role.Id, tokenHash, inviterId, now);

        db.Invitations.Add(created);
        db.Publish(new InvitationSent(role.TenantId, now, created.Id.Value, email, role.Name, inviterId.Value, inviter.FullName));
        await db.SaveChangesAsync(cancellationToken);

        await emailSender.SendAsync(InvitationEmail(email, tenant.Name, inviter.FullName, rawToken), cancellationToken);

        return new InvitationResponse(created.Id.Value, email, role.Name, "pending", created.ExpiresAt, created.CreatedAt, inviter.FullName);
    }

    private EmailMessage InvitationEmail(string to, string workspace, string inviter, string token)
    {
        var webUrl = configuration["App:WebUrl"]?.TrimEnd('/') ?? "http://localhost:3100";
        var link = $"{webUrl}/invite?token={token}";

        return new EmailMessage(
            to,
            $"{inviter} sizi {workspace} çalışma alanına davet etti",
            $"""
            Merhaba,

            {inviter} sizi Akiron CRM'de "{workspace}" çalışma alanına davet etti.
            Daveti kabul etmek için: {link}

            Bağlantı 7 gün geçerlidir.

            ---
            {inviter} invited you to the "{workspace}" workspace on Akiron CRM.
            Accept the invitation: {link}
            """);
    }
}

internal static class CreateInvitationEndpoint
{
    public static void Map(IEndpointRouteBuilder invitations) =>
        invitations.MapPost("/", HandleAsync)
            .RequirePermission(IdentityPermissions.MembersManage)
            .Validate<CreateInvitationCommand>()
            .Produces<InvitationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Invite someone to the organisation by e-mail");

    private static async Task<IResult> HandleAsync(
        CreateInvitationCommand command,
        CreateInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/identity/invitations/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }
}
