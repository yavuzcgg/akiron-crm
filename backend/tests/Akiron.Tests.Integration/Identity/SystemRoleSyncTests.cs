using System.Net;
using System.Net.Http.Json;
using Akiron.Modules.Identity.Features.Sessions;

namespace Akiron.Tests.Integration.Identity;

public sealed class SystemRoleSyncTests(ApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sync_GivesAnOldAdminRoleTheModulesAddedSince()
    {
        using var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync();
        var tenantId = (await registered.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!.TenantId;
        var adminEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(adminEmail, role: "admin");
        using var __ = await api.CreateClient().AcceptInvitationAsync(api.InvitationTokenFor(adminEmail));

        // As if the tenant registered before the CRM module shipped.
        await api.SetRolePermissionsAsync(tenantId, "admin", "identity.members.read", "identity.members.manage");
        using var before = api.CreateClient();
        using var ___ = await before.LoginAsync(adminEmail);
        using var refused = await before.GetAsync("/api/v1/crm/parties", Cancel);

        var updated = await api.SyncSystemRolesAsync(tenantId);
        using var after = api.CreateClient();
        using var login = await after.LoginAsync(adminEmail);
        var session = await login.Content.ReadFromJsonAsync<SessionResponse>(Cancel);
        using var allowed = await after.GetAsync("/api/v1/crm/parties", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(1, updated);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains("crm.parties.write", session!.Permissions);
        Assert.DoesNotContain("identity.tenant.manage", session.Permissions);
    }

    [Fact]
    public async Task Sync_OnRolesAlreadyCurrent_ChangesNothing()
    {
        using var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync();
        var tenantId = (await registered.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!.TenantId;

        Assert.Equal(0, await api.SyncSystemRolesAsync(tenantId));
    }

    [Fact]
    public async Task Member_ByDefault_WorksOnTheTimelineButCannotSeeClients()
    {
        using var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync();
        var tenantId = (await registered.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!.TenantId;
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(memberEmail);
        using var member = api.CreateClient();
        using var __ = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));

        using var timeline = await member.GetAsync($"/api/v1/timeline/workspace/{tenantId}", Cancel);
        using var parties = await member.GetAsync("/api/v1/crm/parties", Cancel);

        Assert.Equal(HttpStatusCode.OK, timeline.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, parties.StatusCode);
    }
}
