using Akiron.BuildingBlocks.Domain;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.Budget;

/// <param name="Budget">TRY excluding VAT; null clears it.</param>
internal sealed record SetBudgetCommand(decimal? Budget);

internal sealed class SetBudgetValidator : AbstractValidator<SetBudgetCommand>
{
    public SetBudgetValidator() => RuleFor(command => command.Budget).InclusiveBetween(0m, 100_000_000m);
}

/// <summary>Separate from the other details: who may see money is not everyone who runs the job.</summary>
internal sealed class SetBudgetHandler(JobsDbContext db, WorkOrderReader reader)
{
    public async Task<Result<WorkOrderResponse>> HandleAsync(Guid id, SetBudgetCommand command, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(id);
        var workOrder = await db.WorkOrders.Include(candidate => candidate.Assignees)
            .FirstOrDefaultAsync(candidate => candidate.Id == workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return JobsErrors.WorkOrderNotFound;
        }

        workOrder.SetBudget(command.Budget);
        await db.SaveChangesAsync(cancellationToken);
        return await reader.DetailAsync(workOrder, cancellationToken);
    }
}
