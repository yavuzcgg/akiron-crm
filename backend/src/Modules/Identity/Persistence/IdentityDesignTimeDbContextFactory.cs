using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Akiron.Modules.Identity.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> only, so migrations can be added without booting the host (which
/// validates secrets and needs a database). The connection string is never opened for
/// <c>migrations add</c>; for <c>database update</c> pass <c>--connection</c>.
/// </summary>
internal sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, IdentityDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new IdentityDbContext(options, new TenantContext());
    }
}
