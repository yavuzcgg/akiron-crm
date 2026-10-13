using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Scheduling;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Jobs;
using Akiron.Contracts.Portal;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Features.Tickets;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Jobs.Features.ClientPortal;

/// <param name="Urgent">The client's only say on priority: it cannot wait.</param>
internal sealed record ClientTicketCommand(string Subject, string? Description = null, bool Urgent = false);

internal sealed class ClientTicketValidator : AbstractValidator<ClientTicketCommand>
{
    public ClientTicketValidator()
    {
        RuleFor(command => command.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength);
        RuleFor(command => command.Description).MaximumLength(Ticket.DescriptionMaxLength);
    }
}

/// <summary>A ticket as its client sees it: what they asked, where it stands, who opened it. No assignee, no due time.</summary>
internal sealed record ClientTicket(
    Guid Id,
    string Number,
    string Subject,
    string? Description,
    string Status,
    bool Urgent,
    string RequesterName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

/// <summary>The signed-in client's support tickets: ask, follow, close.</summary>
internal sealed class ClientTicketsHandler(
    JobsDbContext db,
    IPortalVisitor visitor,
    IPartyDirectory parties,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ClientTicket>> ListAsync(CancellationToken cancellationToken)
    {
        var partyId = visitor.PartyId;
        var tickets = await db.Tickets.AsNoTracking()
            .Where(ticket => ticket.PartyId == partyId)
            .OrderByDescending(ticket => ticket.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return tickets.OrderBy(ticket => !ticket.IsOpen).Select(ToClient).ToList();
    }

    public async Task<Result<ClientTicket>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await FindAsync(id, cancellationToken);
        return ticket is null ? TicketErrors.NotFound : ToClient(ticket);
    }

    public async Task<Result<ClientTicket>> OpenAsync(ClientTicketCommand command, CancellationToken cancellationToken)
    {
        var party = await parties.FindAsync(visitor.PartyId, cancellationToken);
        if (party is null)
        {
            return JobsErrors.PartyNotFound;
        }

        var now = timeProvider.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var ticket = await TicketApi.OpenAsync(
            db, party.PartyId, party.Name, command.Subject, command.Description, command.Urgent ? WorkOrderPriority.Urgent : WorkOrderPriority.Normal,
            fromClient: true, visitor.Name, now, cancellationToken);
        db.Publish(new TicketOpened(
            tenantContext.TenantId, now, ticket.Id.Value, ticket.Number, ticket.Subject, ticket.PartyId, ticket.PartyName,
            JobsApi.Priority(ticket.Priority), true, ticket.RequesterName, null, null));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToClient(ticket);
    }

    /// <summary>The client says it is done (or no longer needed).</summary>
    public async Task<Result<ClientTicket>> CloseAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await FindAsync(id, cancellationToken);
        if (ticket is null)
        {
            return TicketErrors.NotFound;
        }

        var now = timeProvider.GetUtcNow();
        if (ticket.MoveTo(TicketStatus.Closed, now) is { } from)
        {
            db.Publish(new TicketStatusChanged(
                ticket.TenantId, now, ticket.Id.Value, ticket.Number, ticket.Subject, ticket.PartyId, TicketApi.Status(from), TicketApi.Status(TicketStatus.Closed),
                ByClient: true, visitor.Name, null, ticket.AssigneeUserId));
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToClient(ticket);
    }

    private static ClientTicket ToClient(Ticket ticket) => new(
        ticket.Id.Value, ticket.Number, ticket.Subject, ticket.Description, TicketApi.Status(ticket.Status),
        ticket.Priority == WorkOrderPriority.Urgent, ticket.RequesterName, ticket.CreatedAt, ticket.ResolvedAt);

    private Task<Ticket?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticketId = TicketId.From(id);
        var partyId = visitor.PartyId;
        return db.Tickets.FirstOrDefaultAsync(ticket => ticket.Id == ticketId && ticket.PartyId == partyId, cancellationToken);
    }
}

/// <summary>Tells Timeline and Files whose a ticket is and who on the team hears from its client.</summary>
internal sealed class TicketPortalSubject(JobsDbContext db) : IPortalSubject
{
    public string SubjectType => Subjects.Ticket;

    public async Task<PortalSubjectInfo?> FindAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var id = TicketId.From(subjectId);
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return ticket is null
            ? null
            : new PortalSubjectInfo(ticket.PartyId, $"{ticket.Number} {ticket.Subject}", ticket.AssigneeUserId is { } assignee ? [assignee] : []);
    }
}

/// <summary>
/// The conversation moves the ticket: the team's first message to the client is the first response
/// (and an open ticket is then being worked on); a word from the client puts a waiting or resolved
/// ticket back on the team's desk. Redelivery finds the ticket already there.
/// </summary>
internal sealed class TicketThreadConsumer(JobsDbContext db) : IIntegrationEventConsumer<ClientMessagePosted>
{
    public async Task HandleAsync(ClientMessagePosted integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.SubjectType != Subjects.Ticket)
        {
            return;
        }

        var id = TicketId.From(integrationEvent.SubjectId);
        var ticket = await db.Tickets.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (ticket is null)
        {
            return;
        }

        var now = integrationEvent.OccurredAt;
        var from = integrationEvent.FromClient ? ticket.ClientWrote(now) : ticket.StaffAnswered(now);
        if (from is { } left)
        {
            db.Publish(new TicketStatusChanged(
                ticket.TenantId, now, ticket.Id.Value, ticket.Number, ticket.Subject, ticket.PartyId, TicketApi.Status(left), TicketApi.Status(ticket.Status),
                integrationEvent.FromClient, integrationEvent.AuthorName, integrationEvent.AuthorUserId, ticket.AssigneeUserId));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Reminds the team once when a ticket's first answer is late.</summary>
internal sealed class TicketResponseJob(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IScheduledJob
{
    public async Task RunAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        List<Guid> tenants;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            tenants = await Late(db.Tickets.IgnoreQueryFilters([QueryFilters.Tenant]), now)
                .Select(ticket => ticket.TenantId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        foreach (var tenant in tenants)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(TenantId.From(tenant));
            var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            foreach (var ticket in await Late(db.Tickets, now).ToListAsync(cancellationToken))
            {
                if (ticket.TakeOverdueIfDue(now))
                {
                    db.Publish(new TicketResponseOverdue(ticket.TenantId, now, ticket.Id.Value, ticket.Number, ticket.Subject, ticket.PartyName, ticket.AssigneeUserId));
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static IQueryable<Ticket> Late(IQueryable<Ticket> tickets, DateTimeOffset now) =>
        tickets.Where(ticket => ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed
            && ticket.FirstResponseAt == null && !ticket.OverdueNotified && ticket.ResponseDueAt <= now);
}
