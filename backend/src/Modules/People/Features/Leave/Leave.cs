using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Identity;
using Akiron.Contracts.People;
using Akiron.Modules.People.Domain;
using Akiron.Modules.People.Features.Employees;
using Akiron.Modules.People.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.People.Features.Leave;

internal sealed record SubmitLeaveCommand(string Type, DateOnly StartDate, DateOnly EndDate, bool HalfDay = false, string? Note = null);

internal sealed class SubmitLeaveValidator : AbstractValidator<SubmitLeaveCommand>
{
    public static readonly string[] Types = ["annual", "sick", "unpaid", "excuse", "other"];

    public SubmitLeaveValidator()
    {
        RuleFor(command => command.Type).Must(type => Types.Contains(type)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.EndDate).GreaterThanOrEqualTo(command => command.StartDate);
        RuleFor(command => command.EndDate).Must((command, end) => end.DayNumber - command.StartDate.DayNumber < 90)
            .WithErrorCode("validation.range_too_long").WithMessage("A request covers at most 90 days.");
        RuleFor(command => command.EndDate).Equal(command => command.StartDate).When(command => command.HalfDay)
            .WithErrorCode("people.leave.half_day_one_date").WithMessage("A half day is a single date.");
        RuleFor(command => command.Note).MaximumLength(LeaveRequest.NoteMaxLength);
    }
}

internal sealed record DecideLeaveCommand(bool Approve, string? Note = null);

internal sealed class DecideLeaveValidator : AbstractValidator<DecideLeaveCommand>
{
    public DecideLeaveValidator() => RuleFor(command => command.Note).MaximumLength(LeaveRequest.NoteMaxLength);
}

internal sealed record LeaveCalendarFilter([FromQuery(Name = "from")] DateOnly From, [FromQuery(Name = "to")] DateOnly To);

internal sealed class LeaveCalendarFilterValidator : AbstractValidator<LeaveCalendarFilter>
{
    public LeaveCalendarFilterValidator()
    {
        RuleFor(filter => filter.To).GreaterThanOrEqualTo(filter => filter.From);
        RuleFor(filter => filter.To).Must((filter, to) => to.DayNumber - filter.From.DayNumber <= 92)
            .WithErrorCode("validation.range_too_long").WithMessage("Ask for at most three months at a time.");
    }
}

/// <param name="Type">Null in the team calendar for people who may not see why someone is off (sick notes are health data, KVKK).</param>
internal sealed record LeaveResponse(
    Guid Id,
    Guid UserId,
    string UserName,
    string? Type,
    DateOnly StartDate,
    DateOnly EndDate,
    bool HalfDay,
    decimal Days,
    string? Note,
    string Status,
    string? DecidedByName,
    string? DecisionNote,
    DateTimeOffset CreatedAt);

/// <summary>Annual leave for a year: the allowance, what approved requests used and what is still pending.</summary>
internal sealed record LeaveBalance(int Year, int Allowance, decimal Used, decimal Pending, decimal Remaining);

internal sealed record MyLeaveResponse(LeaveBalance Balance, IReadOnlyList<LeaveResponse> Requests);

internal sealed class LeaveHandler(
    PeopleDbContext db,
    IMemberDirectory members,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private Guid Me => (currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.")).Value;

    public async Task<MyLeaveResponse> MineAsync(int? year, CancellationToken cancellationToken)
    {
        var me = Me;
        var forYear = year ?? LeaveCalendar.Today(timeProvider).Year;
        var requests = await db.LeaveRequests.AsNoTracking()
            .Where(request => request.UserId == me && (request.StartDate.Year == forYear || request.EndDate.Year == forYear))
            .OrderByDescending(request => request.StartDate)
            .ToListAsync(cancellationToken);

        return new MyLeaveResponse(await BalanceAsync(me, forYear, cancellationToken), await ToResponsesAsync(requests, showType: true, cancellationToken));
    }

    public async Task<Result<LeaveResponse>> SubmitAsync(SubmitLeaveCommand command, CancellationToken cancellationToken)
    {
        var me = Me;
        var type = Enum.Parse<LeaveType>(command.Type, ignoreCase: true);
        var request = LeaveRequest.Submit(me, type, command.StartDate, command.EndDate, command.HalfDay, command.Note);
        if (request.Days == 0 || (command.HalfDay && !WorkingDays.IsWorkingDay(command.StartDate)))
        {
            return PeopleErrors.NoWorkingDays;
        }

        var overlaps = await db.LeaveRequests.AnyAsync(
            other => other.UserId == me
                && (other.Status == LeaveStatus.Pending || other.Status == LeaveStatus.Approved)
                && other.StartDate <= request.EndDate && other.EndDate >= request.StartDate,
            cancellationToken);
        if (overlaps)
        {
            return PeopleErrors.Overlaps;
        }

        if (type == LeaveType.Annual)
        {
            var balance = await BalanceAsync(me, request.StartDate.Year, cancellationToken);
            if (request.Days > balance.Remaining - balance.Pending)
            {
                return new Error(PeopleErrors.OverAllowance.Code, PeopleErrors.OverAllowance.Detail, ErrorKind.Rule)
                {
                    Parameters = new Dictionary<string, object?> { ["remaining"] = balance.Remaining - balance.Pending },
                };
            }
        }

        db.LeaveRequests.Add(request);
        db.Publish(new LeaveRequested(
            tenantContext.TenantId, timeProvider.GetUtcNow(), request.Id.Value, me, currentUser.DisplayName ?? string.Empty,
            Api(type), request.StartDate, request.EndDate, request.Days));
        await db.SaveChangesAsync(cancellationToken);

        return (await ToResponsesAsync([request], showType: true, cancellationToken))[0];
    }

    public async Task<Result<LeaveResponse>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = Me;
        var requestId = LeaveRequestId.From(id);
        var request = await db.LeaveRequests.FirstOrDefaultAsync(candidate => candidate.Id == requestId && candidate.UserId == me, cancellationToken);
        if (request is null)
        {
            return PeopleErrors.LeaveNotFound;
        }

        if (!request.Cancel(LeaveCalendar.Today(timeProvider)))
        {
            return PeopleErrors.CannotCancel;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await ToResponsesAsync([request], showType: true, cancellationToken))[0];
    }

    public async Task<IReadOnlyList<LeaveResponse>> PendingAsync(CancellationToken cancellationToken)
    {
        var pending = await db.LeaveRequests.AsNoTracking()
            .Where(request => request.Status == LeaveStatus.Pending)
            .OrderBy(request => request.StartDate)
            .Take(200)
            .ToListAsync(cancellationToken);
        return await ToResponsesAsync(pending, showType: true, cancellationToken);
    }

    /// <param name="mayDecideOwn">Owners hold every permission and nobody is above them.</param>
    public async Task<Result<LeaveResponse>> DecideAsync(Guid id, DecideLeaveCommand command, bool mayDecideOwn, CancellationToken cancellationToken)
    {
        var requestId = LeaveRequestId.From(id);
        var request = await db.LeaveRequests.FirstOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);
        if (request is null)
        {
            return PeopleErrors.LeaveNotFound;
        }

        var me = Me;
        if (request.UserId == me && !mayDecideOwn)
        {
            return PeopleErrors.OwnRequest;
        }

        var now = timeProvider.GetUtcNow();
        if (!request.Decide(command.Approve, me, command.Note, now))
        {
            return PeopleErrors.AlreadyDecided;
        }

        db.Publish(new LeaveDecided(
            request.TenantId, now, request.Id.Value, request.UserId, Api(request.Type), request.StartDate, request.EndDate, request.Days,
            command.Approve, request.DecisionNote, me, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        return (await ToResponsesAsync([request], showType: true, cancellationToken))[0];
    }

    /// <summary>Approved and pending leave overlapping the range, for the team calendar.</summary>
    public async Task<IReadOnlyList<LeaveResponse>> CalendarAsync(LeaveCalendarFilter filter, bool showTypes, CancellationToken cancellationToken)
    {
        var requests = await db.LeaveRequests.AsNoTracking()
            .Where(request => (request.Status == LeaveStatus.Approved || request.Status == LeaveStatus.Pending)
                && request.StartDate <= filter.To && request.EndDate >= filter.From)
            .OrderBy(request => request.StartDate)
            .Take(500)
            .ToListAsync(cancellationToken);

        var me = Me;
        var responses = await ToResponsesAsync(requests, showType: true, cancellationToken);
        return responses
            .Select(response => showTypes || response.UserId == me ? response : response with { Type = null, Note = null, DecisionNote = null })
            .ToList();
    }

    private async Task<LeaveBalance> BalanceAsync(Guid userId, int year, CancellationToken cancellationToken)
    {
        var allowance = await db.Employees.Where(employee => employee.UserId == userId).Select(employee => (int?)employee.AnnualLeaveDays).FirstOrDefaultAsync(cancellationToken)
            ?? Employee.DefaultAnnualLeaveDays;
        var annual = await db.LeaveRequests
            .Where(request => request.UserId == userId && request.Type == LeaveType.Annual && request.StartDate.Year == year
                && (request.Status == LeaveStatus.Approved || request.Status == LeaveStatus.Pending))
            .Select(request => new { request.Status, request.Days })
            .ToListAsync(cancellationToken);

        var used = annual.Where(request => request.Status == LeaveStatus.Approved).Sum(request => request.Days);
        var pending = annual.Where(request => request.Status == LeaveStatus.Pending).Sum(request => request.Days);
        return new LeaveBalance(year, allowance, used, pending, allowance - used);
    }

    private async Task<IReadOnlyList<LeaveResponse>> ToResponsesAsync(IReadOnlyList<LeaveRequest> requests, bool showType, CancellationToken cancellationToken)
    {
        var names = await members.FindAsync(requests.SelectMany(request => request.DecidedBy is { } decider ? [request.UserId, decider] : new[] { request.UserId }), cancellationToken);
        string? NameOf(Guid? userId) => userId is { } id && names.TryGetValue(id, out var member) ? member.FullName : null;

        return requests.Select(request => new LeaveResponse(
                request.Id.Value,
                request.UserId,
                NameOf(request.UserId) ?? string.Empty,
                showType ? Api(request.Type) : null,
                request.StartDate,
                request.EndDate,
                request.HalfDay,
                request.Days,
                request.Note,
                request.Status.ToString().ToLowerInvariant(),
                NameOf(request.DecidedBy),
                request.DecisionNote,
                request.CreatedAt))
            .ToList();
    }

    private static string Api(LeaveType type) => type.ToString().ToLowerInvariant();
}
