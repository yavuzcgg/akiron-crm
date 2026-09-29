using System.Security.Claims;
using System.Text;
using Akiron.BuildingBlocks.Security;
using Akiron.Modules.Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Akiron.Modules.Identity.Security;

/// <summary>Signs short-lived HS256 access tokens carrying the tenant, role and permissions (ADR-0007).</summary>
internal sealed class AccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JsonWebTokenHandler _handler = new();

    public string Issue(User user, Tenant tenant, Role role)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var claims = new List<Claim>
        {
            new(AkironClaimTypes.Subject, user.Id.ToString()),
            new(AkironClaimTypes.Email, user.Email),
            new(AkironClaimTypes.Name, user.FullName),
            new(AkironClaimTypes.TenantId, tenant.Id.ToString()),
            new(AkironClaimTypes.Role, role.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };
        claims.AddRange(role.Permissions.Select(permission => new Claim(AkironClaimTypes.Permission, permission)));

        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(jwt.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(SigningKey(jwt), SecurityAlgorithms.HmacSha256),
        });
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.SecretKey));
}
