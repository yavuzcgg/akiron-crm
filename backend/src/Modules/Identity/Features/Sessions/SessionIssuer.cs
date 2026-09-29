using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Identity.Security;
using Microsoft.Extensions.Options;

namespace Akiron.Modules.Identity.Features.Sessions;

/// <summary>A new session: the body for the client and the tokens for the cookies.</summary>
internal sealed record IssuedSession(SessionResponse Session, SessionTokens Tokens, string RefreshTokenHash);

/// <summary>
/// Creates the access token and the next refresh token for a user in a tenant. Adds the refresh
/// token row to the context; the caller saves, so the session and the rest of its unit of work
/// commit together.
/// </summary>
internal sealed class SessionIssuer(
    IdentityDbContext db,
    AccessTokenIssuer accessTokens,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
{
    /// <param name="familyId">The device's token family when refreshing; a new family on sign-in.</param>
    public IssuedSession Issue(User user, Tenant tenant, Role role, Guid? familyId = null)
    {
        var (rawRefreshToken, refreshTokenHash) = RefreshTokenSecret.Create();

        db.RefreshTokens.Add(RefreshToken.Create(
            user.Id,
            tenant.Id,
            refreshTokenHash,
            familyId ?? Guid.CreateVersion7(),
            timeProvider.GetUtcNow(),
            TimeSpan.FromDays(options.Value.RefreshTokenDays)));

        var session = new SessionResponse(
            user.Id.Value,
            user.Email,
            user.FullName,
            tenant.Id.Value,
            tenant.Name,
            role.Name,
            role.Permissions);

        return new IssuedSession(session, new SessionTokens(accessTokens.Issue(user, tenant, role), rawRefreshToken), refreshTokenHash);
    }
}
