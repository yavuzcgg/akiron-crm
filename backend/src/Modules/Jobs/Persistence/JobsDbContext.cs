using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Jobs.Persistence;

internal sealed class JobsDbContext(DbContextOptions<JobsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "jobs";

    public override string Schema => SchemaName;

    public DbSet<Stage> Stages => Set<Stage>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<WorkOrderAssignee> Assignees => Set<WorkOrderAssignee>();

    public DbSet<WorkOrderTask> Tasks => Set<WorkOrderTask>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
}

internal static class JobsConstraints
{
    public const string OneRunningTimer = "ix_time_entries_one_running_per_user";
    public const string WorkOrderStage = "fk_work_orders_stages_stage_id";
}

internal sealed class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("stages");
        builder.HasKey(stage => stage.Id);
        builder.Property(stage => stage.Id).ValueGeneratedNever();
        builder.Property(stage => stage.Key).HasMaxLength(30);
        builder.Property(stage => stage.Name).HasMaxLength(Stage.NameMaxLength);
        builder.Property(stage => stage.Category).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(stage => new { stage.TenantId, stage.Position });
    }
}

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("work_orders");
        builder.HasKey(workOrder => workOrder.Id);
        builder.Property(workOrder => workOrder.Id).ValueGeneratedNever();
        builder.Property(workOrder => workOrder.Number).HasMaxLength(30);
        builder.Property(workOrder => workOrder.Title).HasMaxLength(WorkOrder.TitleMaxLength);
        builder.Property(workOrder => workOrder.Description).HasMaxLength(WorkOrder.DescriptionMaxLength);
        builder.Property(workOrder => workOrder.PartyName).HasMaxLength(250);
        builder.Property(workOrder => workOrder.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(workOrder => workOrder.Budget).HasPrecision(18, 2);
        builder.HasOne<Stage>().WithMany().HasForeignKey(workOrder => workOrder.StageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(workOrder => workOrder.Assignees).WithOne().HasForeignKey(assignee => assignee.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(workOrder => workOrder.Assignees).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(workOrder => new { workOrder.TenantId, workOrder.Number }).IsUnique();
        builder.HasIndex(workOrder => new { workOrder.TenantId, workOrder.StageId, workOrder.Rank });
        builder.HasIndex(workOrder => new { workOrder.TenantId, workOrder.PartyId });
    }
}

internal sealed class WorkOrderAssigneeConfiguration : IEntityTypeConfiguration<WorkOrderAssignee>
{
    public void Configure(EntityTypeBuilder<WorkOrderAssignee> builder)
    {
        builder.ToTable("work_order_assignees");
        builder.HasKey(assignee => new { assignee.WorkOrderId, assignee.UserId });
        builder.HasIndex(assignee => new { assignee.TenantId, assignee.UserId });
    }
}

internal sealed class WorkOrderTaskConfiguration : IEntityTypeConfiguration<WorkOrderTask>
{
    public void Configure(EntityTypeBuilder<WorkOrderTask> builder)
    {
        builder.ToTable("work_order_tasks");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id).ValueGeneratedNever();
        builder.Property(task => task.Title).HasMaxLength(WorkOrderTask.TitleMaxLength);
        builder.Ignore(task => task.IsDone);
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(task => task.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(task => new { task.TenantId, task.WorkOrderId, task.Position });
    }
}

internal sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("time_entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Note).HasMaxLength(TimeEntry.NoteMaxLength);
        builder.Property(entry => entry.CostPerHour).HasPrecision(18, 2);
        builder.Ignore(entry => entry.IsRunning);
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(entry => entry.WorkOrderId).OnDelete(DeleteBehavior.Restrict);

        // The database, not only the handler, keeps one timer per person: two tabs pressing
        // "start" at once cannot both win.
        builder.HasIndex(entry => new { entry.TenantId, entry.UserId })
            .IsUnique()
            .HasFilter("ended_at IS NULL AND is_deleted = false")
            .HasDatabaseName(JobsConstraints.OneRunningTimer);
        builder.HasIndex(entry => new { entry.TenantId, entry.UserId, entry.StartedAt });
        builder.HasIndex(entry => new { entry.TenantId, entry.WorkOrderId });
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class JobsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<JobsDbContext>
{
    public JobsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<JobsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, JobsDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new JobsDbContext(options, new TenantContext());
    }
}
