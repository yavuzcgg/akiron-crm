using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Identity.Security;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Features.Register;

/// <summary>Creates an organisation, its system roles and its owner, and signs the owner in.</summary>
internal sealed record RegisterCommand(string OrganizationName, string FullName, string Email, string Password);

internal sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 128;

    public RegisterValidator()
    {
        RuleFor(command => command.OrganizationName).NotEmpty().MaximumLength(Tenant.NameMaxLength);
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(User.FullNameMaxLength);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(User.EmailMaxLength);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(PasswordMinLength).MaximumLength(PasswordMaxLength);
    }
}

internal sealed class RegisterHandler(
    IdentityDbContext db,
    IPasswordHasher passwordHasher,
    SessionIssuer sessionIssuer,
    ITenantContext tenantContext,
    PermissionCatalog permissionCatalog,
    TimeProvider timeProvider)
{
    public async Task<Result<IssuedSession>> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email);

        // The unique index is the real guard (two sign-ups can race); this check just gives the
        // common case a clean answer without an exception.
        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return IdentityErrors.EmailTaken;
        }

        var tenant = Tenant.Create(command.OrganizationName);

        // Registration is the one flow that creates a tenant, so no token names it yet.
        tenantContext.Bind(tenant.Id);

        var user = User.Create(email, command.FullName, passwordHasher.Hash(command.Password));
        var roles = SystemRoles.CreateFor(tenant.Id, permissionCatalog);
        var owner = roles.Single(role => role.Name == SystemRoles.Owner);

        db.Tenants.Add(tenant);
        db.Users.Add(user);
        db.Roles.AddRange(roles);
        db.Memberships.Add(Membership.Create(tenant.Id, user.Id, owner.Id));
        db.Publish(new WorkspaceCreated(tenant.Id, timeProvider.GetUtcNow(), tenant.Name, user.Id.Value, user.FullName));

        var session = sessionIssuer.Issue(user, tenant, owner);
        await db.SaveChangesAsync(cancellationToken);

        return session;
    }
}

internal static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) =>
        auth.MapPost("/register", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<RegisterCommand>()
            .Produces<SessionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Register an organisation and its owner, and sign in");

    private static async Task<IResult> HandleAsync(
        RegisterCommand command,
        RegisterHandler handler,
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
        return TypedResults.Created("/api/v1/identity/auth/session", result.Value.Session);
    }
}
