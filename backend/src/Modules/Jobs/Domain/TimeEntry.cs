using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Jobs.Domain;

/// <summary>
/// Time someone spent on a work order: a running timer (no <see cref="EndedAt"/>) or a finished
/// block. The minutes feed work order cost and, with hourly costs from People, profitability.
/// </summary>
public sealed class TimeEntry : Entity<TimeEntryId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NoteMaxLength = 500;

    /// <summary>One entry is at most a day; a timer left running over the weekend is a mistake, not work.</summary>
    public const int MaxMinutes = 24 * 60;

    private TimeEntry(TimeEntryId id, Guid userId, WorkOrderId workOrderId, DateTimeOffset startedAt)
        : base(id)
    {
        UserId = userId;
        WorkOrderId = workOrderId;
        StartedAt = startedAt;
    }

    public TenantId TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkOrderId WorkOrderId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public int Minutes { get; private set; }

    public string? Note { get; private set; }

    public bool IsBillable { get; private set; }

    public bool IsRunning => EndedAt is null;

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static TimeEntry Start(Guid userId, WorkOrderId workOrderId, string? note, bool isBillable, DateTimeOffset now) =>
        new(TimeEntryId.New(), userId, workOrderId, now) { Note = Clean(note), IsBillable = isBillable };

    public static TimeEntry Log(Guid userId, WorkOrderId workOrderId, DateTimeOffset startedAt, int minutes, string? note, bool isBillable) =>
        new(TimeEntryId.New(), userId, workOrderId, startedAt)
        {
            EndedAt = startedAt.AddMinutes(minutes),
            Minutes = minutes,
            Note = Clean(note),
            IsBillable = isBillable,
        };

    /// <summary>Stops a running timer; rounds up to a whole minute and caps at <see cref="MaxMinutes"/>.</summary>
    public void Stop(DateTimeOffset now)
    {
        if (!IsRunning)
        {
            return;
        }

        var minutes = (int)Math.Ceiling(Math.Max(0, (now - StartedAt).TotalMinutes));
        Minutes = Math.Clamp(minutes, 1, MaxMinutes);
        EndedAt = StartedAt.AddMinutes(Minutes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
