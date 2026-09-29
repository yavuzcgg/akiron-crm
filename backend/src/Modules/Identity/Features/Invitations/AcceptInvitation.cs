using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Features.Register;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Identity.Security;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Features.Invitations;

/// <summary>
/// Anonymous: the token is the credential for the invitation, the password is the credential for
/// the account. Invitations are read across tenants here because no tenant is bound yet.
/// </summary>
internal sealed class InvitationLookup(IdentityDbContext db, TimeProvider timeProvider)
{
    public async Task<(Invitation Invitation, Tenant Tenant, Role Role, User Inviter)?> FindPendingAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var tokenHash = RefreshTokenSecret.Hash(rawToken);
        var row = await db.Invitations.IgnoreQueryFilters()
            .Where(invitation => invitation.TokenHash == tokenHash)
            .Join(db.Tenants, invitation => invitation.TenantId, tenant => tenant.Id, (invitation, tenant) => new { invitation, tenant })
            .Join(db.Roles.IgnoreQueryFilters(), pair => pair.invitation.RoleId, role => role.Id, (pair, role) => new { pair.invitation, pair.tenant, role })
            .Join(db.Users, triple => triple.invitation.InvitedBy, user => user.Id, (triple, inviter) => new { triple.invitation, triple.tenant, triple.role, inviter })
            .FirstOrDefaultAsync(cancellationToken);

        return row is not null && row.invitation.StatusAt(timeProvider.GetUtcNow()) == InvitationStatus.Pending
            ? (row.invitation, row.tenant, row.role, row.inviter)
            : null;
    }
}

internal sealed class PreviewInvitationHandler(IdentityDbContext db, InvitationLookup lookup)
{
    public async Task<Result<InvitationPreviewResponse>> HandleAsync(string? token, CancellationToken cancellationToken)
    {
        if (await lookup.FindPendingAsync(token, cancellationToken) is not { } found)
        {
            return IdentityErrors.InvitationInvalid;
        }

        var accountExists = await db.Users.AnyAsync(user => user.Email == found.Invitation.Email, cancellationToken);
        return new InvitationPreviewResponse(
            found.Tenant.Name,
            found.Invitation.Email,
            found.Role.Name,
            found.Inviter.FullName,
            accountExists,
            found.Invitation.ExpiresAt);
    }
}

/// <param name="FullName">Required only when the address has no account yet.</param>
internal sealed record AcceptInvitationCommand(string Token, string Password, string? FullName = null);

internal sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(command => command.Token).NotEmpty().MaximumLength(256);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(RegisterValidator.PasswordMaxLength);
        RuleFor(command => command.FullName).MaximumLength(User.FullNameMaxLength);
    }
}

internal sealed class AcceptInvitationHandler(
    IdentityDbContext db,
    InvitationLookup lookup,
    IPasswordHasher passwordHasher,
    SessionIssuer sessionIssuer,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task<Result<IssuedSession>> HandleAsync(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        if (await lookup.FindPendingAsync(command.Token, cancellationToken) is not { } found)
        {
            return IdentityErrors.InvitationInvalid;
        }

        var (invitation, tenant, role, inviter) = found;
        var now = timeProvider.GetUtcNow();
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == invitation.Email, cancellationToken);

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(command.FullName))
            {
                return IdentityErrors.FullNameRequired;
            }

            if (command.Password.Length < RegisterValidator.PasswordMinLength)
            {
                return IdentityErrors.PasswordTooShort(RegisterValidator.PasswordMinLength);
            }

            user = User.Create(invitation.Email, command.FullName, passwordHasher.Hash(command.Password));
            db.Users.Add(user);
        }
        else if (!user.IsActive || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            // An existing account joins only with its own password: holding the e-mail link is not enough.
            return IdentityErrors.InvalidCredentials;
        }

        tenantContext.Bind(tenant.Id);

        if (await db.Memberships.AnyAsync(membership => membership.UserId == user.Id, cancellationToken))
        {
            return IdentityErrors.AlreadyMember;
        }

        db.Memberships.Add(Membership.Create(tenant.Id, user.Id, role.Id));
        invitation.Accept(user.Id, now);
        user.RecordLogin(now);
        db.Publish(new MemberJoined(tenant.Id, now, user.Id.Value, user.FullName, user.Email, role.Name, inviter.Id.Value, inviter.FullName));

        var session = sessionIssuer.Issue(user, tenant, role);
        await db.SaveChangesAsync(cancellationToken);

        return session;
    }
}

internal static class AcceptInvitationEndpoints
{
    public static void Map(IEndpointRouteBuilder invitations)
    {
        invitations.MapGet("/preview", async (string token, PreviewInvitationHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(token, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces<InvitationPreviewResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Show an invitation to the person holding its link");

        invitations.MapPost("/accept", async (
                AcceptInvitationCommand command,
                AcceptInvitationHandler handler,
                AuthCookies cookies,
                TimeProvider timeProvider,
                HttpContext httpContext) =>
            {
                var result = await handler.HandleAsync(command, httpContext.RequestAborted);
                if (!result.IsSuccess)
                {
                    return result.Error.ToProblem();
                }

                cookies.Write(httpContext.Response, result.Value.Tokens, timeProvider.GetUtcNow());
                return Results.Ok(result.Value.Session);
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<AcceptInvitationCommand>()
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Accept an invitation and sign in to its organisation");
    }
}
