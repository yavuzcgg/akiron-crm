using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Scheduling;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Sales;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Crm.Features.Leads;

/// <summary>
/// The board follows the paperwork: a quote sent to the party of exactly one open lead moves that lead
/// to "proposal"; when that quote is accepted, the lead is won. With several open leads for one party
/// nothing moves (a person knows which one it was). Redelivery finds the lead already there.
/// </summary>
internal sealed class QuoteSentLeadConsumer(CrmDbContext db) : IIntegrationEventConsumer<QuoteSent>
{
    public async Task HandleAsync(QuoteSent integrationEvent, CancellationToken cancellationToken)
    {
        var open = await LeadQuotes.OpenLeadsOfAsync(db, integrationEvent.PartyId, cancellationToken);
        if (open.Count != 1)
        {
            return;
        }

        var lead = open[0];
        lead.NoteQuote(integrationEvent.QuoteId);
        if (lead.Stage < LeadStage.Proposal)
        {
            await LeadQuotes.MoveAsync(db, lead, LeadStage.Proposal, integrationEvent.OccurredAt, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class QuoteDecidedLeadConsumer(CrmDbContext db) : IIntegrationEventConsumer<QuoteDecided>
{
    public async Task HandleAsync(QuoteDecided integrationEvent, CancellationToken cancellationToken)
    {
        if (!integrationEvent.Accepted)
        {
            return;
        }

        var open = await LeadQuotes.OpenLeadsOfAsync(db, integrationEvent.PartyId, cancellationToken);
        var lead = open.FirstOrDefault(candidate => candidate.QuoteId == integrationEvent.QuoteId) ?? (open.Count == 1 ? open[0] : null);
        if (lead is null)
        {
            return;
        }

        lead.NoteQuote(integrationEvent.QuoteId);
        await LeadQuotes.MoveAsync(db, lead, LeadStage.Won, integrationEvent.OccurredAt, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class LeadQuotes
{
    public static async Task<List<Lead>> OpenLeadsOfAsync(CrmDbContext db, Guid partyId, CancellationToken cancellationToken) =>
        await db.Leads.Where(lead => lead.PartyId == partyId && lead.Stage != LeadStage.Won && lead.Stage != LeadStage.Lost).ToListAsync(cancellationToken);

    public static async Task MoveAsync(CrmDbContext db, Lead lead, LeadStage stage, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var others = await db.Leads.Where(candidate => candidate.Stage == stage && candidate.Id != lead.Id).OrderBy(candidate => candidate.Rank).Take(500).ToListAsync(cancellationToken);
        if (lead.MoveTo(stage, LeadRanks.At(others, null), now) is { } from)
        {
            db.Publish(new LeadMoved(lead.TenantId, now, lead.Id.Value, lead.Title, lead.PartyId, LeadApi.Stage(from), LeadApi.Stage(stage), null, null, null));
        }
    }
}

/// <summary>Reminds a lead's owner on the day of its next step (once per date; changing the date re-arms it).</summary>
internal sealed class LeadFollowUpJob(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IScheduledJob
{
    public async Task RunAsync(DateOnly today, CancellationToken cancellationToken)
    {
        List<Guid> tenants;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            tenants = await DueLeads(db.Leads.IgnoreQueryFilters([QueryFilters.Tenant]), today)
                .Select(lead => lead.TenantId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        foreach (var tenant in tenants)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(TenantId.From(tenant));
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var now = timeProvider.GetUtcNow();
            foreach (var lead in await DueLeads(db.Leads, today).ToListAsync(cancellationToken))
            {
                if (lead.TakeFollowUpIfDue(today))
                {
                    db.Publish(new LeadFollowUpDue(lead.TenantId, now, lead.Id.Value, lead.Title, lead.NextStep, lead.NextStepDate!.Value, lead.OwnerUserId!.Value));
                }
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone edited a lead meanwhile; the next run looks again.
            }
        }
    }

    private static IQueryable<Lead> DueLeads(IQueryable<Lead> leads, DateOnly today) =>
        leads.Where(lead => lead.Stage != LeadStage.Won && lead.Stage != LeadStage.Lost
            && lead.OwnerUserId != null
            && lead.NextStepDate != null && lead.NextStepDate <= today
            && (lead.FollowUpSentFor == null || lead.FollowUpSentFor != lead.NextStepDate));
}
