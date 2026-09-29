using System.Reflection;
using Akiron.BuildingBlocks.Domain;
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
    private static readonly MethodInfo CombinedFilter = GetFilterMethod(nameof(ApplyCombinedFilter));

    /// <summary>The module's schema; also the prefix of its routes and permission names.</summary>
    public abstract string Schema { get; }

    /// <summary>
    /// Read by the query filters. EF Core evaluates DbContext members per query, so each request
    /// sees its own tenant. With no tenant bound it is the empty id, which matches no row.
    /// </summary>
    protected TenantId CurrentTenantId => tenantContext.HasTenant ? tenantContext.TenantId : TenantId.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        ApplyGlobalFilters(modelBuilder);
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
            var tenantScoped = typeof(ITenantScoped).IsAssignableFrom(clrType);
            var softDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            var filter = (tenantScoped, softDeletable) switch
            {
                (true, true) => CombinedFilter,
                (true, false) => TenantFilter,
                (false, true) => SoftDeleteFilter,
                _ => null,
            };

            filter?.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId);

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => !entity.IsDeleted);

    private void ApplyCombinedFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped, ISoftDeletable =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId && !entity.IsDeleted);

    private static MethodInfo GetFilterMethod(string name) =>
        typeof(ModuleDbContext).GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Filter method {name} is missing.");
}
