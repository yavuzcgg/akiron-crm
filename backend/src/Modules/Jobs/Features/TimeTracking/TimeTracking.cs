using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Security;
using Akiron.Contracts.Identity;
using Akiron.Contracts.People;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.TimeTracking;

internal sealed record StartTimerCommand(Guid WorkOrderId, string? Note = null, bool IsBillable = true);

internal sealed class StartTimerValidator : AbstractValidator<StartTimerCommand>
{
    public StartTimerValidator() => RuleFor(command => command.Note).MaximumLength(TimeEntry.NoteMaxLength);
}

/// <param name="Date">The calendar day the work was done (Europe/Istanbul).</param>
internal sealed record LogTimeCommand(Guid WorkOrderId, DateOnly Date, int Minutes, string? Note = null, bool IsBillable = true);

internal sealed class LogTimeValidator : AbstractValidator<LogTimeCommand>
{
    public LogTimeValidator()
    {
        RuleFor(command => command.Minutes).InclusiveBetween(1, TimeEntry.MaxMinutes);
        RuleFor(command => command.Note).MaximumLength(TimeEntry.NoteMaxLength);
    }
}

/// <summary>Bound from the query string. Without <c>userId</c> it is the caller's own time.</summary>
internal sealed record TimeEntryFilter(
    [FromQuery(Name = "from")] DateOnly From,
    [FromQuery(Name = "to")] DateOnly To,
    [FromQuery(Name = "userId")] Guid? UserId = null,
    [FromQuery(Name = "workOrderId")] Guid? WorkOrderId = null);

internal sealed class TimeEntryFilterValidator : AbstractValidator<TimeEntryFilter>
{
    public const int MaxDays = 62;

    public TimeEntryFilterValidator()
    {
        RuleFor(filter => filter.To).GreaterThanOrEqualTo(filter => filter.From);
        RuleFor(filter => filter.To).Must((filter, to) => to.DayNumber - filter.From.DayNumber < MaxDays)
            .WithErrorCode("validation.range_too_long")
            .WithMessage("Ask for at most 62 days at a time.");
    }
}

internal sealed record TimeEntryResponse(
    Guid Id,
    Guid UserId,
    string UserName,
    Guid WorkOrderId,
    string WorkOrderNumber,
    string WorkOrderTitle,
    string? PartyName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int Minutes,
    string? Note,
    bool IsBillable);

/// <summary>
/// The timer (one running per person, enforced by a unique index), manual entries and the
/// timesheet. Everyone logs their own time; reading others' needs <c>jobs.time.read_all</c>.
/// </summary>
internal sealed class TimeTrackingHandler(JobsDbContext db, IMemberDirectory members, IPeopleCosts costs, ICurrentUser currentUser, TimeProvider timeProvider)
{
    /// <summary>Calendar days are Turkish days: a manual entry for "Tuesday" is Tuesday in Istanbul.</summary>
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private Guid Me => (currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.")).Value;

    public async Task<TimeEntryResponse?> RunningAsync(CancellationToken cancellationToken)
    {
        var me = Me;
        var running = await db.TimeEntries.AsNoTracking().FirstOrDefaultAsync(entry => entry.UserId == me && entry.EndedAt == null, cancellationToken);
        return running is null ? null : (await ToResponsesAsync([running], cancellationToken))[0];
    }

    /// <summary>Starts a timer; one already running is stopped first, as a stopwatch would.</summary>
    public async Task<Result<TimeEntryResponse>> StartAsync(StartTimerCommand command, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(command.WorkOrderId);
        if (!await db.WorkOrders.AnyAsync(workOrder => workOrder.Id == workOrderId, cancellationToken))
        {
            return JobsErrors.WorkOrderNotFound;
        }

        var me = Me;
        var now = timeProvider.GetUtcNow();
        var running = await db.TimeEntries.FirstOrDefaultAsync(entry => entry.UserId == me && entry.EndedAt == null, cancellationToken);
        if (running is not null)
        {
            running.Stop(now, await CostOfAsync(me, cancellationToken));

            // The stop must reach the database before the new row, or the one-timer index refuses it.
            await db.SaveChangesAsync(cancellationToken);
        }

        var entry = TimeEntry.Start(me, workOrderId, command.Note, command.IsBillable, now);
        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return (await ToResponsesAsync([entry], cancellationToken))[0];
    }

    public async Task<Result<TimeEntryResponse>> StopAsync(CancellationToken cancellationToken)
    {
        var me = Me;
        var running = await db.TimeEntries.FirstOrDefaultAsync(entry => entry.UserId == me && entry.EndedAt == null, cancellationToken);
        if (running is null)
        {
            return JobsErrors.NoRunningTimer;
        }

        running.Stop(timeProvider.GetUtcNow(), await CostOfAsync(me, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return (await ToResponsesAsync([running], cancellationToken))[0];
    }

    public async Task<Result<TimeEntryResponse>> LogAsync(LogTimeCommand command, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(command.WorkOrderId);
        if (!await db.WorkOrders.AnyAsync(workOrder => workOrder.Id == workOrderId, cancellationToken))
        {
            return JobsErrors.WorkOrderNotFound;
        }

        // Manual entries carry the day, not the hour: they start at 09:00 local time.
        var local = command.Date.ToDateTime(new TimeOnly(9, 0));
        var startedAt = new DateTimeOffset(local, Istanbul.GetUtcOffset(local)).ToUniversalTime();
        var me = Me;
        var entry = TimeEntry.Log(me, workOrderId, startedAt, command.Minutes, command.Note, command.IsBillable, await CostOfAsync(me, cancellationToken));
        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return (await ToResponsesAsync([entry], cancellationToken))[0];
    }

    public async Task<Result<bool>> RemoveAsync(Guid id, bool mayManageOthers, CancellationToken cancellationToken)
    {
        var entryId = TimeEntryId.From(id);
        var me = Me;
        var entry = await db.TimeEntries.FirstOrDefaultAsync(candidate => candidate.Id == entryId && (mayManageOthers || candidate.UserId == me), cancellationToken);
        if (entry is null)
        {
            return JobsErrors.TimeEntryNotFound;
        }

        db.TimeEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Result<IReadOnlyList<TimeEntryResponse>>> ListAsync(TimeEntryFilter filter, bool mayReadOthers, CancellationToken cancellationToken)
    {
        var me = Me;
        var userId = filter.UserId ?? me;
        if (userId != me && !mayReadOthers)
        {
            return Error.Forbidden("common.auth.forbidden", "Reading someone else's time needs jobs.time.read_all.");
        }

        var from = StartOfDay(filter.From);
        var to = StartOfDay(filter.To.AddDays(1));
        var query = db.TimeEntries.AsNoTracking()
            .Where(entry => entry.StartedAt >= from && entry.StartedAt < to);

        // A work order's own time is everyone's time on it, which only the timesheet readers see.
        query = filter.WorkOrderId is { } workOrderId && mayReadOthers && filter.UserId is null
            ? query.Where(entry => entry.WorkOrderId == WorkOrderId.From(workOrderId))
            : query.Where(entry => entry.UserId == userId);
        if (filter.WorkOrderId is { } onlyWorkOrder)
        {
            query = query.Where(entry => entry.WorkOrderId == WorkOrderId.From(onlyWorkOrder));
        }

        var entries = await query.OrderBy(entry => entry.StartedAt).Take(2000).ToListAsync(cancellationToken);
        return (await ToResponsesAsync(entries, cancellationToken)).ToList();
    }

    private async Task<decimal?> CostOfAsync(Guid userId, CancellationToken cancellationToken) =>
        (await costs.HourlyCostsAsync([userId], cancellationToken)).TryGetValue(userId, out var cost) ? cost : null;

    private static DateTimeOffset StartOfDay(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Istanbul.GetUtcOffset(local)).ToUniversalTime();
    }

    private async Task<IReadOnlyList<TimeEntryResponse>> ToResponsesAsync(IReadOnlyList<TimeEntry> entries, CancellationToken cancellationToken)
    {
        var workOrderIds = entries.Select(entry => entry.WorkOrderId).Distinct().ToList();

        // Archived work orders keep their time; their number and title still label it.
        var workOrders = await db.WorkOrders.IncludingArchived().AsNoTracking()
            .Where(workOrder => workOrderIds.Contains(workOrder.Id))
            .Select(workOrder => new { workOrder.Id, workOrder.Number, workOrder.Title, workOrder.PartyName })
            .ToDictionaryAsync(workOrder => workOrder.Id, cancellationToken);
        var names = await members.FindAsync(entries.Select(entry => entry.UserId), cancellationToken);

        return entries.Select(entry =>
            {
                var workOrder = workOrders[entry.WorkOrderId];
                return new TimeEntryResponse(
                    entry.Id.Value,
                    entry.UserId,
                    names.TryGetValue(entry.UserId, out var member) ? member.FullName : string.Empty,
                    entry.WorkOrderId.Value,
                    workOrder.Number,
                    workOrder.Title,
                    workOrder.PartyName,
                    entry.StartedAt,
                    entry.EndedAt,
                    entry.Minutes,
                    entry.Note,
                    entry.IsBillable);
            })
            .ToList();
    }
}
