using System.Security.Claims;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Modules.Jobs.Features.ArchiveWorkOrder;
using Akiron.Modules.Jobs.Features.CreateWorkOrder;
using Akiron.Modules.Jobs.Features.GetWorkOrder;
using Akiron.Modules.Jobs.Features.ListWorkOrders;
using Akiron.Modules.Jobs.Features.MoveWorkOrder;
using Akiron.Modules.Jobs.Features.Stages;
using Akiron.Modules.Jobs.Features.Tasks;
using Akiron.Modules.Jobs.Features.TimeTracking;
using Akiron.Modules.Jobs.Features.UpdateWorkOrder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.Modules.Jobs.Features;

internal static class JobsEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapStages(endpoints.MapGroup("/stages"));
        MapWorkOrders(endpoints.MapGroup("/work-orders"));
        MapTime(endpoints.MapGroup("/time"));

        // Who can be put on a work order: the people of this organisation, names only.
        endpoints.MapGet("/assignable-members", async (IMemberDirectory members, CancellationToken cancellationToken) =>
                TypedResults.Ok(await members.ListAsync(cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersRead)
            .Produces<IReadOnlyList<MemberSummary>>()
            .WithSummary("People who can be assigned to work orders");
    }

    private static void MapStages(RouteGroupBuilder stages)
    {
        stages.MapGet("/", async (StagesHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersRead)
            .Produces<IReadOnlyList<StageResponse>>()
            .WithSummary("The board's stages, in order");

        stages.MapPost("/", async (StageCommand command, StagesHandler handler, CancellationToken cancellationToken) =>
                Created(await handler.CreateAsync(command, cancellationToken), stage => $"/api/v1/jobs/stages/{stage.Id}"))
            .RequirePermission(JobsPermissions.StagesManage)
            .Validate<StageCommand>()
            .Produces<StageResponse>(StatusCodes.Status201Created)
            .WithSummary("Add a stage (before the first done stage)");

        stages.MapPut("/{id:guid}", async (Guid id, StageCommand command, StagesHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.UpdateAsync(id, command, cancellationToken)))
            .RequirePermission(JobsPermissions.StagesManage)
            .Validate<StageCommand>()
            .Produces<StageResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Rename a stage or change what it means");

        stages.MapDelete("/{id:guid}", async (Guid id, StagesHandler handler, CancellationToken cancellationToken) =>
                NoContent(await handler.RemoveAsync(id, cancellationToken)))
            .RequirePermission(JobsPermissions.StagesManage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Remove an empty stage");

        stages.MapPut("/order", async (ReorderStagesCommand command, StagesHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.ReorderAsync(command, cancellationToken)))
            .RequirePermission(JobsPermissions.StagesManage)
            .Validate<ReorderStagesCommand>()
            .Produces<IReadOnlyList<StageResponse>>()
            .WithSummary("Put the stages in a new order");
    }

    private static void MapWorkOrders(RouteGroupBuilder workOrders)
    {
        workOrders.MapGet("/board", async ([AsParameters] WorkOrderFilter filter, ListWorkOrdersHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.BoardAsync(filter, cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersRead)
            .Validate<WorkOrderFilter>()
            .Produces<BoardResponse>()
            .WithSummary("Stages and cards for the board; finished cards only for 14 days");

        workOrders.MapGet("/", async ([AsParameters] PageRequest page, [AsParameters] WorkOrderFilter filter, ListWorkOrdersHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(page, filter, cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersRead)
            .Validate<PageRequest>()
            .Validate<WorkOrderFilter>()
            .Produces<PagedResult<WorkOrderCard>>()
            .WithSummary("Work orders, open first by due date");

        workOrders.MapGet("/{id:guid}", async (Guid id, GetWorkOrderHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.HandleAsync(id, cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersRead)
            .Produces<WorkOrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One work order with its checklist and time totals");

        workOrders.MapPost("/", async (CreateWorkOrderCommand command, CreateWorkOrderHandler handler, CancellationToken cancellationToken) =>
                Created(await handler.HandleAsync(command, cancellationToken), workOrder => $"/api/v1/jobs/work-orders/{workOrder.Id}"))
            .RequirePermission(JobsPermissions.WorkOrdersWrite)
            .Validate<CreateWorkOrderCommand>()
            .Produces<WorkOrderResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Open a work order");

        workOrders.MapPut("/{id:guid}", async (Guid id, UpdateWorkOrderCommand command, UpdateWorkOrderHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.HandleAsync(id, command, cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersWrite)
            .Validate<UpdateWorkOrderCommand>()
            .Produces<WorkOrderResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Change a work order's details and people");

        workOrders.MapPost("/{id:guid}/move", async (Guid id, MoveWorkOrderCommand command, MoveWorkOrderHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.HandleAsync(id, command, cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersWrite)
            .Validate<MoveWorkOrderCommand>()
            .Produces<WorkOrderCard>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Move a card to a stage and position");

        workOrders.MapDelete("/{id:guid}", async (Guid id, ArchiveWorkOrderHandler handler, CancellationToken cancellationToken) =>
                NoContent(await handler.HandleAsync(id, cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Archive a work order; its time and history stay");

        workOrders.MapPost("/{id:guid}/tasks", async (Guid id, AddTaskCommand command, TasksHandler handler, CancellationToken cancellationToken) =>
                Created(await handler.AddAsync(id, command, cancellationToken), _ => $"/api/v1/jobs/work-orders/{id}"))
            .RequirePermission(JobsPermissions.WorkOrdersWrite)
            .Validate<AddTaskCommand>()
            .Produces<WorkOrderTaskResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add a checklist item");

        workOrders.MapPut("/{id:guid}/tasks/{taskId:guid}", async (Guid id, Guid taskId, UpdateTaskCommand command, TasksHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.UpdateAsync(id, taskId, command, cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersWrite)
            .Validate<UpdateTaskCommand>()
            .Produces<WorkOrderTaskResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Tick or rename a checklist item");

        workOrders.MapDelete("/{id:guid}/tasks/{taskId:guid}", async (Guid id, Guid taskId, TasksHandler handler, CancellationToken cancellationToken) =>
                NoContent(await handler.RemoveAsync(id, taskId, cancellationToken)))
            .RequirePermission(JobsPermissions.WorkOrdersWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a checklist item");
    }

    private static void MapTime(RouteGroupBuilder time)
    {
        time.MapGet("/timer", async (TimeTrackingHandler handler, CancellationToken cancellationToken) =>
                await handler.RunningAsync(cancellationToken) is { } running ? Results.Ok(running) : Results.NoContent())
            .RequirePermission(JobsPermissions.TimeWrite)
            .Produces<TimeEntryResponse>()
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Your running timer, if any");

        time.MapPost("/timer/start", async (StartTimerCommand command, TimeTrackingHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.StartAsync(command, cancellationToken)))
            .RequirePermission(JobsPermissions.TimeWrite)
            .Validate<StartTimerCommand>()
            .Produces<TimeEntryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Start a timer on a work order (stops the one running)");

        time.MapPost("/timer/stop", async (TimeTrackingHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.StopAsync(cancellationToken)))
            .RequirePermission(JobsPermissions.TimeWrite)
            .Produces<TimeEntryResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Stop your running timer");

        time.MapPost("/entries", async (LogTimeCommand command, TimeTrackingHandler handler, CancellationToken cancellationToken) =>
                Created(await handler.LogAsync(command, cancellationToken), entry => $"/api/v1/jobs/time/entries/{entry.Id}"))
            .RequirePermission(JobsPermissions.TimeWrite)
            .Validate<LogTimeCommand>()
            .Produces<TimeEntryResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Log time after the fact");

        time.MapGet("/entries", async ([AsParameters] TimeEntryFilter filter, TimeTrackingHandler handler, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                Ok(await handler.ListAsync(filter, Holds(user, JobsPermissions.TimeReadAll), cancellationToken)))
            .RequirePermission(JobsPermissions.TimeWrite)
            .Validate<TimeEntryFilter>()
            .Produces<IReadOnlyList<TimeEntryResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Time entries between two days (yours, or someone's with jobs.time.read_all)");

        time.MapDelete("/entries/{id:guid}", async (Guid id, TimeTrackingHandler handler, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                NoContent(await handler.RemoveAsync(id, Holds(user, JobsPermissions.TimeReadAll), cancellationToken)))
            .RequirePermission(JobsPermissions.TimeWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a time entry (your own)");
    }

    private static bool Holds(ClaimsPrincipal user, string permission) =>
        user.FindAll(AkironClaimTypes.Permission).Any(claim => claim.Value == permission || claim.Value == PermissionNames.All);

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();

    private static IResult NoContent<T>(Result<T> result) => result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();

    private static IResult Created<T>(Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? TypedResults.Created(location(result.Value), result.Value) : result.Error.ToProblem();
}
