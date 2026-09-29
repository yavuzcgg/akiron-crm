using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Akiron.Modules.Identity.Security;

/// <summary>
/// Tokens never appear in response bodies; they travel in HttpOnly cookies scoped as narrowly as
/// possible (akiron-seo design). A third, readable cookie only tells the web app's route guard that
/// a session probably exists; it grants nothing.
/// </summary>
internal sealed class AuthCookies(IOptions<JwtOptions> options, IConfiguration configuration, IHostEnvironment environment)
{
    public const string AccessCookieName = "akiron_access";
    public const string RefreshCookieName = "akiron_refresh";
    public const string SessionHintCookieName = "akiron_session";

    private const string AccessCookiePath = "/api";
    private const string RefreshCookiePath = "/api/v1/identity/auth";
    private const string SessionHintPath = "/";

    public void Write(HttpResponse response, SessionTokens tokens, DateTimeOffset now)
    {
        var jwt = options.Value;
        var refreshExpires = now.AddDays(jwt.RefreshTokenDays);

        response.Cookies.Append(AccessCookieName, tokens.AccessToken,
            Options(AccessCookiePath, now.AddMinutes(jwt.AccessTokenMinutes), httpOnly: true));
        response.Cookies.Append(RefreshCookieName, tokens.RefreshToken,
            Options(RefreshCookiePath, refreshExpires, httpOnly: true));
        response.Cookies.Append(SessionHintCookieName, "1",
            Options(SessionHintPath, refreshExpires, httpOnly: false));
    }

    public void Clear(HttpResponse response)
    {
        response.Cookies.Delete(AccessCookieName, Options(AccessCookiePath, DateTimeOffset.UnixEpoch, httpOnly: true));
        response.Cookies.Delete(RefreshCookieName, Options(RefreshCookiePath, DateTimeOffset.UnixEpoch, httpOnly: true));
        response.Cookies.Delete(SessionHintCookieName, Options(SessionHintPath, DateTimeOffset.UnixEpoch, httpOnly: false));
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

/// <summary>What a successful sign-in hands to the cookie writer.</summary>
internal sealed record SessionTokens(string AccessToken, string RefreshToken);
