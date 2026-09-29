using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Reference.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Reference.Persistence;

/// <summary>Global reference data (exchange rates now; tax codes and provinces later). Nothing here is tenant-scoped.</summary>
internal sealed class ReferenceDbContext(DbContextOptions<ReferenceDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "reference";

    public override string Schema => SchemaName;

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
}

internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("exchange_rates");
        builder.HasKey(rate => new { rate.BulletinDate, rate.CurrencyCode });
        builder.Property(rate => rate.CurrencyCode).HasMaxLength(3);

        // TCMB quotes four decimals; after dividing by the unit (JPY per 100) six keep full precision.
        foreach (var property in new[] { nameof(ExchangeRate.ForexBuying), nameof(ExchangeRate.ForexSelling), nameof(ExchangeRate.BanknoteBuying), nameof(ExchangeRate.BanknoteSelling) })
        {
            builder.Property(property).HasPrecision(18, 6);
        }

        // "Latest bulletin on or before a date" for one currency.
        builder.HasIndex(rate => new { rate.CurrencyCode, rate.BulletinDate }).IsDescending(false, true);
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class ReferenceDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReferenceDbContext>
{
    public ReferenceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReferenceDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, ReferenceDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ReferenceDbContext(options, new TenantContext());
    }
}
