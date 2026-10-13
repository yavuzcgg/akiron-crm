using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts;
using Akiron.Contracts.Portal;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.ClientPortal;

/// <summary>
/// A work order as its client sees it: what it is, where it stands, when it is due, how far along.
/// Nothing internal: no description, task names, assignees, hours or budget.
/// </summary>
internal sealed record ClientWorkOrder(
    Guid Id,
    string Number,
    string Title,
    string? StageKey,
    string? StageName,
    string StageCategory,
    DateOnly? DueDate,
    DateTimeOffset? CompletedAt,
    int TasksDone,
    int TasksTotal,
    DateTimeOffset CreatedAt);

/// <summary>The signed-in client's work orders; every query is scoped to their party.</summary>
internal sealed class ClientWorkOrdersHandler(JobsDbContext db, IPortalVisitor visitor)
{
    public async Task<IReadOnlyList<ClientWorkOrder>> ListAsync(CancellationToken cancellationToken)
    {
        var partyId = visitor.PartyId;
        var rows = await LoadAsync(db.WorkOrders.AsNoTracking().Where(workOrder => workOrder.PartyId == partyId), cancellationToken);
        return rows.OrderBy(row => row.CompletedAt is not null).ThenByDescending(row => row.CreatedAt).ToList();
    }

    public async Task<Result<ClientWorkOrder>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var partyId = visitor.PartyId;
        var workOrderId = WorkOrderId.From(id);
        var rows = await LoadAsync(db.WorkOrders.AsNoTracking().Where(workOrder => workOrder.Id == workOrderId && workOrder.PartyId == partyId), cancellationToken);

        // Another party's work order is "not found", never "forbidden": its existence is not the client's business.
        return rows.Count == 0 ? JobsErrors.WorkOrderNotFound : rows[0];
    }

    private async Task<List<ClientWorkOrder>> LoadAsync(IQueryable<WorkOrder> workOrders, CancellationToken cancellationToken)
    {
        var rows = await workOrders
            .OrderByDescending(workOrder => workOrder.CreatedAt)
            .Take(200)
            .Join(db.Stages, workOrder => workOrder.StageId, stage => stage.Id, (workOrder, stage) => new
            {
                workOrder.Id,
                workOrder.Number,
                workOrder.Title,
                stage.Key,
                stage.Name,
                stage.Category,
                workOrder.DueDate,
                workOrder.CompletedAt,
                Done = db.Tasks.Count(task => task.WorkOrderId == workOrder.Id && task.DoneAt != null),
                Total = db.Tasks.Count(task => task.WorkOrderId == workOrder.Id),
                workOrder.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => new ClientWorkOrder(
                row.Id.Value, row.Number, row.Title, row.Key, row.Name, JobsApi.Category(row.Category), row.DueDate, row.CompletedAt, row.Done, row.Total, row.CreatedAt))
            .ToList();
    }
}

/// <summary>Tells Timeline and Files whose a work order is and who on the team hears from its client.</summary>
internal sealed class WorkOrderPortalSubject(JobsDbContext db) : IPortalSubject
{
    public string SubjectType => Subjects.WorkOrder;

    public async Task<PortalSubjectInfo?> FindAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var id = WorkOrderId.From(subjectId);
        var workOrder = await db.WorkOrders.AsNoTracking().Include(candidate => candidate.Assignees)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (workOrder?.PartyId is not { } partyId)
        {
            return null;
        }

        var staff = workOrder.Assignees.Select(assignee => assignee.UserId).ToList();
        if (workOrder.CreatedBy is { } creator && !staff.Contains(creator.Value))
        {
            staff.Add(creator.Value);
        }

        return new PortalSubjectInfo(partyId, $"{workOrder.Number} {workOrder.Title}", staff);
    }
}

internal static class ClientWorkOrderEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var portal = endpoints.MapGroup("/portal/work-orders");

        portal.MapGet("/", async (ClientWorkOrdersHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(cancellationToken)))
            .RequirePortal()
            .Produces<IReadOnlyList<ClientWorkOrder>>()
            .WithSummary("Portal: the client's work orders, open ones first");

        portal.MapGet("/{id:guid}", async (Guid id, ClientWorkOrdersHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.GetAsync(id, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .RequirePortal()
            .Produces<ClientWorkOrder>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Portal: one of the client's work orders");
    }
}
