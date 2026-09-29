using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Notifications.Persistence;

internal sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "notifications";

    public override string Schema => SchemaName;

    public DbSet<Notification> Notifications => Set<Notification>();
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public const string IdempotencyUnique = "ix_notifications_tenant_id_idempotency_key";

    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();
        builder.Property(notification => notification.Type).HasMaxLength(100);
        builder.Property(notification => notification.Payload).HasColumnType("jsonb");
        builder.Property(notification => notification.IdempotencyKey).HasMaxLength(100);
        builder.HasIndex(notification => new { notification.TenantId, notification.IdempotencyKey }).IsUnique().HasDatabaseName(IdempotencyUnique);

        // The bell: one person's newest notifications, and the unread count.
        builder.HasIndex(notification => new { notification.TenantId, notification.RecipientUserId, notification.CreatedAt });
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class NotificationsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, NotificationsDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new NotificationsDbContext(options, new TenantContext());
    }
}
