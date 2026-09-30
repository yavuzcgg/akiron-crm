using Akiron.BuildingBlocks.Domain;
using Akiron.Contracts.Identity;
using Akiron.Modules.People.Domain;
using Akiron.Modules.People.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.People.Features.Employees;

internal sealed record UpdateEmployeeCommand(
    string? JobTitle,
    string? Department,
    string? Phone,
    DateOnly? StartDate,
    decimal? HourlyCost,
    int AnnualLeaveDays = Employee.DefaultAnnualLeaveDays);

internal sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeValidator()
    {
        RuleFor(command => command.JobTitle).MaximumLength(100);
        RuleFor(command => command.Department).MaximumLength(100);
        RuleFor(command => command.Phone).MaximumLength(30);
        RuleFor(command => command.HourlyCost).InclusiveBetween(0m, 1_000_000m);
        RuleFor(command => command.AnnualLeaveDays).InclusiveBetween(0, 60);
    }
}

/// <param name="HourlyCost">Only for callers with <c>people.costs.read</c>; null otherwise.</param>
/// <param name="OnLeaveToday">An approved leave covers today.</param>
internal sealed record EmployeeResponse(
    Guid UserId,
    string FullName,
    string? JobTitle,
    string? Department,
    string? Phone,
    DateOnly? StartDate,
    decimal? HourlyCost,
    int AnnualLeaveDays,
    bool OnLeaveToday);

/// <summary>The team directory: every member, with their profile when one has been filled in.</summary>
internal sealed class EmployeesHandler(PeopleDbContext db, IMemberDirectory members, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<EmployeeResponse>> ListAsync(bool withCosts, CancellationToken cancellationToken)
    {
        var people = await members.ListAsync(cancellationToken);
        var ids = people.Select(person => person.UserId).ToList();
        var profiles = await db.Employees.AsNoTracking().Where(employee => ids.Contains(employee.UserId)).ToDictionaryAsync(employee => employee.UserId, cancellationToken);
        var onLeave = await OnLeaveTodayAsync(ids, cancellationToken);

        return people.Select(person => ToResponse(person, profiles.GetValueOrDefault(person.UserId), withCosts, onLeave.Contains(person.UserId))).ToList();
    }

    public async Task<Result<EmployeeResponse>> GetAsync(Guid userId, bool withCosts, CancellationToken cancellationToken)
    {
        var person = (await members.FindAsync([userId], cancellationToken)).GetValueOrDefault(userId);
        if (person is null)
        {
            return PeopleErrors.NotAMember;
        }

        var profile = await db.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.UserId == userId, cancellationToken);
        var onLeave = await OnLeaveTodayAsync([userId], cancellationToken);
        return ToResponse(person, profile, withCosts, onLeave.Contains(userId));
    }

    public async Task<Result<EmployeeResponse>> UpdateAsync(Guid userId, UpdateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var person = (await members.FindAsync([userId], cancellationToken)).GetValueOrDefault(userId);
        if (person is null)
        {
            return PeopleErrors.NotAMember;
        }

        var profile = await db.Employees.FirstOrDefaultAsync(employee => employee.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = Employee.For(userId);
            db.Employees.Add(profile);
        }

        profile.Update(new EmployeeDetails(command.JobTitle, command.Department, command.Phone, command.StartDate, command.HourlyCost, command.AnnualLeaveDays));
        await db.SaveChangesAsync(cancellationToken);

        var onLeave = await OnLeaveTodayAsync([userId], cancellationToken);
        return ToResponse(person, profile, withCosts: true, onLeave.Contains(userId));
    }

    private async Task<HashSet<Guid>> OnLeaveTodayAsync(List<Guid> userIds, CancellationToken cancellationToken)
    {
        var today = LeaveCalendar.Today(timeProvider);
        var off = await db.LeaveRequests
            .Where(request => userIds.Contains(request.UserId) && request.Status == LeaveStatus.Approved
                && request.StartDate <= today && request.EndDate >= today)
            .Select(request => request.UserId)
            .ToListAsync(cancellationToken);
        return [.. off];
    }

    private static EmployeeResponse ToResponse(MemberSummary person, Employee? profile, bool withCosts, bool onLeaveToday) => new(
        person.UserId,
        person.FullName,
        profile?.JobTitle,
        profile?.Department,
        profile?.Phone,
        profile?.StartDate,
        withCosts ? profile?.HourlyCost : null,
        profile?.AnnualLeaveDays ?? Employee.DefaultAnnualLeaveDays,
        onLeaveToday);
}

internal static class LeaveCalendar
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>Today in Türkiye; "on leave today" must not flip at 03:00 local time.</summary>
    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Istanbul).DateTime);
}
