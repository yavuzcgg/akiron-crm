using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.Contracts.Jobs;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.MoveWorkOrder;

/// <param name="Index">Position among the stage's other cards (0 = top); null puts it at the bottom.</param>
internal sealed record MoveWorkOrderCommand(Guid StageId, int? Index = null);

internal sealed class MoveWorkOrderValidator : AbstractValidator<MoveWorkOrderCommand>
{
    public MoveWorkOrderValidator() => RuleFor(command => command.Index).GreaterThanOrEqualTo(0);
}

/// <summary>A drag on the board: to another stage, or to another place in the same one.</summary>
internal sealed class MoveWorkOrderHandler(
    JobsDbContext db,
    StageBoard board,
    WorkOrderReader reader,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<WorkOrderCard>> HandleAsync(Guid id, MoveWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(id);
        var workOrder = await db.WorkOrders.Include(candidate => candidate.Assignees)
            .FirstOrDefaultAsync(candidate => candidate.Id == workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return JobsErrors.WorkOrderNotFound;
        }

        var stages = await board.StagesAsync(cancellationToken);
        var target = stages.FirstOrDefault(stage => stage.Id == StageId.From(command.StageId));
        if (target is null)
        {
            return JobsErrors.StageNotFound;
        }

        var from = stages.First(stage => stage.Id == workOrder.StageId);
        var now = timeProvider.GetUtcNow();
        var rank = await board.RankAtAsync(target.Id, workOrder.Id, command.Index, cancellationToken);
        if (workOrder.MoveTo(target, rank, now))
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
            db.Publish(new WorkOrderMoved(
                workOrder.TenantId,
                now,
                workOrder.Id.Value,
                workOrder.Number,
                workOrder.Title,
                workOrder.PartyId,
                JobsApi.ToRef(from),
                JobsApi.ToRef(target),
                target.Category == StageCategory.Done,
                userId.Value,
                currentUser.DisplayName));
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await reader.CardsAsync([workOrder], cancellationToken))[0];
    }
}
