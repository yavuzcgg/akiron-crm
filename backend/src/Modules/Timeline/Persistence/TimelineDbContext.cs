using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Timeline.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Timeline.Persistence;

internal sealed class TimelineDbContext(DbContextOptions<TimelineDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "timeline";

    public override string Schema => SchemaName;

    public DbSet<TimelineEntry> Entries => Set<TimelineEntry>();

    public DbSet<TimelineLink> Links => Set<TimelineLink>();
}

internal sealed class TimelineEntryConfiguration : IEntityTypeConfiguration<TimelineEntry>
{
    public const string IdempotencyUnique = "ix_timeline_entries_tenant_id_idempotency_key";

    public void Configure(EntityTypeBuilder<TimelineEntry> builder)
    {
        builder.ToTable("entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Type).HasMaxLength(100);
        builder.Property(entry => entry.ActorKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(entry => entry.ActorName).HasMaxLength(200);
        builder.Property(entry => entry.Payload).HasColumnType("jsonb");
        builder.Property(entry => entry.IdempotencyKey).HasMaxLength(100);
        builder.Property(entry => entry.Visibility).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(entry => new { entry.TenantId, entry.IdempotencyKey }).IsUnique().HasDatabaseName(IdempotencyUnique);

        builder.HasMany(entry => entry.Links).WithOne().HasForeignKey(link => link.EntryId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entry => entry.Links).HasField("_links");
    }
}

internal sealed class TimelineLinkConfiguration : IEntityTypeConfiguration<TimelineLink>
{
    public void Configure(EntityTypeBuilder<TimelineLink> builder)
    {
        builder.ToTable("links");
        builder.HasKey(link => link.Sequence);
        builder.Property(link => link.Sequence).UseIdentityAlwaysColumn();
        builder.Property(link => link.SubjectType).HasMaxLength(50);
        builder.HasIndex(link => new { link.EntryId, link.SubjectType, link.SubjectId }).IsUnique();

        // The stream query: one subject, newest first, keyset on (occurred_at, sequence).
        builder.HasIndex(link => new { link.TenantId, link.SubjectType, link.SubjectId, link.OccurredAt, link.Sequence })
            .IsDescending(false, false, false, true, true)
            .HasDatabaseName("ix_timeline_links_stream");
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class TimelineDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TimelineDbContext>
{
    public TimelineDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TimelineDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, TimelineDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TimelineDbContext(options, new TenantContext());
    }
}
