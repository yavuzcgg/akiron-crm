using Microsoft.EntityFrameworkCore;

namespace Akiron.BuildingBlocks.Persistence;

/// <summary>Names of the global query filters every module context applies (ADR-0002).</summary>
public static class QueryFilters
{
    public const string Tenant = "Tenant";
    public const string SoftDelete = "SoftDelete";

    /// <summary>
    /// Includes soft-deleted (archived) rows while keeping the tenant filter, e.g. to label time
    /// logged on a work order that was archived since. Never use <c>IgnoreQueryFilters()</c> for this.
    /// </summary>
    public static IQueryable<TEntity> IncludingArchived<TEntity>(this IQueryable<TEntity> query)
        where TEntity : class =>
        query.IgnoreQueryFilters([SoftDelete]);
}
