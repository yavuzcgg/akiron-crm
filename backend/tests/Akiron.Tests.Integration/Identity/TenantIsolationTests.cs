using System.Net;
using System.Net.Http.Json;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Features.ListMembers;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Tests.Integration.Identity;

/// <summary>ADR-0002 checked through the full HTTP pipeline: one tenant never sees another's rows.</summary>
public sealed class TenantIsolationTests(ApiFixture api)
{
    private static readonly Uri MembersUri = new("/api/v1/identity/members", UriKind.Relative);

    [Fact]
    public async Task Members_ListsOnlyTheCurrentTenant()
    {
        using var first = api.CreateClient();
        var firstEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await first.RegisterAsync(firstEmail, organizationName: "Birinci Ajans");

        using var second = api.CreateClient();
        var secondEmail = ApiClientExtensions.UniqueEmail();
        using var __ = await second.RegisterAsync(secondEmail, organizationName: "İkinci Ajans");

        var members = await first.GetFromJsonAsync<PagedResult<MemberResponse>>(MembersUri, TestContext.Current.CancellationToken);

        Assert.NotNull(members);
        var member = Assert.Single(members.Items);
        Assert.Equal(firstEmail, member.Email);
        Assert.Equal(1, members.TotalCount);
    }

    [Fact]
    public async Task Members_WithoutThePermission_ReturnsForbidden()
    {
        using var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync();
        var session = await registered.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(session);

        // Inviting people arrives in Sprint 2; until then the member is added straight to the database.
        var memberEmail = ApiClientExtensions.UniqueEmail();
        await AddMemberAsync(TenantId.From(session.TenantId), memberEmail);

        using var member = api.CreateClient();
        using var login = await member.LoginAsync(memberEmail);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await member.GetAsync(MembersUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("common.auth.forbidden", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Members_WithAnOversizedPage_IsRejectedNotClamped()
    {
        using var client = api.CreateClient();
        using var _ = await client.RegisterAsync();

        using var response = await client.GetAsync(new Uri("/api/v1/identity/members?pageSize=500", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("common.validation.failed", await response.ReadErrorCodeAsync());
    }

    private async Task AddMemberAsync(TenantId tenantId, string email)
    {
        await using var scope = api.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Akiron.Modules.Identity.Security.IPasswordHasher>();

        var memberRole = await db.Roles.SingleAsync(role => role.Name == SystemRoles.Member, TestContext.Current.CancellationToken);
        var user = User.Create(email, "Ayşe Yılmaz", hasher.Hash(ApiClientExtensions.Password));

        db.Users.Add(user);
        db.Memberships.Add(Membership.Create(tenantId, user.Id, memberRole.Id));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
