using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.People;

/// <summary>
/// What an hour of each person costs the agency (TRY, internal accounting). Jobs snapshots it on
/// every time entry, so a raise changes future cost, not last month's.
/// </summary>
public interface IPeopleCosts
{
    /// <summary>Hourly cost by user; people without a cost set are left out.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> HourlyCostsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}

/// <summary>Someone asked for time off. <paramref name="Type"/>: annual, sick, unpaid, excuse, other.</summary>
[IntegrationEventName("people.leave.requested")]
public sealed record LeaveRequested(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid LeaveRequestId,
    Guid UserId,
    string UserName,
    string Type,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Days) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A leave request was approved or rejected (<paramref name="Approved"/>).</summary>
[IntegrationEventName("people.leave.decided")]
public sealed record LeaveDecided(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid LeaveRequestId,
    Guid UserId,
    string Type,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Days,
    bool Approved,
    string? Note,
    Guid DecidedByUserId,
    string? DecidedByName) : IntegrationEvent(TenantId, OccurredAt);
