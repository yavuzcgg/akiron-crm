using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.People.Domain;

public readonly record struct LeaveRequestId(Guid Value) : ITypedId<LeaveRequestId>
{
    public static LeaveRequestId New() => new(Guid.CreateVersion7());

    public static LeaveRequestId From(Guid value) => new(value);
}

public enum LeaveType
{
    /// <summary>Yıllık ücretli izin; the only type counted against the allowance.</summary>
    Annual,

    /// <summary>Rapor (sick note).</summary>
    Sick,

    /// <summary>Ücretsiz izin.</summary>
    Unpaid,

    /// <summary>Mazeret izni: marriage, bereavement, birth…</summary>
    Excuse,

    Other,
}

public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
}

/// <summary>
/// A request for time off and its single approval step: pending until someone with the approve
/// permission decides; the requester can withdraw it while it is pending or still in the future.
/// The general approval engine (expenses, content) grows out of this once those arrive.
/// </summary>
public sealed class LeaveRequest : Entity<LeaveRequestId>, ITenantScoped, IAuditable
{
    public const int NoteMaxLength = 500;

    private LeaveRequest(LeaveRequestId id, Guid userId, LeaveType type, DateOnly startDate, DateOnly endDate, bool halfDay, decimal days, string? note)
        : base(id)
    {
        UserId = userId;
        Type = type;
        StartDate = startDate;
        EndDate = endDate;
        HalfDay = halfDay;
        Days = days;
        Note = note;
        Status = LeaveStatus.Pending;
    }

    public TenantId TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public LeaveType Type { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    /// <summary>A half day off (one date only).</summary>
    public bool HalfDay { get; private set; }

    /// <summary>Working days taken, weekends and public holidays excluded (<see cref="WorkingDays"/>).</summary>
    public decimal Days { get; private set; }

    public string? Note { get; private set; }

    public LeaveStatus Status { get; private set; }

    public Guid? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? DecisionNote { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static LeaveRequest Submit(Guid userId, LeaveType type, DateOnly startDate, DateOnly endDate, bool halfDay, string? note)
    {
        var days = halfDay ? 0.5m : WorkingDays.Between(startDate, endDate);
        return new LeaveRequest(LeaveRequestId.New(), userId, type, startDate, halfDay ? startDate : endDate, halfDay, days, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
    }

    public bool Decide(bool approve, Guid deciderId, string? note, DateTimeOffset now)
    {
        if (Status != LeaveStatus.Pending)
        {
            return false;
        }

        Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        DecidedBy = deciderId;
        DecidedAt = now;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return true;
    }

    /// <summary>Pending requests, and approved ones that have not started yet, can be withdrawn.</summary>
    public bool Cancel(DateOnly today)
    {
        if (Status == LeaveStatus.Pending || (Status == LeaveStatus.Approved && StartDate > today))
        {
            Status = LeaveStatus.Cancelled;
            return true;
        }

        return false;
    }
}

/// <summary>
/// Working days in Türkiye: Monday to Friday minus the fixed-date public holidays. Religious
/// holidays move every year and arrive with the Reference module's calendar; until then an
/// approver can see and adjust (reject and ask again) when a request spans one.
/// </summary>
public static class WorkingDays
{
    private static readonly (int Month, int Day)[] FixedHolidays =
    [
        (1, 1),   // Yılbaşı
        (4, 23),  // Ulusal Egemenlik ve Çocuk Bayramı
        (5, 1),   // Emek ve Dayanışma Günü
        (5, 19),  // Atatürk'ü Anma, Gençlik ve Spor Bayramı
        (7, 15),  // Demokrasi ve Millî Birlik Günü
        (8, 30),  // Zafer Bayramı
        (10, 29), // Cumhuriyet Bayramı
    ];

    public static bool IsWorkingDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
        && !FixedHolidays.Contains((date.Month, date.Day));

    public static decimal Between(DateOnly start, DateOnly end)
    {
        var days = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (IsWorkingDay(date))
            {
                days++;
            }
        }

        return days;
    }
}
