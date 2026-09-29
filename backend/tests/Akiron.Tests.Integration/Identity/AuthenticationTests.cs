using System.Net;
using System.Net.Http.Json;
using System.Text;
using Akiron.Modules.Identity.Features.Sessions;

namespace Akiron.Tests.Integration.Identity;

public sealed class AuthenticationTests(ApiFixture api)
{
    private static readonly Uri SessionUri = new("/api/v1/identity/auth/session", UriKind.Relative);

    [Fact]
    public async Task Register_WithValidRequest_SignsTheOwnerIn()
    {
        using var client = api.CreateClient();
        var email = ApiClientExtensions.UniqueEmail();

        using var response = await client.RegisterAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        Assert.Equal(email, session.Email);
        Assert.Equal("Çelik Ajans İletişim", session.TenantName);
        Assert.Equal("owner", session.Role);
        Assert.Equal(["*"], session.Permissions);

        // Tokens travel only in cookies, and the cookies are enough for the next request.
        Assert.NotNull(response.SetCookieValue("akiron_access"));
        Assert.NotNull(response.SetCookieValue("akiron_refresh"));
        using var sessionResponse = await client.GetAsync(SessionUri, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsEmailTaken()
    {
        using var client = api.CreateClient();
        var email = ApiClientExtensions.UniqueEmail();
        using var first = await client.RegisterAsync(email);

        using var second = await client.RegisterAsync(email, organizationName: "Başka Ajans");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("identity.user.email_taken", await second.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Register_WithInvalidFields_ReturnsFieldCodes()
    {
        using var client = api.CreateClient();

        using var response = await client.RegisterAsync(email: "not-an-email", organizationName: "", password: "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"code\":\"common.validation.failed\"", body, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"validation.required\"", body, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"validation.email\"", body, StringComparison.Ordinal);
        Assert.Contains("\"minLength\":10", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_WithUndecodableBody_ReturnsMalformedRequest()
    {
        using var client = api.CreateClient();

        // "Ç" in Windows-1254 instead of UTF-8: what a misconfigured client actually sent during development.
        using var content = new ByteArrayContent([(byte)'{', (byte)'"', 0xC7, (byte)'"', (byte)'}']);
        content.Headers.ContentType = new("application/json") { CharSet = Encoding.UTF8.WebName };
        using var response = await client.PostAsync(new Uri("/api/v1/identity/auth/register", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("common.request.malformed", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsInvalidCredentials()
    {
        using var client = api.CreateClient();
        var email = ApiClientExtensions.UniqueEmail();
        using var _ = await client.RegisterAsync(email);

        using var response = await api.CreateClient().LoginAsync(email, "yanlis-parola-123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("identity.auth.invalid_credentials", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsTheSameErrorAsAWrongPassword()
    {
        using var response = await api.CreateClient().LoginAsync(ApiClientExtensions.UniqueEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("identity.auth.invalid_credentials", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Session_WithoutCookies_ReturnsUnauthenticated()
    {
        using var response = await api.CreateClient().GetAsync(SessionUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("common.auth.unauthenticated", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Refresh_WithAReusedToken_RevokesTheWholeFamily()
    {
        using var client = api.CreateClient();
        using var registered = await client.RegisterAsync();
        var stolen = registered.SetCookieValue("akiron_refresh");

        using var rotated = await client.RefreshAsync();
        Assert.Equal(HttpStatusCode.NoContent, rotated.StatusCode);

        // An attacker replays the token the browser has already used.
        using var attacker = api.CreateClient();
        attacker.DefaultRequestHeaders.Add("Cookie", $"akiron_refresh={stolen}");
        using var replay = await attacker.RefreshAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("identity.auth.session_expired", await replay.ReadErrorCodeAsync());

        // The legitimate browser's newer token belonged to the same family, so it is gone too.
        using var victim = await client.RefreshAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, victim.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        using var client = api.CreateClient();
        using var _ = await client.RegisterAsync();

        using var logout = await client.PostAsync(new Uri("/api/v1/identity/auth/logout", UriKind.Relative), null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var refresh = await client.RefreshAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Login_OnASecondDevice_KeepsTheFirstSignedIn()
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var laptop = api.CreateClient();
        using var _ = await laptop.RegisterAsync(email);

        using var phone = api.CreateClient();
        using var login = await phone.LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var laptopRefresh = await laptop.RefreshAsync();
        Assert.Equal(HttpStatusCode.NoContent, laptopRefresh.StatusCode);
    }
}
