using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Jobs.Features;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Jobs;

public static class JobsPermissions
{
    public const string WorkOrdersRead = "jobs.work_orders.read";
    public const string WorkOrdersWrite = "jobs.work_orders.write";
    public const string StagesManage = "jobs.stages.manage";

    /// <summary>Logging one's own time.</summary>
    public const string TimeWrite = "jobs.time.write";

    /// <summary>Seeing everyone's time: timesheets of the whole team.</summary>
    public const string TimeReadAll = "jobs.time.read_all";

    public static IReadOnlyCollection<string> All { get; } = [WorkOrdersRead, WorkOrdersWrite, StagesManage, TimeWrite, TimeReadAll];

    public static IReadOnlyCollection<string> Member { get; } = [WorkOrdersRead, WorkOrdersWrite, TimeWrite];
}

internal static class JobsErrors
{
    public static readonly Error WorkOrderNotFound = Error.NotFound("jobs.work_order.not_found", "No such work order in this organisation.");

    public static readonly Error StageNotFound = Error.NotFound("jobs.stage.not_found", "No such stage in this organisation.");

    public static readonly Error TaskNotFound = Error.NotFound("jobs.task.not_found", "No such task on this work order.");

    public static readonly Error PartyNotFound = Error.Validation("jobs.work_order.party_not_found", "The client does not exist in this organisation.");

    public static readonly Error AssigneeNotMember = Error.Validation("jobs.work_order.assignee_not_member", "Everyone assigned must be a member of this organisation.");

    public static readonly Error StageNotEmpty = Error.Rule("jobs.stage.not_empty", "Move the work orders out of the stage before removing it.");

    public static readonly Error LastStage = Error.Rule("jobs.stage.last_of_its_kind", "The board needs at least one open and one done stage.");

    public static readonly Error StageOrderMismatch = Error.Validation("jobs.stage.order_mismatch", "Send every stage exactly once.");

    public static readonly Error TimeEntryNotFound = Error.NotFound("jobs.time_entry.not_found", "No such time entry.");

    public static readonly Error NoRunningTimer = Error.Rule("jobs.timer.not_running", "No timer is running.");

    public static readonly Error TimerAlreadyRunning = Error.Conflict("jobs.timer.already_running", "A timer is already running.");
}

/// <summary>Work orders on a board with per-tenant stages, their checklists and logged time (Phase 2).</summary>
public sealed class JobsModule : IModule
{
    public string Name => JobsDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => JobsPermissions.All;

    public IReadOnlyCollection<string> MemberPermissions => JobsPermissions.Member;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<JobsDbContext>(configuration, JobsDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(JobsModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<JobsModule>(includeInternalTypes: true);
        services.AddIntegrationEventConsumersFromAssembly(typeof(JobsModule).Assembly);
        services.AddScoped<StageBoard>();
        services.AddScoped<WorkOrderReader>();
        services.AddScoped<Features.CreateWorkOrder.WorkOrderReferences>();
        services.Configure<ConstraintErrorMap>(map => map
            .Add(JobsConstraints.OneRunningTimer, JobsErrors.TimerAlreadyRunning)
            .Add(JobsConstraints.WorkOrderStage, JobsErrors.StageNotEmpty));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => JobsEndpoints.Map(endpoints);
}
