using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Jobs.Domain;

public enum WorkOrderPriority
{
    Low,
    Normal,
    High,
    Urgent,
}

/// <summary>The editable part of a work order; number, stage and assignees change through their own methods.</summary>
public sealed record WorkOrderDetails(
    string Title,
    string? Description,
    Guid? PartyId,
    string? PartyName,
    WorkOrderPriority Priority,
    DateOnly? DueDate);

/// <summary>
/// A job done for a client (iş emri): a logo, a shoot, a month of social media. It sits in one
/// stage of the board, has people on it, a checklist and logged time.
/// </summary>
public sealed class WorkOrder : Entity<WorkOrderId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 10_000;

    private readonly List<WorkOrderAssignee> _assignees = [];

    private WorkOrder(WorkOrderId id, string number, StageId stageId, double rank)
        : base(id)
    {
        Number = number;
        StageId = stageId;
        Rank = rank;
        Title = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>IS-2026-0001: per tenant and year, gap-free (document numbering, ADR-0004).</summary>
    public string Number { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    /// <summary>The client (a CRM party); optional for internal work.</summary>
    public Guid? PartyId { get; private set; }

    /// <summary>The party's name when it was linked, refreshed when the party is renamed; lists need no call to CRM.</summary>
    public string? PartyName { get; private set; }

    public StageId StageId { get; private set; }

    /// <summary>Order inside the stage; a card dropped between two others gets the midpoint.</summary>
    public double Rank { get; private set; }

    public WorkOrderPriority Priority { get; private set; }

    public DateOnly? DueDate { get; private set; }

    /// <summary>The accepted quote the work order was opened from, if any.</summary>
    public Guid? SourceQuoteId { get; private set; }

    /// <summary>The agreed price of the job in TRY, excluding VAT; what cost is weighed against.</summary>
    public decimal? Budget { get; private set; }

    /// <summary>Set when the work order enters a done stage, cleared when it leaves one.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyCollection<WorkOrderAssignee> Assignees => _assignees;

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static WorkOrder Create(string number, Stage stage, double rank, WorkOrderDetails details, DateTimeOffset now)
    {
        var workOrder = new WorkOrder(WorkOrderId.New(), number, stage.Id, rank);
        workOrder.Update(details);
        if (stage.Category == StageCategory.Done)
        {
            workOrder.CompletedAt = now;
        }

        return workOrder;
    }

    public void Update(WorkOrderDetails details)
    {
        Title = details.Title.Trim();
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        PartyId = details.PartyId;
        PartyName = details.PartyId is null ? null : details.PartyName;
        Priority = details.Priority;
        DueDate = details.DueDate;
    }

    public void RenameParty(string partyName) => PartyName = partyName;

    public void FromQuote(Guid quoteId, decimal netTotalTry)
    {
        SourceQuoteId = quoteId;
        SetBudget(netTotalTry);
    }

    public void SetBudget(decimal? budget) => Budget = budget is { } amount ? decimal.Round(amount, 2) : null;

    /// <summary>Moves the card; returns true when the stage (not only the order) changed.</summary>
    public bool MoveTo(Stage stage, double rank, DateTimeOffset now)
    {
        var changed = StageId != stage.Id;
        StageId = stage.Id;
        Rank = rank;
        if (changed)
        {
            CompletedAt = stage.Category == StageCategory.Done ? now : null;
        }

        return changed;
    }

    /// <summary>Changes only the order inside the current stage.</summary>
    public void Rerank(double rank) => Rank = rank;

    /// <summary>Makes the assignees exactly <paramref name="userIds"/>; returns the ones newly added.</summary>
    public IReadOnlyList<Guid> AssignExactly(IEnumerable<Guid> userIds)
    {
        var wanted = userIds.Distinct().ToHashSet();
        _assignees.RemoveAll(assignee => !wanted.Contains(assignee.UserId));

        var added = wanted.Where(userId => _assignees.All(assignee => assignee.UserId != userId)).ToList();
        _assignees.AddRange(added.Select(userId => new WorkOrderAssignee(Id, userId)));
        return added;
    }
}

/// <summary>A person on a work order.</summary>
public sealed class WorkOrderAssignee : ITenantScoped
{
    internal WorkOrderAssignee(WorkOrderId workOrderId, Guid userId)
    {
        WorkOrderId = workOrderId;
        UserId = userId;
    }

    public TenantId TenantId { get; private set; }

    public WorkOrderId WorkOrderId { get; private set; }

    public Guid UserId { get; private set; }
}
