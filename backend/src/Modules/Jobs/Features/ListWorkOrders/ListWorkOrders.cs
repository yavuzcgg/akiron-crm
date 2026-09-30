using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.ListWorkOrders;

/// <summary>Bound from the query string; shared by the board and the paged list.</summary>
internal sealed record WorkOrderFilter(
    [FromQuery(Name = "search")] string? Search = null,
    [FromQuery(Name = "partyId")] Guid? PartyId = null,
    [FromQuery(Name = "mine")] bool Mine = false,
    [FromQuery(Name = "includeDone")] bool IncludeDone = false);

internal sealed class WorkOrderFilterValidator : AbstractValidator<WorkOrderFilter>
{
    public WorkOrderFilterValidator() => RuleFor(filter => filter.Search).MaximumLength(100);
}

internal sealed record BoardResponse(IReadOnlyList<StageResponse> Stages, IReadOnlyList<WorkOrderCard> WorkOrders);

internal sealed class ListWorkOrdersHandler(JobsDbContext db, StageBoard board, WorkOrderReader reader, ICurrentUser currentUser, TimeProvider timeProvider)
{
    /// <summary>A board shows everything in progress; finished cards only while they are recent.</summary>
    public static readonly TimeSpan DoneVisibleFor = TimeSpan.FromDays(14);

    private const int BoardLimit = 500;

    public async Task<BoardResponse> BoardAsync(WorkOrderFilter filter, CancellationToken cancellationToken)
    {
        var stages = await board.StagesAsync(cancellationToken);
        var doneSince = timeProvider.GetUtcNow() - DoneVisibleFor;

        var workOrders = await Filtered(filter)
            .Where(workOrder => workOrder.CompletedAt == null || workOrder.CompletedAt >= doneSince)
            .OrderBy(workOrder => workOrder.Rank)
            .Take(BoardLimit)
            .ToListAsync(cancellationToken);

        return new BoardResponse(stages.Select(JobsApi.ToResponse).ToList(), await reader.CardsAsync(workOrders, cancellationToken));
    }

    public async Task<PagedResult<WorkOrderCard>> ListAsync(PageRequest page, WorkOrderFilter filter, CancellationToken cancellationToken)
    {
        var query = Filtered(filter);
        if (!filter.IncludeDone)
        {
            query = query.Where(workOrder => workOrder.CompletedAt == null);
        }

        var result = await query
            .OrderBy(workOrder => workOrder.CompletedAt != null)
            .ThenBy(workOrder => workOrder.DueDate == null)
            .ThenBy(workOrder => workOrder.DueDate)
            .ThenByDescending(workOrder => workOrder.CreatedAt)
            .ToPagedResultAsync(page, cancellationToken);

        return new PagedResult<WorkOrderCard>(await reader.CardsAsync(result.Items, cancellationToken), result.Page, result.PageSize, result.TotalCount);
    }

    private IQueryable<WorkOrder> Filtered(WorkOrderFilter filter)
    {
        var query = db.WorkOrders.AsNoTracking().Include(workOrder => workOrder.Assignees).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = "%" + filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(workOrder => EF.Functions.ILike(workOrder.Title, pattern, "\\")
                || EF.Functions.ILike(workOrder.Number, pattern, "\\")
                || (workOrder.PartyName != null && EF.Functions.ILike(workOrder.PartyName, pattern, "\\")));
        }

        if (filter.PartyId is { } partyId)
        {
            query = query.Where(workOrder => workOrder.PartyId == partyId);
        }

        if (filter.Mine && currentUser.UserId is { } me)
        {
            query = query.Where(workOrder => workOrder.Assignees.Any(assignee => assignee.UserId == me.Value));
        }

        return query;
    }
}
