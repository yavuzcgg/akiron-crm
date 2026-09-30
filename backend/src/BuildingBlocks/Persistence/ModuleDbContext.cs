using System.Reflection;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Akiron.BuildingBlocks.Persistence;

/// <summary>
/// Base for each module's DbContext (ADR-0001): one Postgres schema per module, entity
/// configurations picked up from the module assembly, typed ids stored as <c>uuid</c>, and global
/// filters for tenant scope and soft delete (ADR-0002).
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, ITenantContext tenantContext) : DbContext(options)
{
    private static readonly MethodInfo TenantFilter = GetFilterMethod(nameof(ApplyTenantFilter));
    private static readonly MethodInfo SoftDeleteFilter = GetFilterMethod(nameof(ApplySoftDeleteFilter));

    /// <summary>The module's schema; also the prefix of its routes and permission names.</summary>
    public abstract string Schema { get; }

    /// <summary>
    /// Read by the query filters. EF Core evaluates DbContext members per query, so each request
    /// sees its own tenant. With no tenant bound it is the empty id, which matches no row.
    /// </summary>
    protected TenantId CurrentTenantId => tenantContext.HasTenant ? tenantContext.TenantId : TenantId.Empty;

    /// <summary>This module's outbox (ADR-0001); read by <see cref="Events.OutboxProcessor"/>.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Queues an integration event in this module's outbox. It is written by the next
    /// <c>SaveChanges</c>, in the same transaction as the change that caused it.
    /// </summary>
    public void Publish(IIntegrationEvent integrationEvent) => OutboxMessages.Add(OutboxMessage.From(integrationEvent));

    /// <summary>
    /// The next number in a per-tenant, per-series, per-year sequence (ADR-0004): 1, 2, 3 … reset
    /// every year. The counter row stays locked until the surrounding transaction ends, so call
    /// this inside an explicit transaction together with the insert of the document: a rollback
    /// then releases the number instead of leaving a gap.
    /// </summary>
    public async Task<long> NextDocumentSequenceAsync(string series, int year, CancellationToken cancellationToken)
    {
        if (!tenantContext.HasTenant)
        {
            throw new TenantMismatchException("Document numbers are allocated per tenant; no tenant is bound.");
        }

        // Only the schema name is interpolated, and it comes from code (ModuleDbContext.Schema),
        // never from input; identifiers cannot be passed as parameters. Values are parameters.
#pragma warning disable EF1002
        var values = await Database.SqlQueryRaw<long>(
            $$"""
            INSERT INTO "{{Schema}}".document_counters (tenant_id, series, year, last_value)
            VALUES ({0}, {1}, {2}, 1)
            ON CONFLICT (tenant_id, series, year)
            DO UPDATE SET last_value = document_counters.last_value + 1
            RETURNING last_value AS "Value"
            """,
            tenantContext.TenantId.Value, series, year).ToListAsync(cancellationToken);
#pragma warning restore EF1002

        return values[0];
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        ConfigureInfrastructureTables(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        ApplyGlobalFilters(modelBuilder);
    }

    private static void ConfigureInfrastructureTables(ModelBuilder modelBuilder)
    {
        AuditChange.Configure(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("outbox_messages");
            outbox.HasKey(message => message.Id);
            outbox.Property(message => message.Id).ValueGeneratedNever();
            outbox.Property(message => message.Type).HasMaxLength(200);
            outbox.Property(message => message.Payload).HasColumnType("jsonb");

            // The dispatcher's query: pending rows in order.
            outbox.HasIndex(message => new { message.NextAttemptAt, message.OccurredAt })
                .HasFilter("processed_at IS NULL AND failed_at IS NULL")
                .HasDatabaseName("ix_outbox_messages_pending");
        });

        modelBuilder.Entity<DocumentCounter>(counter =>
        {
            counter.ToTable("document_counters");
            counter.HasKey(row => new { row.TenantId, row.Series, row.Year });
            counter.Property(row => row.Series).HasMaxLength(20);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        TypedIdConventions.Apply(configurationBuilder, typeof(ModuleDbContext).Assembly, GetType().Assembly);
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => type.BaseType is null && !type.IsOwned()))
        {
            var clrType = entityType.ClrType;
            if (typeof(ITenantScoped).IsAssignableFrom(clrType))
            {
                TenantFilter.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                SoftDeleteFilter.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    // Named filters (EF Core 10), so a query can lift one without the other:
    // IncludingArchived() drops SoftDelete and keeps Tenant.
    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(QueryFilters.Tenant, entity => entity.TenantId == CurrentTenantId);

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(QueryFilters.SoftDelete, entity => !entity.IsDeleted);

    private static MethodInfo GetFilterMethod(string name) =>
        typeof(ModuleDbContext).GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Filter method {name} is missing.");
}
