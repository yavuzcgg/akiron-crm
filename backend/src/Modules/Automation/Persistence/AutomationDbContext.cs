using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Automation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Automation.Persistence;

internal sealed class AutomationDbContext(DbContextOptions<AutomationDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "automation";

    public override string Schema => SchemaName;

    public DbSet<AutomationRule> Rules => Set<AutomationRule>();

    public DbSet<AutomationRun> Runs => Set<AutomationRun>();
}

internal static class AutomationConstraints
{
    public const string RunUnique = "ix_runs_rule_id_event_id";
}

internal sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("rules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.Name).HasMaxLength(AutomationRule.NameMaxLength);
        builder.Property(rule => rule.Trigger).HasMaxLength(50);
        builder.Property(rule => rule.Action).HasMaxLength(50);
        builder.Property(rule => rule.Target).HasMaxLength(AutomationRule.TargetMaxLength);
        builder.Property(rule => rule.Subject).HasMaxLength(AutomationRule.SubjectMaxLength);
        builder.Property(rule => rule.Body).HasMaxLength(AutomationRule.BodyMaxLength);
        builder.HasIndex(rule => new { rule.TenantId, rule.Trigger, rule.Enabled });
    }
}

internal sealed class AutomationRunConfiguration : IEntityTypeConfiguration<AutomationRun>
{
    public void Configure(EntityTypeBuilder<AutomationRun> builder)
    {
        builder.ToTable("runs");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Trigger).HasMaxLength(50);
        builder.Property(run => run.Outcome).HasConversion<string>().HasMaxLength(20);
        builder.Property(run => run.Detail).HasMaxLength(300);
        builder.HasIndex(run => new { run.RuleId, run.EventId }).IsUnique().HasDatabaseName(AutomationConstraints.RunUnique);
        builder.HasIndex(run => new { run.TenantId, run.OccurredAt });
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class AutomationDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AutomationDbContext>
{
    public AutomationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AutomationDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, AutomationDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AutomationDbContext(options, new TenantContext());
    }
}
