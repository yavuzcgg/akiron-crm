using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Contracts.People;
using Akiron.Modules.People.Features;
using Akiron.Modules.People.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.People;

public static class PeoplePermissions
{
    /// <summary>The team directory and who is off when.</summary>
    public const string Read = "people.read";

    /// <summary>Editing profiles: title, department, allowance, hourly cost.</summary>
    public const string Manage = "people.manage";

    /// <summary>Seeing hourly costs.</summary>
    public const string CostsRead = "people.costs.read";

    public const string LeaveRequest = "people.leave.request";

    public const string LeaveApprove = "people.leave.approve";

    public static IReadOnlyCollection<string> All { get; } = [Read, Manage, CostsRead, LeaveRequest, LeaveApprove];

    public static IReadOnlyCollection<string> Member { get; } = [Read, LeaveRequest];
}

internal static class PeopleErrors
{
    public static readonly Error NotAMember = Error.NotFound("people.employee.not_found", "No such member in this organisation.");

    public static readonly Error LeaveNotFound = Error.NotFound("people.leave.not_found", "No such leave request.");

    public static readonly Error NoWorkingDays = Error.Validation("people.leave.no_working_days", "The dates hold no working day.");

    public static readonly Error Overlaps = Error.Conflict("people.leave.overlaps", "Another request already covers some of these days.");

    public static readonly Error OverAllowance = Error.Rule("people.leave.over_allowance", "Not enough annual leave left for this request.");

    public static readonly Error AlreadyDecided = Error.Rule("people.leave.already_decided", "This request was already decided or withdrawn.");

    public static readonly Error OwnRequest = Error.Rule("people.leave.own_request", "Someone else has to decide your own request.");

    public static readonly Error CannotCancel = Error.Rule("people.leave.cannot_cancel", "Only pending requests and approved ones that have not started can be withdrawn.");
}

/// <summary>HR-lite (MODULES.md, no payroll): profiles, hourly cost for job profitability, leave with approval.</summary>
public sealed class PeopleModule : IModule
{
    public string Name => PeopleDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => PeoplePermissions.All;

    public IReadOnlyCollection<string> MemberPermissions => PeoplePermissions.Member;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PeopleDbContext>(configuration, PeopleDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(PeopleModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<PeopleModule>(includeInternalTypes: true);
        services.AddScoped<IPeopleCosts, PeopleCosts>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => PeopleEndpoints.Map(endpoints);
}
