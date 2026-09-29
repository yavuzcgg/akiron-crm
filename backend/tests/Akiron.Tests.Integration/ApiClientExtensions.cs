using System.Net.Http.Json;
using System.Text.Json;

namespace Akiron.Tests.Integration;

/// <summary>Small helpers so tests read as the HTTP conversation they check.</summary>
internal static class ApiClientExtensions
{
    public const string Password = "Guclu-Parola-2026";

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static Task<HttpResponseMessage> RegisterAsync(
        this HttpClient client,
        string? email = null,
        string organizationName = "Çelik Ajans İletişim",
        string password = Password) =>
        client.PostAsJsonAsync("/api/v1/identity/auth/register", new
        {
            organizationName,
            fullName = "Yavuz Çelik",
            email = email ?? UniqueEmail(),
            password,
        });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/v1/identity/auth/login", new { email, password });

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client) =>
        client.PostAsync(new Uri("/api/v1/identity/auth/refresh", UriKind.Relative), content: null);

    /// <summary>The <c>code</c> of a ProblemDetails body (ADR-0006).</summary>
    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>The raw value of a cookie the response set, e.g. to replay a stolen refresh token.</summary>
    public static string? SetCookieValue(this HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
                .Select(header => header.Split(';')[0])
                .Where(pair => pair.StartsWith(name + "=", StringComparison.Ordinal))
                .Select(pair => pair[(name.Length + 1)..])
                .FirstOrDefault()
            : null;
}
