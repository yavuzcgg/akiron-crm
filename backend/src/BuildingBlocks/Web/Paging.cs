using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.BuildingBlocks.Web;

/// <summary>Bound from the query string with <c>[AsParameters]</c>. Out-of-range values are rejected, not clamped.</summary>
public sealed record PageRequest(
    [FromQuery(Name = "page")] int Page = 1,
    [FromQuery(Name = "pageSize")] int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;
}

public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }
}
