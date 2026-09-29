using System.Text;

namespace Akiron.Api;

/// <summary>
/// Refuses to start with missing, weak or development secrets outside Development (akiron-seo
/// design). Booting production with the key committed in appsettings.Development.json would let
/// anyone with the repository forge sessions for any tenant.
/// </summary>
internal static class SecretsValidator
{
    /// <summary>HS256 needs at least 256 bits of key material.</summary>
    private const int MinimumJwtKeyBytes = 32;

    private const string DevelopmentMarker = "dev-only";

    public static void ValidateSecrets(this IConfiguration configuration, IHostEnvironment environment)
    {
        var failures = new List<string>();
        var relaxed = environment.IsDevelopment() || environment.IsEnvironment("Testing");

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
        {
            failures.Add("ConnectionStrings__Default is not set.");
        }

        var jwtSecret = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            failures.Add("Jwt__SecretKey is not set.");
        }
        else if (Encoding.UTF8.GetByteCount(jwtSecret) < MinimumJwtKeyBytes)
        {
            failures.Add($"Jwt__SecretKey must be at least {MinimumJwtKeyBytes} bytes.");
        }
        else if (!relaxed && jwtSecret.Contains(DevelopmentMarker, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Jwt__SecretKey is the development placeholder; supply a real secret.");
        }

        if (!relaxed && configuration.GetValue<bool?>("Auth:CookieSecure") == false)
        {
            failures.Add("Auth__CookieSecure may be false only in Development or Testing.");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Startup aborted: invalid configuration for the '{environment.EnvironmentName}' environment:{Environment.NewLine}" +
                string.Join(Environment.NewLine, failures.Select(failure => $"  - {failure}")));
        }
    }
}
