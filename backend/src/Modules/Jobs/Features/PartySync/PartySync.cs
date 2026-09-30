using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Contracts.Crm;
using Akiron.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.PartySync;

/// <summary>
/// Keeps the client name shown on work orders current when the party is renamed in CRM.
/// Idempotent: setting the same name twice changes nothing.
/// </summary>
internal sealed class PartyRenamedConsumer(JobsDbContext db) : IIntegrationEventConsumer<PartyUpdated>
{
    public async Task HandleAsync(PartyUpdated integrationEvent, CancellationToken cancellationToken)
    {
        if (!integrationEvent.ChangedFields.Contains("name"))
        {
            return;
        }

        await db.WorkOrders
            .IncludingArchived()
            .Where(workOrder => workOrder.PartyId == integrationEvent.PartyId && workOrder.PartyName != integrationEvent.Name)
            .ExecuteUpdateAsync(setters => setters.SetProperty(workOrder => workOrder.PartyName, integrationEvent.Name), cancellationToken);
    }
}
