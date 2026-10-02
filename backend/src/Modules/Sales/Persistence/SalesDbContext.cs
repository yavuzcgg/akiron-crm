using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Sales.Persistence;

internal sealed class SalesDbContext(DbContextOptions<SalesDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "sales";

    public override string Schema => SchemaName;

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();

    public DbSet<QuoteSnapshot> QuoteSnapshots => Set<QuoteSnapshot>();
}

/// <summary>What a revision looked like when it was sent: the record behind "what did we offer them?".</summary>
internal sealed class QuoteSnapshot : ITenantScoped
{
    private QuoteSnapshot()
    {
        Content = string.Empty;
    }

    public long Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public QuoteId QuoteId { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    /// <summary>The quote as JSON (<c>jsonb</c>), exactly as the client received it.</summary>
    public string Content { get; private set; }

    public static QuoteSnapshot Of(Quote quote, string content) =>
        new() { QuoteId = quote.Id, Revision = quote.Revision, SentAt = quote.SentAt!.Value, Content = content };
}

internal static class SalesConstraints
{
    public const string QuoteNumberRevisionUnique = "ix_quotes_tenant_id_number";
}

internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("catalog_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(CatalogItem.NameMaxLength).UseCollation("tr-x-icu");
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.Unit).HasMaxLength(20);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 4);
        builder.Property(item => item.Currency).HasConversion(currency => currency.Code, code => Currency.From(code)).HasMaxLength(3);
        builder.Property(item => item.WithholdingCode).HasMaxLength(10);
        builder.Ignore(item => item.NetUnitPrice);
        builder.HasIndex(item => new { item.TenantId, item.Name });
    }
}

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes");
        builder.HasKey(quote => quote.Id);
        builder.Property(quote => quote.Id).ValueGeneratedNever();
        builder.Property(quote => quote.Number).HasMaxLength(30);
        builder.Property(quote => quote.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(quote => quote.PartyName).HasMaxLength(250);
        builder.Property(quote => quote.Title).HasMaxLength(Quote.TitleMaxLength);
        builder.Property(quote => quote.RecipientName).HasMaxLength(200);
        builder.Property(quote => quote.RecipientEmail).HasMaxLength(254);
        builder.Property(quote => quote.Currency).HasConversion(currency => currency.Code, code => Currency.From(code)).HasMaxLength(3);
        builder.Property(quote => quote.ExchangeRate).HasPrecision(18, 6);
        builder.Property(quote => quote.Notes).HasMaxLength(Quote.NotesMaxLength);
        foreach (var total in new[] { nameof(Quote.GrossTotal), nameof(Quote.DiscountTotal), nameof(Quote.NetTotal), nameof(Quote.VatTotal), nameof(Quote.WithholdingTotal), nameof(Quote.GrandTotal) })
        {
            builder.Property<decimal>(total).HasPrecision(18, 2);
        }

        builder.Property(quote => quote.LinkTokenHash).HasMaxLength(64);
        builder.Property(quote => quote.DecidedByName).HasMaxLength(200);
        builder.Property(quote => quote.DecisionNote).HasMaxLength(2000);
        builder.Ignore(quote => quote.NetTotalTry);

        builder.HasMany(quote => quote.Lines).WithOne().HasForeignKey(line => line.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(quote => quote.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(quote => new { quote.TenantId, quote.Number }).IsUnique().HasDatabaseName(SalesConstraints.QuoteNumberRevisionUnique);
        builder.HasIndex(quote => quote.LinkTokenHash).IsUnique().HasFilter("link_token_hash IS NOT NULL");
        builder.HasIndex(quote => new { quote.TenantId, quote.PartyId });
        builder.HasIndex(quote => new { quote.TenantId, quote.Status, quote.IssueDate });
    }
}

internal sealed class QuoteLineConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> builder)
    {
        builder.ToTable("quote_lines");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.Name).HasMaxLength(QuoteLine.NameMaxLength);
        builder.Property(line => line.Description).HasMaxLength(2000);
        builder.Property(line => line.Unit).HasMaxLength(20);
        builder.Property(line => line.Quantity).HasPrecision(18, 4);
        builder.Property(line => line.UnitPrice).HasPrecision(18, 4);
        builder.Property(line => line.DiscountPercent).HasPrecision(5, 2);
        foreach (var amount in new[] { nameof(QuoteLine.Gross), nameof(QuoteLine.Discount), nameof(QuoteLine.Net), nameof(QuoteLine.Vat), nameof(QuoteLine.Withholding), nameof(QuoteLine.Total) })
        {
            builder.Property<decimal>(amount).HasPrecision(18, 2);
        }

        builder.HasIndex(line => new { line.TenantId, line.QuoteId, line.Position });
    }
}

internal sealed class QuoteSnapshotConfiguration : IEntityTypeConfiguration<QuoteSnapshot>
{
    public void Configure(EntityTypeBuilder<QuoteSnapshot> builder)
    {
        builder.ToTable("quote_snapshots");
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Content).HasColumnType("jsonb");
        builder.HasIndex(snapshot => new { snapshot.TenantId, snapshot.QuoteId, snapshot.Revision }).IsUnique();
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class SalesDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    public SalesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, SalesDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SalesDbContext(options, new TenantContext());
    }
}
