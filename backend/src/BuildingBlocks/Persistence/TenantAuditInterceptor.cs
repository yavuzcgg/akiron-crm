using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Akiron.BuildingBlocks.Persistence;

/// <summary>
/// Enforces ADR-0002 at the last possible moment, so no handler has to remember it:
/// <list type="bullet">
/// <item>new tenant-scoped rows get the current tenant; a row carrying another tenant's id is refused;</item>
/// <item>changing or deleting a row that belongs to another tenant is refused;</item>
/// <item>deleting a soft-deletable row marks it deleted instead;</item>
/// <item>creation and change stamps are written.</item>
/// </list>
/// Bulk <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> bypass interceptors; they are only used on
/// global tables (e.g. refresh tokens).
/// </summary>
public sealed class TenantAuditInterceptor(
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;
        var audit = new List<AuditChange>();

        // Interceptors run before EF's own change detection; property IsModified must be current here.
        context.ChangeTracker.DetectChanges();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            if (entry.Entity is ITenantScoped)
            {
                GuardTenant(entry);
            }

            // Decided before soft delete turns a Deleted entry into a Modified one.
            var action = entry.State switch
            {
                EntityState.Added => "created",
                EntityState.Deleted => "deleted",
                _ => "updated",
            };

            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable })
            {
                entry.State = EntityState.Modified;
                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = now;
            }

            if (entry.Entity is IAuditable)
            {
                Stamp(entry, now, userId);

                var tenant = entry.Entity is ITenantScoped scoped ? scoped.TenantId : TenantId.Empty;
                if (AuditChange.From(entry, action, tenant, userId, now) is { } change)
                {
                    audit.Add(change);
                }
            }
        }

        context.Set<AuditChange>().AddRange(audit);
    }

    private void GuardTenant(EntityEntry entry)
    {
        if (!tenantContext.HasTenant)
        {
            throw new TenantMismatchException(
                $"A {entry.Metadata.ClrType.Name} was saved with no tenant bound to the scope.");
        }

        var current = tenantContext.TenantId;
        var property = entry.Property(nameof(ITenantScoped.TenantId));

        if (entry.State == EntityState.Added)
        {
            var assigned = (TenantId)property.CurrentValue!;

            if (assigned == TenantId.Empty)
            {
                property.CurrentValue = current;
            }
            else if (assigned != current)
            {
                throw new TenantMismatchException(
                    $"A {entry.Metadata.ClrType.Name} for another tenant was added to this tenant's scope.");
            }

            return;
        }

        if ((TenantId)property.OriginalValue! != current || (TenantId)property.CurrentValue! != current)
        {
            throw new TenantMismatchException(
                $"A {entry.Metadata.ClrType.Name} belonging to another tenant was modified or deleted.");
        }
    }

    private static void Stamp(EntityEntry entry, DateTimeOffset now, UserId? userId)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
            entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = userId;
            return;
        }

        entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
        entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = userId;
    }
}
