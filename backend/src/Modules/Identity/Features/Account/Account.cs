using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Email;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Features.RefreshSession;
using Akiron.Modules.Identity.Features.Register;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Identity.Security;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Akiron.Modules.Identity.Features.Account;

internal sealed record UpdateProfileCommand(string FullName);

internal sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator() => RuleFor(command => command.FullName).NotEmpty().MaximumLength(User.FullNameMaxLength);
}

internal sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(command => command.CurrentPassword).NotEmpty().MaximumLength(RegisterValidator.PasswordMaxLength);
        RuleFor(command => command.NewPassword).NotEmpty()
            .MinimumLength(RegisterValidator.PasswordMinLength)
            .MaximumLength(RegisterValidator.PasswordMaxLength);
    }
}

internal sealed record ForgotPasswordCommand(string Email);

internal sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(User.EmailMaxLength);
}

internal sealed record ResetPasswordCommand(string Token, string NewPassword);

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(command => command.Token).NotEmpty().MaximumLength(256);
        RuleFor(command => command.NewPassword).NotEmpty()
            .MinimumLength(RegisterValidator.PasswordMinLength)
            .MaximumLength(RegisterValidator.PasswordMaxLength);
    }
}

internal sealed record RenameWorkspaceCommand(string Name);

internal sealed class RenameWorkspaceValidator : AbstractValidator<RenameWorkspaceCommand>
{
    public RenameWorkspaceValidator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(Tenant.NameMaxLength);
}

internal sealed class UpdateProfileHandler(IdentityDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        var user = await db.Users.SingleAsync(candidate => candidate.Id == userId, cancellationToken);

        user.Rename(command.FullName);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>
/// Changes the password and signs every other device out; the device that made the change stays
/// signed in (its refresh-token family is kept).
/// </summary>
internal sealed class ChangePasswordHandler(IdentityDbContext db, ICurrentUser currentUser, IPasswordHasher passwordHasher, TimeProvider timeProvider)
{
    public async Task<Result<bool>> HandleAsync(ChangePasswordCommand command, string? currentRefreshToken, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        var user = await db.Users.SingleAsync(candidate => candidate.Id == userId, cancellationToken);

        if (!passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
        {
            return IdentityErrors.CurrentPasswordWrong;
        }

        user.ChangePassword(passwordHasher.Hash(command.NewPassword));
        await db.SaveChangesAsync(cancellationToken);

        var keepFamily = currentRefreshToken is null
            ? (Guid?)null
            : await db.RefreshTokens
                .Where(token => token.TokenHash == RefreshTokenSecret.Hash(currentRefreshToken))
                .Select(token => (Guid?)token.FamilyId)
                .FirstOrDefaultAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.FamilyId != keepFamily)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);

        return true;
    }
}

/// <summary>
/// Sends a reset link if the address has an account. Answers the same either way, so the form
/// cannot be used to find out who is registered.
/// </summary>
internal sealed class ForgotPasswordHandler(IdentityDbContext db, IEmailSender emailSender, IConfiguration configuration, TimeProvider timeProvider)
{
    public async Task HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email);
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email && candidate.IsActive, cancellationToken);
        if (user is null)
        {
            return;
        }

        var (rawToken, tokenHash) = RefreshTokenSecret.Create();
        db.PasswordResetTokens.Add(PasswordResetToken.Create(user.Id, tokenHash, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);

        var link = $"{configuration["App:WebUrl"]?.TrimEnd('/') ?? "http://localhost:3100"}/reset-password?token={rawToken}";
        await emailSender.SendAsync(
            new EmailMessage(
                user.Email,
                "Akiron CRM şifre sıfırlama",
                $"""
                Merhaba {user.FullName},

                Şifrenizi sıfırlamak için: {link}
                Bağlantı 1 saat geçerlidir. Bu isteği siz yapmadıysanız bu e-postayı yok sayın.

                ---
                Reset your Akiron CRM password: {link}
                The link is valid for one hour. If you did not ask for this, ignore this e-mail.
                """),
            cancellationToken);
    }
}

/// <summary>Sets the new password, spends every open reset link and signs every device out.</summary>
internal sealed class ResetPasswordHandler(IdentityDbContext db, IPasswordHasher passwordHasher, TimeProvider timeProvider)
{
    public async Task<Result<bool>> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var tokenHash = RefreshTokenSecret.Hash(command.Token);
        var reset = await db.PasswordResetTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        if (reset is null || !reset.IsUsableAt(now))
        {
            return IdentityErrors.ResetLinkInvalid;
        }

        var user = await db.Users.SingleAsync(candidate => candidate.Id == reset.UserId, cancellationToken);
        user.ChangePassword(passwordHasher.Hash(command.NewPassword));
        reset.MarkUsed(now);
        await db.SaveChangesAsync(cancellationToken);

        await db.PasswordResetTokens
            .Where(token => token.UserId == user.Id && token.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.UsedAt, now), cancellationToken);
        await db.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);

        return true;
    }
}

internal sealed class RenameWorkspaceHandler(IdentityDbContext db, ITenantContext tenantContext, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<Result<bool>> HandleAsync(RenameWorkspaceCommand command, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.SingleAsync(candidate => candidate.Id == tenantContext.TenantId, cancellationToken);
        var oldName = tenant.Name;
        tenant.Rename(command.Name);

        if (tenant.Name != oldName)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
            db.Publish(new WorkspaceRenamed(tenant.Id, timeProvider.GetUtcNow(), oldName, tenant.Name, userId.Value, currentUser.DisplayName));
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

internal static class AccountEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints, IEndpointRouteBuilder auth)
    {
        // The caller's own account: any signed-in person may change their own name and password.
        endpoints.MapPut("/me/profile", async (UpdateProfileCommand command, UpdateProfileHandler handler, CancellationToken cancellationToken) =>
                ToResult(await handler.HandleAsync(command, cancellationToken)))
            .RequireAuthorization()
            .Validate<UpdateProfileCommand>()
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Change your display name");

        // Under /auth on purpose: the refresh cookie is scoped to that path, and it tells us which
        // device is making the change so that one stays signed in.
        auth.MapPut("/password", async (ChangePasswordCommand command, ChangePasswordHandler handler, HttpContext httpContext) =>
            {
                httpContext.Request.Cookies.TryGetValue(AuthCookies.RefreshCookieName, out var refreshToken);
                return ToResult(await handler.HandleAsync(command, refreshToken, httpContext.RequestAborted));
            })
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<ChangePasswordCommand>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Change your password; other devices are signed out");

        endpoints.MapPut("/workspace", async (RenameWorkspaceCommand command, RenameWorkspaceHandler handler, CancellationToken cancellationToken) =>
                ToResult(await handler.HandleAsync(command, cancellationToken)))
            .RequirePermission(IdentityPermissions.TenantManage)
            .Validate<RenameWorkspaceCommand>()
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Rename the organisation");

        auth.MapPost("/forgot-password", async (ForgotPasswordCommand command, ForgotPasswordHandler handler, CancellationToken cancellationToken) =>
            {
                await handler.HandleAsync(command, cancellationToken);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<ForgotPasswordCommand>()
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("E-mail a password reset link (answers the same whether or not the address is registered)");

        auth.MapPost("/reset-password", async (ResetPasswordCommand command, ResetPasswordHandler handler, CancellationToken cancellationToken) =>
                ToResult(await handler.HandleAsync(command, cancellationToken)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Validate<ResetPasswordCommand>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Choose a new password with a reset link; every device is signed out");
    }

    private static IResult ToResult(Result<bool> result) => result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
}
