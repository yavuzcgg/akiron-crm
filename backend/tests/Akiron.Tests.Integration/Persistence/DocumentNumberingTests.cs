using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Tests.Integration.Persistence;

/// <summary>ADR-0004 numbering: per tenant, series and year; no gaps from rolled-back work.</summary>
public sealed class DocumentNumberingTests(ApiFixture api)
{
    [Fact]
    public async Task NextSequence_CountsPerSeriesAndYear_AndIgnoresRolledBackNumbers()
    {
        var tenant = await CreateTenantAsync();

        Assert.Equal(1, await NextAsync(tenant, "TKL", 2026, commit: true));
        Assert.Equal(2, await NextAsync(tenant, "TKL", 2026, commit: true));
        Assert.Equal(3, await NextAsync(tenant, "TKL", 2026, commit: false));
        Assert.Equal(3, await NextAsync(tenant, "TKL", 2026, commit: true));
        Assert.Equal(1, await NextAsync(tenant, "TKL", 2027, commit: true));
        Assert.Equal(1, await NextAsync(tenant, "ISE", 2026, commit: true));
        Assert.Equal(1, await NextAsync(await CreateTenantAsync(), "TKL", 2026, commit: true));
    }

    [Fact]
    public void Format_PadsToFourDigits() =>
        Assert.Equal("TKL-2026-0007", DocumentNumber.Format("TKL", 2026, 7));

    private async Task<long> NextAsync(TenantId tenant, string series, int year, bool commit)
    {
        await using var scope = api.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(tenant);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var value = await db.NextDocumentSequenceAsync(series, year, TestContext.Current.CancellationToken);

        if (commit)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        return value;
    }

    private async Task<TenantId> CreateTenantAsync()
    {
        await using var scope = api.CreateScope();
        var tenant = Tenant.Create($"Numara {Guid.NewGuid():N}");
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(tenant.Id);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return tenant.Id;
    }
}
