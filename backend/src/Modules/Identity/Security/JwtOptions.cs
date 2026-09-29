namespace Akiron.Modules.Identity.Security;

/// <summary>Bound from the <c>Jwt</c> configuration section. The secret comes from the environment outside Development.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "akiron-crm";

    public string Audience { get; set; } = "akiron-crm-web";

    public int AccessTokenMinutes { get; set; } = 30;

    public int RefreshTokenDays { get; set; } = 14;
}
