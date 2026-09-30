using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features;

/// <summary>
/// The tenant's stages, created on first use: tenants that registered before this module existed
/// get the same four built-in stages as new ones, without a data migration per tenant.
/// </summary>
internal sealed class StageBoard(JobsDbContext db, ITenantContext tenantContext)
{
    /// <summary>Gap between neighbouring cards; leaves room for many drops before a stage is renumbered.</summary>
    public const double RankStep = 1024;

    public async Task<List<Stage>> StagesAsync(CancellationToken cancellationToken)
    {
        var stages = await db.Stages.OrderBy(stage => stage.Position).ToListAsync(cancellationToken);
        if (stages.Count > 0)
        {
            return stages;
        }

        // Two first requests at once must not both seed: a transaction-scoped advisory lock per
        // tenant serialises them, and the second one finds the stages the first wrote.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({"jobs.stages:" + tenantContext.TenantId.Value}))",
            cancellationToken);

        stages = await db.Stages.OrderBy(stage => stage.Position).ToListAsync(cancellationToken);
        if (stages.Count == 0)
        {
            stages = [.. Stage.Defaults()];
            db.Stages.AddRange(stages);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return stages;
    }

    /// <summary>
    /// The rank that puts a card at <paramref name="index"/> among the other cards of the stage,
    /// renumbering the stage when two neighbours have run out of room between them.
    /// </summary>
    public async Task<double> RankAtAsync(StageId stageId, WorkOrderId? moving, int? index, CancellationToken cancellationToken)
    {
        var others = await db.WorkOrders
            .Where(workOrder => workOrder.StageId == stageId && (moving == null || workOrder.Id != moving))
            .OrderBy(workOrder => workOrder.Rank)
            .ToListAsync(cancellationToken);

        var position = Math.Clamp(index ?? others.Count, 0, others.Count);
        if (others.Count == 0)
        {
            return RankStep;
        }

        if (position == others.Count)
        {
            return others[^1].Rank + RankStep;
        }

        if (position == 0)
        {
            return others[0].Rank - RankStep;
        }

        var before = others[position - 1].Rank;
        var after = others[position].Rank;
        if (after - before > 1e-6)
        {
            return (before + after) / 2;
        }

        // Out of room: space the stage out again, leaving a slot at the target position.
        for (var i = 0; i < others.Count; i++)
        {
            others[i].Rerank((i < position ? i + 1 : i + 2) * RankStep);
        }

        return (position + 1) * RankStep;
    }
}
