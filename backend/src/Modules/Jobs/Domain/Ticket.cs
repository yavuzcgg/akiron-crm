using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Jobs.Domain;

public readonly record struct TicketId(Guid Value) : ITypedId<TicketId>
{
    public static TicketId New() => new(Guid.CreateVersion7());

    public static TicketId From(Guid value) => new(value);
}

public enum TicketStatus
{
    /// <summary>Waiting for the team.</summary>
    Open,

    InProgress,

    /// <summary>The team asked the client something; the clock is the client's.</summary>
    WaitingClient,

    /// <summary>The team considers it done; a word from the client reopens it.</summary>
    Resolved,

    /// <summary>Done and confirmed (or closed by the client).</summary>
    Closed,
}

/// <summary>
/// A client's request for help (destek talebi): something is broken, something small is needed, a
/// question. Smaller than a work order and may become one. Its conversation lives on the timeline
/// (subject "ticket"), visible to both sides. A simple promise is kept per priority: how soon someone
/// answers for the first time.
/// </summary>
public sealed class Ticket : Entity<TicketId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int SubjectMaxLength = 200;
    public const int DescriptionMaxLength = 5000;

    private Ticket(TicketId id, string number)
        : base(id)
    {
        Number = number;
        Subject = string.Empty;
        PartyName = string.Empty;
        RequesterName = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>DST-2026-0001.</summary>
    public string Number { get; private set; }

    public Guid PartyId { get; private set; }

    public string PartyName { get; private set; }

    public string Subject { get; private set; }

    public string? Description { get; private set; }

    public TicketStatus Status { get; private set; }

    public WorkOrderPriority Priority { get; private set; }

    /// <summary>Whether the client opened it in the portal (otherwise staff did, on their word).</summary>
    public bool FromClient { get; private set; }

    public string RequesterName { get; private set; }

    public Guid? AssigneeUserId { get; private set; }

    public string? AssigneeName { get; private set; }

    /// <summary>When the first answer is due, by priority.</summary>
    public DateTimeOffset ResponseDueAt { get; private set; }

    public DateTimeOffset? FirstResponseAt { get; private set; }

    /// <summary>Whether the "nobody answered" reminder already went out.</summary>
    public bool OverdueNotified { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>The work order opened from this ticket, if it turned out to be work.</summary>
    public WorkOrderId? WorkOrderId { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>How long the first answer may take, by priority (clock hours).</summary>
    public static TimeSpan ResponseTarget(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Urgent => TimeSpan.FromHours(4),
        WorkOrderPriority.High => TimeSpan.FromHours(8),
        WorkOrderPriority.Low => TimeSpan.FromHours(72),
        _ => TimeSpan.FromHours(24),
    };

    public static Ticket Open(
        string number, Guid partyId, string partyName, string subject, string? description, WorkOrderPriority priority,
        bool fromClient, string requesterName, DateTimeOffset now)
    {
        var ticket = new Ticket(TicketId.New(), number)
        {
            PartyId = partyId,
            PartyName = partyName,
            Status = TicketStatus.Open,
            FromClient = fromClient,
            RequesterName = requesterName.Trim(),
            ResponseDueAt = now + ResponseTarget(priority),
        };
        ticket.Describe(subject, description, priority, now);

        // A ticket staff opened while talking to the client has had its first answer.
        if (!fromClient)
        {
            ticket.FirstResponseAt = now;
        }

        return ticket;
    }

    public void Describe(string subject, string? description, WorkOrderPriority priority, DateTimeOffset openedAt)
    {
        Subject = subject.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (priority != Priority || ResponseDueAt == default)
        {
            Priority = priority;
            ResponseDueAt = openedAt + ResponseTarget(priority);
            OverdueNotified = false;
        }
    }

    /// <summary>Returns whether the assignee changed.</summary>
    public bool Assign(Guid? userId, string? name)
    {
        if (AssigneeUserId == userId)
        {
            return false;
        }

        AssigneeUserId = userId;
        AssigneeName = userId is null ? null : name;
        return true;
    }

    /// <summary>Moves the ticket; returns the status it left, or null when nothing changed.</summary>
    public TicketStatus? MoveTo(TicketStatus status, DateTimeOffset now)
    {
        if (status == Status)
        {
            return null;
        }

        var from = Status;
        Status = status;
        ResolvedAt = IsOpen ? null : ResolvedAt ?? now;
        return from;
    }

    /// <summary>Someone on the team wrote to the client: the first answer is given, and an open ticket is being worked on.</summary>
    public TicketStatus? StaffAnswered(DateTimeOffset now)
    {
        FirstResponseAt ??= now;
        return Status == TicketStatus.Open ? MoveTo(TicketStatus.InProgress, now) : null;
    }

    /// <summary>The client wrote: whatever the team was waiting for (or thought solved) is back on the team's desk.</summary>
    public TicketStatus? ClientWrote(DateTimeOffset now) =>
        Status is TicketStatus.WaitingClient or TicketStatus.Resolved ? MoveTo(TicketStatus.Open, now) : null;

    public void LinkWorkOrder(WorkOrderId workOrderId) => WorkOrderId = workOrderId;

    /// <summary>Whether the first answer is late and nobody was told yet; marks it told.</summary>
    public bool TakeOverdueIfDue(DateTimeOffset now)
    {
        if (!IsOpen || FirstResponseAt is not null || OverdueNotified || ResponseDueAt > now)
        {
            return false;
        }

        OverdueNotified = true;
        return true;
    }
}
