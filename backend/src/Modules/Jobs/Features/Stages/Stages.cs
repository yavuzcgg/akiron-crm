using Akiron.BuildingBlocks.Domain;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.Stages;

internal sealed record StageCommand(string Name, string Category);

internal sealed class StageValidator : AbstractValidator<StageCommand>
{
    public StageValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Stage.NameMaxLength);
        RuleFor(command => command.Category).Must(category => JobsApi.Categories.Contains(category)).WithErrorCode("validation.invalid_value");
    }
}

internal sealed record ReorderStagesCommand(IReadOnlyList<Guid> StageIds);

internal sealed class ReorderStagesValidator : AbstractValidator<ReorderStagesCommand>
{
    public ReorderStagesValidator() => RuleFor(command => command.StageIds).NotEmpty();
}

/// <summary>The tenant's own workflow: add, rename, reorder and remove board columns.</summary>
internal sealed class StagesHandler(JobsDbContext db, StageBoard board)
{
    public async Task<IReadOnlyList<StageResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await board.StagesAsync(cancellationToken)).Select(JobsApi.ToResponse).ToList();

    public async Task<Result<StageResponse>> CreateAsync(StageCommand command, CancellationToken cancellationToken)
    {
        var stages = await board.StagesAsync(cancellationToken);

        // New stages go before the first done stage, which is where work usually is added.
        var firstDone = stages.FindIndex(stage => stage.Category == StageCategory.Done);
        var position = firstDone < 0 ? stages.Count : firstDone;
        var stage = Stage.Create(command.Name, JobsApi.ParseCategory(command.Category), position);

        stages.Insert(position, stage);
        Renumber(stages);
        db.Stages.Add(stage);
        await db.SaveChangesAsync(cancellationToken);
        return JobsApi.ToResponse(stage);
    }

    public async Task<Result<StageResponse>> UpdateAsync(Guid id, StageCommand command, CancellationToken cancellationToken)
    {
        var stages = await board.StagesAsync(cancellationToken);
        var stage = stages.FirstOrDefault(candidate => candidate.Id == StageId.From(id));
        if (stage is null)
        {
            return JobsErrors.StageNotFound;
        }

        var category = JobsApi.ParseCategory(command.Category);
        if (category != stage.Category && IsLastOfItsKind(stage, stages))
        {
            return JobsErrors.LastStage;
        }

        stage.Update(command.Name, category);
        await db.SaveChangesAsync(cancellationToken);
        return JobsApi.ToResponse(stage);
    }

    public async Task<Result<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var stages = await board.StagesAsync(cancellationToken);
        var stage = stages.FirstOrDefault(candidate => candidate.Id == StageId.From(id));
        if (stage is null)
        {
            return JobsErrors.StageNotFound;
        }

        // Archived work orders still point at their stage; the foreign key refuses those
        // (JobsConstraints.WorkOrderStage maps to the same error).
        if (await db.WorkOrders.AnyAsync(workOrder => workOrder.StageId == stage.Id, cancellationToken))
        {
            return JobsErrors.StageNotEmpty;
        }

        if (IsLastOfItsKind(stage, stages))
        {
            return JobsErrors.LastStage;
        }

        stages.Remove(stage);
        Renumber(stages);
        db.Stages.Remove(stage);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Result<IReadOnlyList<StageResponse>>> ReorderAsync(ReorderStagesCommand command, CancellationToken cancellationToken)
    {
        var stages = await board.StagesAsync(cancellationToken);
        if (command.StageIds.Count != stages.Count || command.StageIds.Distinct().Count() != stages.Count
            || stages.Any(stage => !command.StageIds.Contains(stage.Id.Value)))
        {
            return JobsErrors.StageOrderMismatch;
        }

        var ordered = command.StageIds.Select(id => stages.First(stage => stage.Id.Value == id)).ToList();
        Renumber(ordered);
        await db.SaveChangesAsync(cancellationToken);
        return ordered.Select(JobsApi.ToResponse).ToList();
    }

    /// <summary>The board needs somewhere to add work (open) and somewhere to finish it (done).</summary>
    private static bool IsLastOfItsKind(Stage stage, List<Stage> stages) =>
        stage.Category != StageCategory.Active && !stages.Any(other => other != stage && other.Category == stage.Category);

    private static void Renumber(List<Stage> stages)
    {
        for (var index = 0; index < stages.Count; index++)
        {
            stages[index].MoveTo(index);
        }
    }
}
