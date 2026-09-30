using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Jobs.Domain;

public readonly record struct WorkOrderTemplateId(Guid Value) : ITypedId<WorkOrderTemplateId>
{
    public static WorkOrderTemplateId New() => new(Guid.CreateVersion7());

    public static WorkOrderTemplateId From(Guid value) => new(value);
}

public sealed record WorkOrderTemplateDetails(
    string Name,
    string? Title,
    string? Description,
    WorkOrderPriority Priority,
    int? DueInDays,
    IReadOnlyList<string> Tasks);

/// <summary>
/// A recurring kind of job ("Logo tasarımı", "Aylık sosyal medya"): its default title, brief,
/// priority, lead time and checklist, so a new work order starts complete.
/// </summary>
public sealed class WorkOrderTemplate : Entity<WorkOrderTemplateId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 100;
    public const int MaxTasks = 50;

    private WorkOrderTemplate(WorkOrderTemplateId id)
        : base(id)
    {
        Name = string.Empty;
        Tasks = [];
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Title of the work orders it creates; the template name when empty.</summary>
    public string? Title { get; private set; }

    public string? Description { get; private set; }

    public WorkOrderPriority Priority { get; private set; }

    /// <summary>Due date as days from the day the work order is opened.</summary>
    public int? DueInDays { get; private set; }

    public IReadOnlyList<string> Tasks { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static WorkOrderTemplate Create(WorkOrderTemplateDetails details)
    {
        var template = new WorkOrderTemplate(WorkOrderTemplateId.New());
        template.Update(details);
        return template;
    }

    public void Update(WorkOrderTemplateDetails details)
    {
        Name = details.Name.Trim();
        Title = string.IsNullOrWhiteSpace(details.Title) ? null : details.Title.Trim();
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        Priority = details.Priority;
        DueInDays = details.DueInDays;
        Tasks = details.Tasks.Select(task => task.Trim()).Where(task => task.Length > 0).ToList();
    }
}
