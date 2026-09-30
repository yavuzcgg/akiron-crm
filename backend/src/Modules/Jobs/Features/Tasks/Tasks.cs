using Akiron.BuildingBlocks.Domain;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.Tasks;

internal sealed record AddTaskCommand(string Title);

internal sealed class AddTaskValidator : AbstractValidator<AddTaskCommand>
{
    public AddTaskValidator() => RuleFor(command => command.Title).NotEmpty().MaximumLength(WorkOrderTask.TitleMaxLength);
}

internal sealed record UpdateTaskCommand(string Title, bool IsDone);

internal sealed class UpdateTaskValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskValidator() => RuleFor(command => command.Title).NotEmpty().MaximumLength(WorkOrderTask.TitleMaxLength);
}

/// <summary>A work order's checklist: add, tick, rename, remove.</summary>
internal sealed class TasksHandler(JobsDbContext db, TimeProvider timeProvider)
{
    public const int MaxTasks = 100;

    public async Task<Result<WorkOrderTaskResponse>> AddAsync(Guid workOrderId, AddTaskCommand command, CancellationToken cancellationToken)
    {
        var id = WorkOrderId.From(workOrderId);
        if (!await db.WorkOrders.AnyAsync(workOrder => workOrder.Id == id, cancellationToken))
        {
            return JobsErrors.WorkOrderNotFound;
        }

        var positions = await db.Tasks.Where(task => task.WorkOrderId == id).Select(task => task.Position).ToListAsync(cancellationToken);
        if (positions.Count >= MaxTasks)
        {
            return Error.Rule("jobs.task.too_many", "A work order holds at most 100 tasks.");
        }

        var task = WorkOrderTask.Create(id, command.Title, positions.Count == 0 ? 0 : positions.Max() + 1);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(task);
    }

    public async Task<Result<WorkOrderTaskResponse>> UpdateAsync(Guid workOrderId, Guid taskId, UpdateTaskCommand command, CancellationToken cancellationToken)
    {
        var task = await FindAsync(workOrderId, taskId, cancellationToken);
        if (task is null)
        {
            return JobsErrors.TaskNotFound;
        }

        task.Update(command.Title, command.IsDone, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(task);
    }

    public async Task<Result<bool>> RemoveAsync(Guid workOrderId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await FindAsync(workOrderId, taskId, cancellationToken);
        if (task is null)
        {
            return JobsErrors.TaskNotFound;
        }

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Only tasks of a work order the caller can see: an archived work order hides its checklist too.</summary>
    private async Task<WorkOrderTask?> FindAsync(Guid workOrderId, Guid taskId, CancellationToken cancellationToken)
    {
        var parent = WorkOrderId.From(workOrderId);
        var id = WorkOrderTaskId.From(taskId);
        var visible = await db.WorkOrders.AnyAsync(workOrder => workOrder.Id == parent, cancellationToken);
        return visible
            ? await db.Tasks.FirstOrDefaultAsync(task => task.Id == id && task.WorkOrderId == parent, cancellationToken)
            : null;
    }

    private static WorkOrderTaskResponse ToResponse(WorkOrderTask task) => new(task.Id.Value, task.Title, task.IsDone, task.DoneAt);
}
