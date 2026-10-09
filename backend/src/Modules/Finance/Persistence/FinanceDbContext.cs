using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Finance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Finance.Persistence;

internal sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "finance";

    public override string Schema => SchemaName;

    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();

    public DbSet<Charge> Charges => Set<Charge>();

    public DbSet<OpenItem> OpenItems => Set<OpenItem>();

    public DbSet<Receipt> Receipts => Set<Receipt>();

    public DbSet<Allocation> Allocations => Set<Allocation>();
}

internal static class FinanceConstraints
{
    public const string ContractPeriodUnique = "ix_charges_tenant_id_contract_id_period";
}

internal static class FinanceMapping
{
    public static PropertyBuilder<Currency> AsCurrency(this PropertyBuilder<Currency> property) =>
        property.HasConversion(currency => currency.Code, code => Currency.From(code)).HasMaxLength(3);
}

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Sequence).UseIdentityAlwaysColumn();
        builder.Property(entry => entry.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(entry => entry.DocumentNumber).HasMaxLength(30);
        builder.Property(entry => entry.Description).HasMaxLength(LedgerEntry.DescriptionMaxLength);
        builder.Property(entry => entry.Debit).HasPrecision(18, 2);
        builder.Property(entry => entry.Credit).HasPrecision(18, 2);
        builder.Property(entry => entry.Currency).AsCurrency();
        builder.Property(entry => entry.ExchangeRate).HasPrecision(18, 6);
        builder.HasIndex(entry => new { entry.TenantId, entry.PartyId, entry.Currency, entry.Date, entry.Sequence });
        builder.HasIndex(entry => new { entry.TenantId, entry.Source, entry.SourceId });
    }
}

internal sealed class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
    public void Configure(EntityTypeBuilder<Charge> builder)
    {
        builder.ToTable("charges");
        builder.HasKey(charge => charge.Id);
        builder.Property(charge => charge.Id).ValueGeneratedNever();
        builder.Property(charge => charge.Number).HasMaxLength(30);
        builder.Property(charge => charge.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(charge => charge.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(charge => charge.PartyName).HasMaxLength(250);
        builder.Property(charge => charge.Description).HasMaxLength(Charge.DescriptionMaxLength);
        builder.Property(charge => charge.Currency).AsCurrency();
        builder.Property(charge => charge.ExchangeRate).HasPrecision(18, 6);
        builder.Property(charge => charge.Amount).HasPrecision(18, 2);
        builder.Property(charge => charge.InvoiceNumber).HasMaxLength(50);
        builder.Property(charge => charge.CancellationReason).HasMaxLength(500);
        builder.HasMany(charge => charge.Items).WithOne().HasForeignKey(item => item.ChargeId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(charge => charge.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(charge => new { charge.TenantId, charge.Number }).IsUnique();
        builder.HasIndex(charge => new { charge.TenantId, charge.PartyId, charge.Date });
        builder.HasIndex(charge => new { charge.TenantId, charge.QuoteId });

        // A contract's month is charged once, however often its event is delivered.
        builder.HasIndex(charge => new { charge.TenantId, charge.ContractId, charge.Period })
            .IsUnique()
            .HasFilter("contract_id IS NOT NULL")
            .HasDatabaseName(FinanceConstraints.ContractPeriodUnique);
    }
}

internal sealed class OpenItemConfiguration : IEntityTypeConfiguration<OpenItem>
{
    public void Configure(EntityTypeBuilder<OpenItem> builder)
    {
        builder.ToTable("open_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.Allocated).HasPrecision(18, 2);
        builder.Property(item => item.Currency).AsCurrency();
        builder.Ignore(item => item.Remaining);

        // Two collections allocating to the same instalment at once: the second one fails and retries.
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.PartyId, item.IsOpen, item.DueDate });
    }
}

internal sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("receipts");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.Property(receipt => receipt.Number).HasMaxLength(30);
        builder.Property(receipt => receipt.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(receipt => receipt.PartyName).HasMaxLength(250);
        builder.Property(receipt => receipt.Amount).HasPrecision(18, 2);
        builder.Property(receipt => receipt.Currency).AsCurrency();
        builder.Property(receipt => receipt.ExchangeRate).HasPrecision(18, 6);
        builder.Property(receipt => receipt.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(receipt => receipt.Account).HasMaxLength(100);
        builder.Property(receipt => receipt.Reference).HasMaxLength(100);
        builder.Property(receipt => receipt.Note).HasMaxLength(Receipt.NoteMaxLength);
        builder.Property(receipt => receipt.ReversalReason).HasMaxLength(500);
        builder.Ignore(receipt => receipt.Allocated);
        builder.Ignore(receipt => receipt.Unallocated);
        builder.HasMany(receipt => receipt.Allocations).WithOne().HasForeignKey(allocation => allocation.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(receipt => receipt.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(receipt => new { receipt.TenantId, receipt.Number }).IsUnique();
        builder.HasIndex(receipt => new { receipt.TenantId, receipt.PartyId, receipt.Date });
    }
}

internal sealed class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    public void Configure(EntityTypeBuilder<Allocation> builder)
    {
        builder.ToTable("allocations");
        builder.HasKey(allocation => allocation.Id);
        builder.Property(allocation => allocation.Id).ValueGeneratedNever();
        builder.Property(allocation => allocation.Amount).HasPrecision(18, 2);
        builder.HasOne<OpenItem>().WithMany().HasForeignKey(allocation => allocation.OpenItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(allocation => new { allocation.TenantId, allocation.OpenItemId });
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class FinanceDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, FinanceDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new FinanceDbContext(options, new TenantContext());
    }
}
