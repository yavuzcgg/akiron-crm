using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Scheduling;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Sales;
using Akiron.Modules.Sales.Domain;
using Akiron.Modules.Sales.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Sales.Features.Contracts;

/// <summary>
/// Once the day comes: renews auto-renewing contracts, bills the months that started, ends contracts
/// that reached their end date and reminds owners whose contract nears its end. Each contract's
/// changes and events are saved together; a concurrent run on another instance loses on the row
/// version and leaves the work to the winner.
/// </summary>
internal sealed class ContractScheduler(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IScheduledJob
{
    public async Task RunAsync(DateOnly today, CancellationToken cancellationToken)
    {
        List<Guid> tenants;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
            tenants = await db.Contracts.IgnoreQueryFilters([QueryFilters.Tenant])
                .Where(contract => contract.Status != ContractStatus.Ended)
                .Select(contract => contract.TenantId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        foreach (var tenantId in tenants)
        {
            await RunTenantAsync(Akiron.BuildingBlocks.Domain.TenantId.From(tenantId), today, cancellationToken);
        }
    }

    private async Task RunTenantAsync(Akiron.BuildingBlocks.Domain.TenantId tenantId, DateOnly today, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

        var ids = await db.Contracts.Where(contract => contract.Status != ContractStatus.Ended).Select(contract => contract.Id).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            var contract = await db.Contracts.FirstAsync(candidate => candidate.Id == id, cancellationToken);
            if (!Advance(db, contract, today))
            {
                continue;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another instance (or a person) changed it a moment ago; the next run looks again.
            }
        }
    }

    /// <summary>Applies today's moves to one contract and queues their events; returns whether anything changed.</summary>
    private bool Advance(SalesDbContext db, Contract contract, DateOnly today)
    {
        var now = timeProvider.GetUtcNow();
        var changed = false;

        if (contract.RenewIfDue(today))
        {
            db.Publish(new ContractRenewed(contract.TenantId, now, contract.Id.Value, contract.Number, contract.Title, contract.PartyId, contract.EndDate!.Value));
            changed = true;
        }

        var periods = contract.BillDue(today);
        if (periods.Count > 0)
        {
            ContractViews.PublishPeriods(db, contract.TenantId, contract, periods, now);
            changed = true;
        }

        if (contract.ExpireIfDue(today))
        {
            db.Publish(new ContractEnded(contract.TenantId, now, contract.Id.Value, contract.Number, contract.Title, contract.PartyId, contract.EndedOn!.Value, null, null, null));
            changed = true;
        }

        if (contract.TakeNoticeIfDue(today))
        {
            db.Publish(new ContractEnding(
                contract.TenantId, now, contract.Id.Value, contract.Number, contract.Title, contract.PartyName, contract.EndDate!.Value, contract.AutoRenew, contract.CreatedBy?.Value));
            changed = true;
        }

        return changed;
    }
}
