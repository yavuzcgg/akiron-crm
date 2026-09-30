using System.Security.Claims;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.People.Features.Employees;
using Akiron.Modules.People.Features.Leave;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.Modules.People.Features;

internal static class PeopleEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var employees = endpoints.MapGroup("/employees");

        employees.MapGet("/", async (EmployeesHandler handler, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(user.HasPermission(PeoplePermissions.CostsRead), cancellationToken)))
            .RequirePermission(PeoplePermissions.Read)
            .Produces<IReadOnlyList<EmployeeResponse>>()
            .WithSummary("The team: every member with title, department and whether they are off today");

        employees.MapGet("/{userId:guid}", async (Guid userId, EmployeesHandler handler, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                Ok(await handler.GetAsync(userId, user.HasPermission(PeoplePermissions.CostsRead), cancellationToken)))
            .RequirePermission(PeoplePermissions.Read)
            .Produces<EmployeeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One person's profile");

        employees.MapPut("/{userId:guid}", async (Guid userId, UpdateEmployeeCommand command, EmployeesHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.UpdateAsync(userId, command, cancellationToken)))
            .RequirePermission(PeoplePermissions.Manage)
            .Validate<UpdateEmployeeCommand>()
            .Produces<EmployeeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set a person's title, department, allowance and hourly cost");

        var leave = endpoints.MapGroup("/leave");

        leave.MapGet("/mine", async (int? year, LeaveHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.MineAsync(year, cancellationToken)))
            .RequirePermission(PeoplePermissions.LeaveRequest)
            .Produces<MyLeaveResponse>()
            .WithSummary("Your requests and annual leave balance for a year");

        leave.MapPost("/", async (SubmitLeaveCommand command, LeaveHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.SubmitAsync(command, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/people/leave/{result.Value.Id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(PeoplePermissions.LeaveRequest)
            .Validate<SubmitLeaveCommand>()
            .Produces<LeaveResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Ask for time off; approvers are notified");

        leave.MapPost("/{id:guid}/cancel", async (Guid id, LeaveHandler handler, CancellationToken cancellationToken) =>
                Ok(await handler.CancelAsync(id, cancellationToken)))
            .RequirePermission(PeoplePermissions.LeaveRequest)
            .Produces<LeaveResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Withdraw your own request");

        leave.MapGet("/pending", async (LeaveHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.PendingAsync(cancellationToken)))
            .RequirePermission(PeoplePermissions.LeaveApprove)
            .Produces<IReadOnlyList<LeaveResponse>>()
            .WithSummary("Requests waiting for a decision");

        leave.MapPost("/{id:guid}/decide", async (Guid id, DecideLeaveCommand command, LeaveHandler handler, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                Ok(await handler.DecideAsync(id, command, user.HasPermission(PermissionNames.All), cancellationToken)))
            .RequirePermission(PeoplePermissions.LeaveApprove)
            .Validate<DecideLeaveCommand>()
            .Produces<LeaveResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithSummary("Approve or reject a request; the requester is notified");

        leave.MapGet("/calendar", async ([Microsoft.AspNetCore.Http.AsParameters] LeaveCalendarFilter filter, LeaveHandler handler, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.CalendarAsync(filter, user.HasPermission(PeoplePermissions.LeaveApprove), cancellationToken)))
            .RequirePermission(PeoplePermissions.Read)
            .Validate<LeaveCalendarFilter>()
            .Produces<IReadOnlyList<LeaveResponse>>()
            .WithSummary("Who is off between two days (the reason only for approvers)");
    }


    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
}
