using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Identity.Security;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Features.Login;

/// <param name="TenantId">Which organisation to sign into when the user belongs to several; the oldest membership otherwise.</param>
internal sealed record LoginCommand(string Email, string Password, Guid? TenantId = null);

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(User.EmailMaxLength);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(256);
    }
}

internal sealed class LoginHandler(
    IdentityDbContext db,
    IPasswordHasher passwordHasher,
    SessionIssuer sessionIssuer,
    TimeProvider timeProvider)
{
    public async Task<Result<IssuedSession>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email);
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        if (user is null)
        {
            passwordHasher.SimulateVerify(command.Password);
            return IdentityErrors.InvalidCredentials;
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash) || !user.IsActive)
        {
            return IdentityErrors.InvalidCredentials;
        }

        var requestedTenant = command.TenantId is { } requested ? TenantId.From(requested) : (TenantId?)null;

        // No tenant is bound before sign-in, so the tenant filter would hide every membership;
        // this is one of the auth flows allowed to look across tenants.
        var access = await db.Memberships.IgnoreQueryFilters()
            .Where(membership => membership.UserId == user.Id && membership.IsActive)
            .Where(membership => requestedTenant == null || membership.TenantId == requestedTenant)
            .OrderBy(membership => membership.CreatedAt)
            .Join(db.Tenants, membership => membership.TenantId, tenant => tenant.Id, (membership, tenant) => new { membership, tenant })
            .Join(db.Roles.IgnoreQueryFilters(), pair => pair.membership.RoleId, role => role.Id, (pair, role) => new { pair.tenant, role })
            .FirstOrDefaultAsync(cancellationToken);

        if (access is null)
        {
            return IdentityErrors.InvalidCredentials;
        }

        user.RecordLogin(timeProvider.GetUtcNow());
        var session = sessionIssuer.Issue(user, access.tenant, access.role);
        await db.SaveChangesAsync(cancellationToken);

        return session;
    }
}

internal static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) =>
        auth.MapPost("/login", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<LoginCommand>()
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Sign in with e-mail and password");

    private static async Task<IResult> HandleAsync(
        LoginCommand command,
        LoginHandler handler,
        AuthCookies cookies,
        TimeProvider timeProvider,
        HttpContext httpContext)
    {
        var result = await handler.HandleAsync(command, httpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        cookies.Write(httpContext.Response, result.Value.Tokens, timeProvider.GetUtcNow());
        return TypedResults.Ok(result.Value.Session);
    }
}
