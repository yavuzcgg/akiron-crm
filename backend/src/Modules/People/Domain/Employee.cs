using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.People.Domain;

public readonly record struct EmployeeId(Guid Value) : ITypedId<EmployeeId>
{
    public static EmployeeId New() => new(Guid.CreateVersion7());

    public static EmployeeId From(Guid value) => new(value);
}

public sealed record EmployeeDetails(
    string? JobTitle,
    string? Department,
    string? Phone,
    DateOnly? StartDate,
    decimal? HourlyCost,
    int AnnualLeaveDays);

/// <summary>
/// The HR side of a member (İK-lite, no payroll): title, department, start date, hourly cost and
/// yearly leave allowance. Identity owns who the person is; this owns what they do here.
/// </summary>
public sealed class Employee : Entity<EmployeeId>, ITenantScoped, IAuditable
{
    /// <summary>Turkish Labour Law 53: 14 days a year for one to five years of service.</summary>
    public const int DefaultAnnualLeaveDays = 14;

    private Employee(EmployeeId id, Guid userId)
        : base(id)
    {
        UserId = userId;
        AnnualLeaveDays = DefaultAnnualLeaveDays;
    }

    public TenantId TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string? JobTitle { get; private set; }

    public string? Department { get; private set; }

    public string? Phone { get; private set; }

    public DateOnly? StartDate { get; private set; }

    /// <summary>What an hour of this person costs the agency, in TRY (salary and overhead, set by an admin).</summary>
    public decimal? HourlyCost { get; private set; }

    public int AnnualLeaveDays { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static Employee For(Guid userId) => new(EmployeeId.New(), userId);

    public void Update(EmployeeDetails details)
    {
        JobTitle = Clean(details.JobTitle);
        Department = Clean(details.Department);
        Phone = Clean(details.Phone);
        StartDate = details.StartDate;
        HourlyCost = details.HourlyCost is { } cost ? decimal.Round(cost, 2) : null;
        AnnualLeaveDays = details.AnnualLeaveDays;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
