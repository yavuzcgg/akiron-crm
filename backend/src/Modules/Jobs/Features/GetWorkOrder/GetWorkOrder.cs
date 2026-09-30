using Akiron.BuildingBlocks.Domain;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.GetWorkOrder;

internal sealed class GetWorkOrderHandler(JobsDbContext db, WorkOrderReader reader)
{
    public async Task<Result<WorkOrderResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(id);
        var workOrder = await db.WorkOrders.AsNoTracking().Include(candidate => candidate.Assignees)
            .FirstOrDefaultAsync(candidate => candidate.Id == workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return JobsErrors.WorkOrderNotFound;
        }

        return await reader.DetailAsync(workOrder, cancellationToken);
    }
}
