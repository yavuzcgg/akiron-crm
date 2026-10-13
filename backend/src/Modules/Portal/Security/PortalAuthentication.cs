using System.Security.Claims;
using System.Text.Encodings.Web;
using Akiron.BuildingBlocks.Security;
using Akiron.Modules.Portal.Domain;
using Akiron.Modules.Portal.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akiron.Modules.Portal.Security;

/// <summary>
/// The portal's cookies, apart from the staff ones (ADR-0007): an HttpOnly session secret sent to
/// the API, and a readable hint that only tells the web app's route guard a session probably exists.
/// </summary>
internal sealed class PortalCookies(IConfiguration configuration, IHostEnvironment environment)
{
    public const string SessionCookieName = "akiron_portal";
    public const string HintCookieName = "akiron_portal_session";

    public void Write(HttpResponse response, string secret, DateTimeOffset expires)
    {
        response.Cookies.Append(SessionCookieName, secret, Options("/api", expires, httpOnly: true));
        response.Cookies.Append(HintCookieName, "1", Options("/", expires, httpOnly: false));
    }

    public void Clear(HttpResponse response)
    {
        response.Cookies.Delete(SessionCookieName, Options("/api", DateTimeOffset.UnixEpoch, httpOnly: true));
        response.Cookies.Delete(HintCookieName, Options("/", DateTimeOffset.UnixEpoch, httpOnly: false));
    }

    private CookieOptions Options(string path, DateTimeOffset expires, bool httpOnly) => new()
    {
        HttpOnly = httpOnly,
        Secure = configuration.GetValue<bool?>("Auth:CookieSecure") ?? !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = path,
        Expires = expires,
    };
}

/// <summary>
/// Turns the portal cookie into a principal: the session is looked up by its hash across tenants
/// (no tenant is known yet), and must be unrevoked, unexpired and belong to an active portal user.
/// The principal carries the tenant and the party; nothing a staff policy would accept.
/// </summary>
internal sealed class PortalAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    PortalDbContext db,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(PortalCookies.SessionCookieName, out var secret) || string.IsNullOrEmpty(secret) || secret.Length > 128)
        {
            return AuthenticateResult.NoResult();
        }

        var hash = PortalSecret.Hash(secret);
        var now = timeProvider.GetUtcNow();
        var found = await db.Sessions.IgnoreQueryFilters().AsNoTracking()
            .Where(session => session.TokenHash == hash && session.RevokedAt == null && session.ExpiresAt > now)
            .Join(db.Users.IgnoreQueryFilters(), session => session.PortalUserId, user => user.Id, (session, user) => user)
            .FirstOrDefaultAsync(user => user.Status == PortalUserStatus.Active, Context.RequestAborted);
        if (found is null)
        {
            return AuthenticateResult.Fail("The portal session is not valid.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(PortalAuth.UserIdClaim, found.Id.Value.ToString()),
                new Claim(PortalAuth.PartyIdClaim, found.PartyId.ToString()),
                new Claim(AkironClaimTypes.TenantId, found.TenantId.Value.ToString()),
                new Claim(AkironClaimTypes.Name, found.FullName),
                new Claim(AkironClaimTypes.Email, found.Email),
            ],
            PortalAuth.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), PortalAuth.Scheme));
    }
}
