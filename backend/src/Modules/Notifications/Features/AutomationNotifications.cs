using System.Text.Json;
using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Automation;
using Akiron.Contracts.Identity;
using Akiron.Modules.Notifications.Domain;

namespace Akiron.Modules.Notifications.Features;

/// <summary>Delivers what a rule asked the team to be told: to one person, or to everyone.</summary>
internal sealed class AutomationNoticeNotification(NotificationSender sender, IMemberDirectory members) : IIntegrationEventConsumer<AutomationNotice>
{
    public const string Type = "automation.notice";

    public async Task HandleAsync(AutomationNotice integrationEvent, CancellationToken cancellationToken)
    {
        var recipients = integrationEvent.UserId is { } userId
            ? (await members.FindAsync([userId], cancellationToken)).Keys.ToList()
            : (await members.ListAsync(cancellationToken)).Select(member => member.UserId).ToList();

        var payload = JsonSerializer.Serialize(
            new
            {
                ruleName = integrationEvent.RuleName,
                text = integrationEvent.Text,
                subjectType = integrationEvent.SubjectType,
                subjectId = integrationEvent.SubjectId,
            },
            JsonSerializerOptions.Web);
        foreach (var recipient in recipients)
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, recipient, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }
}
