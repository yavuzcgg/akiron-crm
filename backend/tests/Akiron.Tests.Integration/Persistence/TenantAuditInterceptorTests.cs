using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Tests.Integration.Persistence;

/// <summary>
/// The guard that replaces "remember to set TenantId in every handler" (ADR-0002). The akiron-seo
/// incident this prevents: a dropped middleware made writes land under the empty tenant id.
/// </summary>
public sealed class TenantAuditInterceptorTests(ApiFixture api)
{
    [Fact]
    public async Task SaveChanges_WithNoTenantBound_RefusesTenantScopedRows()
    {
        var tenant = await CreateTenantAsync();
        await using var scope = api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        db.Roles.Add(Role.CreateSystem(tenant, "editor", []));

        await Assert.ThrowsAsync<TenantMismatchException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChanges_WithAnotherTenantsRow_Refuses()
    {
        var mine = await CreateTenantAsync();
        var theirs = await CreateTenantAsync();
        await using var scope = api.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(mine);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        db.Roles.Add(Role.CreateSystem(theirs, "editor", []));

        await Assert.ThrowsAsync<TenantMismatchException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChanges_WithoutATenantOnTheRow_StampsTheCurrentTenantAndCreationTime()
    {
        var tenant = await CreateTenantAsync();
        await using var scope = api.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(tenant);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var role = Role.CreateSystem(TenantId.Empty, "editor", ["identity.members.read"]);
        db.Roles.Add(role);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(tenant, role.TenantId);
        Assert.True(role.CreatedAt >= before);
    }

    [Fact]
    public async Task Query_ForAnotherTenant_SeesNothing()
    {
        var mine = await CreateTenantAsync();
        var theirs = await CreateTenantAsync();

        await using var scope = api.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(mine);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var visibleTenants = await db.Roles.Select(role => role.TenantId).Distinct().ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([mine], visibleTenants);
        Assert.DoesNotContain(theirs, visibleTenants);
    }

    /// <summary>A tenant with its three system roles, created the way registration does it.</summary>
    private async Task<TenantId> CreateTenantAsync()
    {
        await using var scope = api.CreateScope();
        var tenant = Tenant.Create($"Test {Guid.NewGuid():N}");
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(tenant.Id);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        db.Tenants.Add(tenant);
        db.Roles.AddRange(SystemRoles.CreateFor(tenant.Id, new PermissionCatalog([])));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return tenant.Id;
    }
}
