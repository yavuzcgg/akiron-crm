using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Jobs;
using Akiron.Modules.Jobs.Domain;
using Akiron.Modules.Jobs.Features.CreateWorkOrder;
using Akiron.Modules.Jobs.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Jobs.Features.Tickets;

internal sealed record TicketCommand(
    Guid PartyId,
    string Subject,
    string? Description = null,
    string? Priority = null,
    Guid? AssigneeUserId = null,
    string? RequesterName = null);

internal sealed class TicketValidator : AbstractValidator<TicketCommand>
{
    public TicketValidator()
    {
        RuleFor(command => command.PartyId).NotEmpty();
        RuleFor(command => command.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength);
        RuleFor(command => command.Description).MaximumLength(Ticket.DescriptionMaxLength);
        RuleFor(command => command.Priority).Must(priority => priority is null || JobsApi.Priorities.Contains(priority)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.RequesterName).MaximumLength(200);
    }
}

internal sealed record TicketStatusCommand(string Status);

internal sealed class TicketStatusValidator : AbstractValidator<TicketStatusCommand>
{
    public TicketStatusValidator() =>
        RuleFor(command => command.Status).Must(status => TicketApi.Statuses.Contains(status)).WithErrorCode("validation.invalid_value");
}

internal sealed record TicketFilter(
    [FromQuery(Name = "view")] string? View = null,
    [FromQuery(Name = "partyId")] Guid? PartyId = null,
    [FromQuery(Name = "assigneeId")] Guid? AssigneeId = null,
    [FromQuery(Name = "search")] string? Search = null);

internal sealed class TicketFilterValidator : AbstractValidator<TicketFilter>
{
    public TicketFilterValidator()
    {
        RuleFor(filter => filter.View).Must(view => view is null or "open" or "closed").WithErrorCode("validation.invalid_value");
        RuleFor(filter => filter.Search).MaximumLength(100);
    }
}

/// <param name="ResponseOverdue">Nobody has answered yet and the time its priority allows has passed.</param>
internal sealed record TicketResponse(
    Guid Id,
    string Number,
    Guid PartyId,
    string PartyName,
    string Subject,
    string? Description,
    string Status,
    string Priority,
    bool FromClient,
    string RequesterName,
    Guid? AssigneeUserId,
    string? AssigneeName,
    DateTimeOffset ResponseDueAt,
    DateTimeOffset? FirstResponseAt,
    bool ResponseOverdue,
    DateTimeOffset? ResolvedAt,
    Guid? WorkOrderId,
    DateTimeOffset CreatedAt);

internal static class TicketApi
{
    public const string NumberSeries = "DST";

    public static readonly string[] Statuses = ["open", "in_progress", "waiting_client", "resolved", "closed"];

    public static string Status(TicketStatus status) => Statuses[(int)status];

    public static TicketStatus ParseStatus(string value) => (TicketStatus)Array.IndexOf(Statuses, value);

    public static TicketResponse ToResponse(Ticket ticket, DateTimeOffset now) => new(
        ticket.Id.Value,
        ticket.Number,
        ticket.PartyId,
        ticket.PartyName,
        ticket.Subject,
        ticket.Description,
        Status(ticket.Status),
        JobsApi.Priority(ticket.Priority),
        ticket.FromClient,
        ticket.RequesterName,
        ticket.AssigneeUserId,
        ticket.AssigneeName,
        ticket.ResponseDueAt,
        ticket.FirstResponseAt,
        ticket.IsOpen && ticket.FirstResponseAt is null && ticket.ResponseDueAt <= now,
        ticket.ResolvedAt,
        ticket.WorkOrderId?.Value,
        ticket.CreatedAt);

    /// <summary>Opens a ticket with the next DST number; call inside a transaction (the counter row stays locked).</summary>
    public static async Task<Ticket> OpenAsync(
        JobsDbContext db, Guid partyId, string partyName, string subject, string? description, WorkOrderPriority priority,
        bool fromClient, string requesterName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sequence = await db.NextDocumentSequenceAsync(NumberSeries, now.Year, cancellationToken);
        var ticket = Ticket.Open(DocumentNumber.Format(NumberSeries, now.Year, sequence), partyId, partyName, subject, description, priority, fromClient, requesterName, now);
        db.Tickets.Add(ticket);
        return ticket;
    }
}

internal static class TicketErrors
{
    public static readonly Error NotFound = Error.NotFound("jobs.ticket.not_found", "No such support ticket in this organisation.");

    public static readonly Error AlreadyConverted = Error.Rule("jobs.ticket.already_converted", "A work order was already opened from this ticket.");
}

/// <summary>Support tickets on the staff side: the queue, opening one on a client's word, assigning, moving, turning one into a work order.</summary>
internal sealed class TicketsHandler(
    JobsDbContext db,
    IPartyDirectory parties,
    IMemberDirectory members,
    CreateWorkOrderHandler createWorkOrder,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private Guid UserId => (currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.")).Value;

    public async Task<IReadOnlyList<TicketResponse>> ListAsync(TicketFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Tickets.AsNoTracking();
        query = filter.View switch
        {
            "closed" => query.Where(ticket => ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed),
            "open" => query.Where(ticket => ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed),
            _ => query,
        };
        if (filter.PartyId is { } partyId)
        {
            query = query.Where(ticket => ticket.PartyId == partyId);
        }

        if (filter.AssigneeId is { } assigneeId)
        {
            query = query.Where(ticket => ticket.AssigneeUserId == assigneeId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = "%" + filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(ticket => EF.Functions.ILike(ticket.Subject, pattern, "\\")
                || EF.Functions.ILike(ticket.Number, pattern, "\\")
                || EF.Functions.ILike(ticket.PartyName, pattern, "\\"));
        }

        // Most urgent first, then the ones waiting longest.
        var tickets = filter.View == "closed"
            ? await query.OrderByDescending(ticket => ticket.ResolvedAt).Take(200).ToListAsync(cancellationToken)
            : await query.OrderByDescending(ticket => ticket.Priority).ThenBy(ticket => ticket.CreatedAt).Take(500).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        return tickets.Select(ticket => TicketApi.ToResponse(ticket, now)).ToList();
    }

    public async Task<Result<TicketResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await FindAsync(id, cancellationToken);
        return ticket is null ? TicketErrors.NotFound : TicketApi.ToResponse(ticket, timeProvider.GetUtcNow());
    }

    public async Task<Result<TicketResponse>> OpenAsync(TicketCommand command, CancellationToken cancellationToken)
    {
        var party = await parties.FindAsync(command.PartyId, cancellationToken);
        if (party is null)
        {
            return JobsErrors.PartyNotFound;
        }

        var assignee = await AssigneeAsync(command.AssigneeUserId, cancellationToken);
        if (!assignee.IsSuccess)
        {
            return assignee.Error;
        }

        var now = timeProvider.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var ticket = await TicketApi.OpenAsync(
            db, party.PartyId, party.Name, command.Subject, command.Description, JobsApi.ParsePriority(command.Priority),
            fromClient: false, string.IsNullOrWhiteSpace(command.RequesterName) ? party.Name : command.RequesterName, now, cancellationToken);
        ticket.Assign(command.AssigneeUserId, assignee.Value);
        db.Publish(new TicketOpened(
            tenantContext.TenantId, now, ticket.Id.Value, ticket.Number, ticket.Subject, ticket.PartyId, ticket.PartyName,
            JobsApi.Priority(ticket.Priority), false, ticket.RequesterName, ticket.AssigneeUserId, UserId, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TicketApi.ToResponse(ticket, now);
    }

    public async Task<Result<TicketResponse>> UpdateAsync(Guid id, TicketCommand command, CancellationToken cancellationToken)
    {
        var ticket = await FindAsync(id, cancellationToken);
        if (ticket is null)
        {
            return TicketErrors.NotFound;
        }

        var assignee = await AssigneeAsync(command.AssigneeUserId, cancellationToken);
        if (!assignee.IsSuccess)
        {
            return assignee.Error;
        }

        var now = timeProvider.GetUtcNow();
        ticket.Describe(command.Subject, command.Description, JobsApi.ParsePriority(command.Priority), ticket.CreatedAt);
        if (ticket.Assign(command.AssigneeUserId, assignee.Value) && command.AssigneeUserId is { } newAssignee && newAssignee != UserId)
        {
            db.Publish(new TicketAssigned(ticket.TenantId, now, ticket.Id.Value, ticket.Number, ticket.Subject, ticket.PartyName, newAssignee, UserId, currentUser.DisplayName));
        }

        await db.SaveChangesAsync(cancellationToken);
        return TicketApi.ToResponse(ticket, now);
    }

    public async Task<Result<TicketResponse>> MoveAsync(Guid id, TicketStatusCommand command, CancellationToken cancellationToken)
    {
        var ticket = await FindAsync(id, cancellationToken);
        if (ticket is null)
        {
            return TicketErrors.NotFound;
        }

        var now = timeProvider.GetUtcNow();
        var to = TicketApi.ParseStatus(command.Status);
        if (ticket.MoveTo(to, now) is { } from)
        {
            db.Publish(new TicketStatusChanged(
                ticket.TenantId, now, ticket.Id.Value, ticket.Number, ticket.Subject, ticket.PartyId, TicketApi.Status(from), TicketApi.Status(to),
                ByClient: false, currentUser.DisplayName, UserId, ticket.AssigneeUserId));
            await db.SaveChangesAsync(cancellationToken);
        }

        return TicketApi.ToResponse(ticket, now);
    }

    /// <summary>The request turned out to be work: opens a work order for the client with the ticket's subject and text.</summary>
    public async Task<Result<TicketResponse>> ConvertAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await FindAsync(id, cancellationToken);
        if (ticket is null)
        {
            return TicketErrors.NotFound;
        }

        if (ticket.WorkOrderId is not null)
        {
            return TicketErrors.AlreadyConverted;
        }

        var created = await createWorkOrder.HandleAsync(
            new CreateWorkOrderCommand(
                ticket.Subject,
                ticket.Description,
                ticket.PartyId,
                Priority: JobsApi.Priority(ticket.Priority),
                AssigneeIds: ticket.AssigneeUserId is { } assignee ? [assignee] : null),
            cancellationToken);
        if (!created.IsSuccess)
        {
            return created.Error;
        }

        var now = timeProvider.GetUtcNow();
        ticket.LinkWorkOrder(WorkOrderId.From(created.Value.Id));
        var from = ticket.MoveTo(TicketStatus.InProgress, now);
        db.Publish(new TicketConverted(ticket.TenantId, now, ticket.Id.Value, ticket.Number, ticket.PartyId, created.Value.Id, created.Value.Number, UserId, currentUser.DisplayName));
        if (from is { } left)
        {
            db.Publish(new TicketStatusChanged(
                ticket.TenantId, now, ticket.Id.Value, ticket.Number, ticket.Subject, ticket.PartyId, TicketApi.Status(left), TicketApi.Status(TicketStatus.InProgress),
                ByClient: false, currentUser.DisplayName, UserId, ticket.AssigneeUserId));
        }

        await db.SaveChangesAsync(cancellationToken);
        return TicketApi.ToResponse(ticket, now);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await FindAsync(id, cancellationToken);
        if (ticket is null)
        {
            return TicketErrors.NotFound;
        }

        db.Tickets.Remove(ticket);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Result<string?>> AssigneeAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id)
        {
            return (string?)null;
        }

        var found = await members.FindAsync([id], cancellationToken);
        return found.TryGetValue(id, out var member) ? member.FullName : JobsErrors.AssigneeNotMember;
    }

    private Task<Ticket?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticketId = TicketId.From(id);
        return db.Tickets.FirstOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
    }
}
