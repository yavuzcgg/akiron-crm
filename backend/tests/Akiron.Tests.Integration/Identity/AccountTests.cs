using System.Net;
using System.Net.Http.Json;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Identity;

public sealed class AccountTests(ApiFixture api)
{
    private static readonly Uri SessionUri = new("/api/v1/identity/auth/session", UriKind.Relative);

    [Fact]
    public async Task ChangePassword_WithTheWrongCurrentPassword_IsRefused()
    {
        using var client = api.CreateClient();
        using var _ = await client.RegisterAsync();

        using var response = await client.PutAsJsonAsync("/api/v1/identity/auth/password", new { currentPassword = "yanlis-parola-1", newPassword = "Yeni-Parola-2026" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("identity.password.current_wrong", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task ChangePassword_KeepsThisDeviceAndSignsOtherDevicesOut()
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var laptop = api.CreateClient();
        using var _ = await laptop.RegisterAsync(email);
        using var phone = api.CreateClient();
        using var __ = await phone.LoginAsync(email);

        using var change = await laptop.PutAsJsonAsync("/api/v1/identity/auth/password", new { currentPassword = ApiClientExtensions.Password, newPassword = "Yeni-Parola-2026" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        using var laptopRefresh = await laptop.RefreshAsync();
        using var phoneRefresh = await phone.RefreshAsync();
        using var oldPassword = await api.CreateClient().LoginAsync(email);
        using var newPassword = await api.CreateClient().LoginAsync(email, "Yeni-Parola-2026");

        Assert.Equal(HttpStatusCode.NoContent, laptopRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, phoneRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ForAnUnknownAddress_AnswersTheSameAndSendsNothing()
    {
        var email = ApiClientExtensions.UniqueEmail();

        using var response = await api.CreateClient().PostAsJsonAsync("/api/v1/identity/auth/forgot-password", new { email }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(api.ResetTokenFor(email));
    }

    [Fact]
    public async Task ResetPassword_WithTheEmailedLink_SetsTheNewPasswordOnceAndSignsEverythingOut()
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var browser = api.CreateClient();
        using var _ = await browser.RegisterAsync(email);

        using var forgot = await api.CreateClient().PostAsJsonAsync("/api/v1/identity/auth/forgot-password", new { email = email.ToUpperInvariant() }, TestContext.Current.CancellationToken);
        var token = api.ResetTokenFor(email);
        Assert.NotNull(token);

        using var reset = await api.CreateClient().PostAsJsonAsync("/api/v1/identity/auth/reset-password", new { token, newPassword = "Sifirlanmis-2026" }, TestContext.Current.CancellationToken);
        using var reuse = await api.CreateClient().PostAsJsonAsync("/api/v1/identity/auth/reset-password", new { token, newPassword = "Baska-Parola-2026" }, TestContext.Current.CancellationToken);
        using var oldSession = await browser.RefreshAsync();
        using var login = await api.CreateClient().LoginAsync(email, "Sifirlanmis-2026");

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal("identity.password.reset_invalid", await reuse.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task RenameWorkspace_AsOwner_ChangesTheNameAndWritesTheTimeline()
    {
        using var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync(organizationName: "Lord of the Mysteries");
        var session = await registered.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);

        using var rename = await owner.PutAsJsonAsync("/api/v1/identity/workspace", new { name = "Akiron HQ" }, TestContext.Current.CancellationToken);
        await api.DeliverOutboxAsync();
        var after = await owner.GetFromJsonAsync<SessionResponse>(SessionUri, TestContext.Current.CancellationToken);
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/workspace/{session!.TenantId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, rename.StatusCode);
        Assert.Equal("Akiron HQ", after!.TenantName);
        var entry = timeline!.Items[0];
        Assert.Equal("identity.workspace.renamed", entry.Type);
        Assert.Equal("Lord of the Mysteries", entry.Payload.GetProperty("oldName").GetString());
    }

    [Fact]
    public async Task RenameWorkspace_AsAdmin_IsForbidden()
    {
        using var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        var adminEmail = ApiClientExtensions.UniqueEmail();
        using var __ = await owner.InviteAsync(adminEmail, role: "admin");
        using var admin = api.CreateClient();
        using var ___ = await admin.AcceptInvitationAsync(api.InvitationTokenFor(adminEmail));

        using var rename = await admin.PutAsJsonAsync("/api/v1/identity/workspace", new { name = "Ele geçirildi" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesTheNameShownInTheSession()
    {
        using var client = api.CreateClient();
        using var _ = await client.RegisterAsync();

        using var update = await client.PutAsJsonAsync("/api/v1/identity/me/profile", new { fullName = "Yavuz Selim Çelik" }, TestContext.Current.CancellationToken);
        var session = await client.GetFromJsonAsync<SessionResponse>(SessionUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal("Yavuz Selim Çelik", session!.FullName);
    }
}
