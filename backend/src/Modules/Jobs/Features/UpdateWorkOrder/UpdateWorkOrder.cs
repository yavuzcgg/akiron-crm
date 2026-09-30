using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.Contracts.Jobs;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Features.CreateWorkOrder;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.UpdateWorkOrder;

internal sealed record UpdateWorkOrderCommand(
    string Title,
    string? Description = null,
    Guid? PartyId = null,
    string? Priority = null,
    DateOnly? DueDate = null,
    IReadOnlyList<Guid>? AssigneeIds = null) : IWorkOrderInput;

internal sealed class UpdateWorkOrderValidator : AbstractValidator<UpdateWorkOrderCommand>
{
    public UpdateWorkOrderValidator() => WorkOrderInputRules.Apply(this);
}

/// <summary>Replaces the details and the people; only people newly added are notified.</summary>
internal sealed class UpdateWorkOrderHandler(
    JobsDbContext db,
    WorkOrderReferences references,
    WorkOrderReader reader,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<WorkOrderResponse>> HandleAsync(Guid id, UpdateWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(id);
        var workOrder = await db.WorkOrders.Include(candidate => candidate.Assignees)
            .FirstOrDefaultAsync(candidate => candidate.Id == workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return JobsErrors.WorkOrderNotFound;
        }

        // A party archived since keeps its name on old work orders; only a new link is checked.
        var partyUnchanged = command.PartyId is not null && command.PartyId == workOrder.PartyId;
        var resolved = await references.ResolveAsync(partyUnchanged ? command with { PartyId = null } : command, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return resolved.Error;
        }

        var (party, assignees) = resolved.Value;
        var details = partyUnchanged
            ? WorkOrderReferences.Details(command, null) with { PartyId = workOrder.PartyId, PartyName = workOrder.PartyName }
            : WorkOrderReferences.Details(command, party);

        workOrder.Update(details);
        var added = workOrder.AssignExactly(assignees);
        if (added.Count > 0)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
            db.Publish(new WorkOrderAssigned(workOrder.TenantId, timeProvider.GetUtcNow(), workOrder.Id.Value, workOrder.Number, workOrder.Title, added, userId.Value, currentUser.DisplayName));
        }

        await db.SaveChangesAsync(cancellationToken);
        return await reader.DetailAsync(workOrder, cancellationToken);
    }
}
