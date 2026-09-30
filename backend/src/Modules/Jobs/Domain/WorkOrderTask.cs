using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Jobs.Domain;

/// <summary>A checklist item on a work order ("İlk taslak", "Revize 1", "Teslim").</summary>
public sealed class WorkOrderTask : Entity<WorkOrderTaskId>, ITenantScoped, IAuditable
{
    public const int TitleMaxLength = 300;

    private WorkOrderTask(WorkOrderTaskId id, WorkOrderId workOrderId, string title, int position)
        : base(id)
    {
        WorkOrderId = workOrderId;
        Title = title;
        Position = position;
    }

    public TenantId TenantId { get; private set; }

    public WorkOrderId WorkOrderId { get; private set; }

    public string Title { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset? DoneAt { get; private set; }

    public bool IsDone => DoneAt is not null;

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static WorkOrderTask Create(WorkOrderId workOrderId, string title, int position) =>
        new(WorkOrderTaskId.New(), workOrderId, title.Trim(), position);

    public void Update(string title, bool isDone, DateTimeOffset now)
    {
        Title = title.Trim();
        DoneAt = isDone ? DoneAt ?? now : null;
    }
}
