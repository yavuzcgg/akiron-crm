using Akiron.Contracts.Identity;
using Akiron.Contracts.Jobs;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features;

internal sealed record StageResponse(Guid Id, string? Key, string? Name, string Category, int Position);

internal sealed record AssigneeResponse(Guid UserId, string FullName);

/// <summary>A work order as the board and lists show it.</summary>
internal sealed record WorkOrderCard(
    Guid Id,
    string Number,
    string Title,
    Guid StageId,
    string Priority,
    DateOnly? DueDate,
    Guid? PartyId,
    string? PartyName,
    IReadOnlyList<AssigneeResponse> Assignees,
    int TasksDone,
    int TasksTotal,
    int MinutesLogged,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt);

internal sealed record WorkOrderTaskResponse(Guid Id, string Title, bool IsDone, DateTimeOffset? DoneAt);

internal sealed record WorkOrderResponse(
    Guid Id,
    string Number,
    string Title,
    string? Description,
    StageResponse Stage,
    string Priority,
    DateOnly? DueDate,
    Guid? PartyId,
    string? PartyName,
    IReadOnlyList<AssigneeResponse> Assignees,
    IReadOnlyList<WorkOrderTaskResponse> Tasks,
    int MinutesLogged,
    int BillableMinutes,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt);

internal static class JobsApi
{
    public static string Priority(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Low => "low",
        WorkOrderPriority.High => "high",
        WorkOrderPriority.Urgent => "urgent",
        _ => "normal",
    };

    public static WorkOrderPriority ParsePriority(string? value) => value switch
    {
        "low" => WorkOrderPriority.Low,
        "high" => WorkOrderPriority.High,
        "urgent" => WorkOrderPriority.Urgent,
        _ => WorkOrderPriority.Normal,
    };

    public static readonly string[] Priorities = ["low", "normal", "high", "urgent"];

    public static string Category(StageCategory category) => category switch
    {
        StageCategory.Active => "active",
        StageCategory.Done => "done",
        _ => "open",
    };

    public static StageCategory ParseCategory(string value) => value switch
    {
        "active" => StageCategory.Active,
        "done" => StageCategory.Done,
        _ => StageCategory.Open,
    };

    public static readonly string[] Categories = ["open", "active", "done"];

    public static StageResponse ToResponse(Stage stage) =>
        new(stage.Id.Value, stage.Key, stage.Name, Category(stage.Category), stage.Position);

    public static StageRef ToRef(Stage stage) => new(stage.Id.Value, stage.Key, stage.Name);
}

/// <summary>Builds cards and details: task counts, logged minutes and assignee names in a few queries.</summary>
internal sealed class WorkOrderReader(JobsDbContext db, IMemberDirectory members)
{
    public async Task<IReadOnlyList<WorkOrderCard>> CardsAsync(IReadOnlyList<WorkOrder> workOrders, CancellationToken cancellationToken)
    {
        var ids = workOrders.Select(workOrder => workOrder.Id).ToList();

        var tasks = await db.Tasks
            .Where(task => ids.Contains(task.WorkOrderId))
            .GroupBy(task => task.WorkOrderId)
            .Select(group => new { WorkOrderId = group.Key, Total = group.Count(), Done = group.Count(task => task.DoneAt != null) })
            .ToDictionaryAsync(row => row.WorkOrderId, cancellationToken);

        var minutes = await db.TimeEntries
            .Where(entry => ids.Contains(entry.WorkOrderId) && entry.EndedAt != null)
            .GroupBy(entry => entry.WorkOrderId)
            .Select(group => new { WorkOrderId = group.Key, Minutes = group.Sum(entry => entry.Minutes) })
            .ToDictionaryAsync(row => row.WorkOrderId, row => row.Minutes, cancellationToken);

        var names = await members.FindAsync(workOrders.SelectMany(workOrder => workOrder.Assignees.Select(assignee => assignee.UserId)), cancellationToken);

        return workOrders.Select(workOrder => new WorkOrderCard(
                workOrder.Id.Value,
                workOrder.Number,
                workOrder.Title,
                workOrder.StageId.Value,
                JobsApi.Priority(workOrder.Priority),
                workOrder.DueDate,
                workOrder.PartyId,
                workOrder.PartyName,
                Assignees(workOrder, names),
                tasks.TryGetValue(workOrder.Id, out var counts) ? counts.Done : 0,
                counts?.Total ?? 0,
                minutes.GetValueOrDefault(workOrder.Id),
                workOrder.CompletedAt,
                workOrder.CreatedAt))
            .ToList();
    }

    public async Task<WorkOrderResponse> DetailAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var stage = await db.Stages.FirstAsync(candidate => candidate.Id == workOrder.StageId, cancellationToken);
        var tasks = await db.Tasks
            .Where(task => task.WorkOrderId == workOrder.Id)
            .OrderBy(task => task.Position)
            .Select(task => new WorkOrderTaskResponse(task.Id.Value, task.Title, task.DoneAt != null, task.DoneAt))
            .ToListAsync(cancellationToken);
        var time = await db.TimeEntries
            .Where(entry => entry.WorkOrderId == workOrder.Id && entry.EndedAt != null)
            .GroupBy(_ => 1)
            .Select(group => new { Total = group.Sum(entry => entry.Minutes), Billable = group.Where(entry => entry.IsBillable).Sum(entry => entry.Minutes) })
            .FirstOrDefaultAsync(cancellationToken);
        var names = await members.FindAsync(workOrder.Assignees.Select(assignee => assignee.UserId), cancellationToken);

        return new WorkOrderResponse(
            workOrder.Id.Value,
            workOrder.Number,
            workOrder.Title,
            workOrder.Description,
            JobsApi.ToResponse(stage),
            JobsApi.Priority(workOrder.Priority),
            workOrder.DueDate,
            workOrder.PartyId,
            workOrder.PartyName,
            Assignees(workOrder, names),
            tasks,
            time?.Total ?? 0,
            time?.Billable ?? 0,
            workOrder.CompletedAt,
            workOrder.CreatedAt);
    }

    /// <summary>People who left the organisation stay on old work orders, shown without a name.</summary>
    private static List<AssigneeResponse> Assignees(WorkOrder workOrder, IReadOnlyDictionary<Guid, MemberSummary> names) =>
        workOrder.Assignees
            .Select(assignee => new AssigneeResponse(assignee.UserId, names.TryGetValue(assignee.UserId, out var member) ? member.FullName : string.Empty))
            .OrderBy(assignee => assignee.FullName, StringComparer.Ordinal)
            .ToList();
}
