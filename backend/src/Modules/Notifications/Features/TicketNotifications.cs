using System.Text.Json;
using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Jobs;
using Akiron.Modules.Notifications.Domain;

namespace Akiron.Modules.Notifications.Features;

/// <summary>
/// Support tickets reach the person they are assigned to; a ticket nobody owns yet reaches everyone
/// who handles tickets, so a client's request never waits for someone to notice the queue.
/// </summary>
internal sealed class TicketNotifications(NotificationSender sender, IMemberDirectory members)
    : IIntegrationEventConsumer<TicketOpened>,
        IIntegrationEventConsumer<TicketAssigned>,
        IIntegrationEventConsumer<TicketResponseOverdue>,
        IIntegrationEventConsumer<TicketStatusChanged>
{
    public const string Opened = "jobs.ticket.opened";
    public const string Assigned = "jobs.ticket.assigned";
    public const string Overdue = "jobs.ticket.response_overdue";
    public const string ClosedByClient = "jobs.ticket.closed_by_client";

    public async Task HandleAsync(TicketOpened integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                ticketId = integrationEvent.TicketId,
                number = integrationEvent.Number,
                subject = integrationEvent.Subject,
                partyName = integrationEvent.PartyName,
                priority = integrationEvent.Priority,
                fromClient = integrationEvent.FromClient,
                requesterName = integrationEvent.RequesterName,
            },
            JsonSerializerOptions.Web);

        // Staff who opened a ticket for themselves (or for nobody) need no notice of it.
        IEnumerable<Guid> recipients = integrationEvent.FromClient
            ? await AssigneeOrAllAsync(integrationEvent.AssigneeUserId, cancellationToken)
            : integrationEvent.AssigneeUserId is { } assignee ? [assignee] : [];
        foreach (var userId in recipients.Where(userId => userId != integrationEvent.OpenedByUserId))
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, userId, Opened, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }

    public Task HandleAsync(TicketAssigned integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                ticketId = integrationEvent.TicketId,
                number = integrationEvent.Number,
                subject = integrationEvent.Subject,
                partyName = integrationEvent.PartyName,
                assignedByName = integrationEvent.AssignedByName,
            },
            JsonSerializerOptions.Web);
        return sender.SendAsync(
            Notification.Create(integrationEvent.TenantId, integrationEvent.AssigneeUserId, Assigned, payload, integrationEvent.EventId, integrationEvent.OccurredAt),
            cancellationToken);
    }

    public async Task HandleAsync(TicketResponseOverdue integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new { ticketId = integrationEvent.TicketId, number = integrationEvent.Number, subject = integrationEvent.Subject, partyName = integrationEvent.PartyName },
            JsonSerializerOptions.Web);
        foreach (var userId in await AssigneeOrAllAsync(integrationEvent.AssigneeUserId, cancellationToken))
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, userId, Overdue, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }

    public async Task HandleAsync(TicketStatusChanged integrationEvent, CancellationToken cancellationToken)
    {
        // A client's message already notifies (and reopens); only their closing a ticket is news of its own.
        if (!integrationEvent.ByClient || integrationEvent.ToStatus != "closed")
        {
            return;
        }

        var payload = JsonSerializer.Serialize(
            new { ticketId = integrationEvent.TicketId, number = integrationEvent.Number, subject = integrationEvent.Subject, clientName = integrationEvent.ChangedByName },
            JsonSerializerOptions.Web);
        foreach (var userId in await AssigneeOrAllAsync(integrationEvent.AssigneeUserId, cancellationToken))
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, userId, ClosedByClient, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }

    private async Task<IReadOnlyCollection<Guid>> AssigneeOrAllAsync(Guid? assignee, CancellationToken cancellationToken)
    {
        if (assignee is { } assigneeId && (await members.FindAsync([assigneeId], cancellationToken)).ContainsKey(assigneeId))
        {
            return [assigneeId];
        }

        return (await members.WithPermissionAsync("jobs.tickets.write", cancellationToken)).Select(member => member.UserId).ToList();
    }
}
