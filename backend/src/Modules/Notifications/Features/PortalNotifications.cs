using System.Text.Json;
using Akiron.BuildingBlocks.Events;
using Akiron.Contracts;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Portal;
using Akiron.Modules.Notifications.Domain;

namespace Akiron.Modules.Notifications.Features;

/// <summary>
/// Tells the team when a client writes or sends a file through the portal: the people on the record
/// (a work order's assignees and creator), or, for something sent to the party as a whole (a payment
/// receipt), whoever records collections.
/// </summary>
internal sealed class ClientActivityNotification(NotificationSender sender, IMemberDirectory members)
    : IIntegrationEventConsumer<ClientMessagePosted>, IIntegrationEventConsumer<ClientFileUploaded>
{
    public const string MessageType = "portal.message.posted";
    public const string FileType = "portal.file.uploaded";

    public async Task HandleAsync(ClientMessagePosted integrationEvent, CancellationToken cancellationToken)
    {
        if (!integrationEvent.FromClient)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                subjectType = integrationEvent.SubjectType,
                subjectId = integrationEvent.SubjectId,
                title = integrationEvent.Title,
                clientName = integrationEvent.AuthorName,
                excerpt = integrationEvent.Excerpt,
            },
            JsonSerializerOptions.Web);
        foreach (var userId in await RecipientsAsync(integrationEvent.StaffUserIds, integrationEvent.SubjectType, cancellationToken))
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, userId, MessageType, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }

    public async Task HandleAsync(ClientFileUploaded integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                subjectType = integrationEvent.SubjectType,
                subjectId = integrationEvent.SubjectId,
                title = integrationEvent.Title,
                clientName = integrationEvent.ClientName,
                fileName = integrationEvent.FileName,
            },
            JsonSerializerOptions.Web);
        foreach (var userId in await RecipientsAsync(integrationEvent.StaffUserIds, integrationEvent.SubjectType, cancellationToken))
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, userId, FileType, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }

    private async Task<IReadOnlyCollection<Guid>> RecipientsAsync(IReadOnlyList<Guid> staff, string subjectType, CancellationToken cancellationToken)
    {
        // People may have left since they were put on the record; only current members are told.
        var current = (await members.FindAsync(staff, cancellationToken)).Keys.ToList();
        if (current.Count > 0)
        {
            return current;
        }

        var permission = subjectType == Subjects.Party ? "finance.write" : "jobs.work_orders.write";
        return (await members.WithPermissionAsync(permission, cancellationToken)).Select(member => member.UserId).ToList();
    }
}
