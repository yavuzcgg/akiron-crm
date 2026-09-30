using Akiron.BuildingBlocks.Domain;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.ArchiveWorkOrder;

/// <summary>Takes a work order off the board (soft delete); its time, files and history stay.</summary>
internal sealed class ArchiveWorkOrderHandler(JobsDbContext db)
{
    public async Task<Result<bool>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(id);
        var workOrder = await db.WorkOrders.FirstOrDefaultAsync(candidate => candidate.Id == workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return JobsErrors.WorkOrderNotFound;
        }

        db.WorkOrders.Remove(workOrder);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
