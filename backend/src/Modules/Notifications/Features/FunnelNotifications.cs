using System.Text.Json;
using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Finance;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Sales;
using Akiron.Modules.Notifications.Domain;

namespace Akiron.Modules.Notifications.Features;

/// <summary>
/// A lead from the web form goes to the form's owner (or everyone who runs the funnel); a lead someone
/// added for a colleague goes to that colleague.
/// </summary>
internal sealed class LeadCreatedNotification(NotificationSender sender, IMemberDirectory members) : IIntegrationEventConsumer<LeadCreated>
{
    public const string Type = "crm.lead.created";

    public async Task HandleAsync(LeadCreated integrationEvent, CancellationToken cancellationToken)
    {
        var fromWeb = integrationEvent.CreatedByUserId is null;
        IEnumerable<Guid> recipients = integrationEvent.OwnerUserId is { } owner
            ? [owner]
            : fromWeb ? (await members.WithPermissionAsync("crm.leads.write", cancellationToken)).Select(member => member.UserId) : [];

        var payload = JsonSerializer.Serialize(
            new
            {
                leadId = integrationEvent.LeadId,
                title = integrationEvent.Title,
                source = integrationEvent.Source,
                webForm = fromWeb,
                createdByName = integrationEvent.CreatedByName,
            },
            JsonSerializerOptions.Web);

        foreach (var userId in recipients.Where(userId => userId != integrationEvent.CreatedByUserId))
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, userId, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }
}

internal sealed class LeadFollowUpNotification(NotificationSender sender) : IIntegrationEventConsumer<LeadFollowUpDue>
{
    public const string Type = "crm.lead.follow_up_due";

    public Task HandleAsync(LeadFollowUpDue integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new { leadId = integrationEvent.LeadId, title = integrationEvent.Title, nextStep = integrationEvent.NextStep, date = integrationEvent.Date },
            JsonSerializerOptions.Web);
        return sender.SendAsync(
            Notification.Create(integrationEvent.TenantId, integrationEvent.OwnerUserId, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt),
            cancellationToken);
    }
}

/// <summary>Tells the contract's owner (or whoever manages contracts) that its end date is near.</summary>
internal sealed class ContractEndingNotification(NotificationSender sender, IMemberDirectory members) : IIntegrationEventConsumer<ContractEnding>
{
    public const string Type = "sales.contract.ending";

    public async Task HandleAsync(ContractEnding integrationEvent, CancellationToken cancellationToken)
    {
        var owner = integrationEvent.OwnerUserId is { } ownerId ? (await members.FindAsync([ownerId], cancellationToken)).GetValueOrDefault(ownerId) : null;
        var recipients = owner is not null
            ? [owner.UserId]
            : (await members.WithPermissionAsync("sales.contracts.write", cancellationToken)).Select(member => member.UserId).ToList();

        var payload = JsonSerializer.Serialize(
            new
            {
                contractId = integrationEvent.ContractId,
                number = integrationEvent.Number,
                title = integrationEvent.Title,
                partyName = integrationEvent.PartyName,
                endDate = integrationEvent.EndDate,
                autoRenew = integrationEvent.AutoRenew,
            },
            JsonSerializerOptions.Web);
        foreach (var userId in recipients)
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, userId, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }
}

/// <summary>The daily collections digest, for everyone who records collections.</summary>
internal sealed class PaymentsDigestNotification(NotificationSender sender, IMemberDirectory members) : IIntegrationEventConsumer<PaymentsDueDigest>
{
    public const string Type = "finance.payments.digest";

    public async Task HandleAsync(PaymentsDueDigest integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                day = integrationEvent.Day,
                dueToday = integrationEvent.DueTodayCount,
                overdue = integrationEvent.OverdueCount,
                upcoming = integrationEvent.UpcomingCount,
                overdueAmounts = integrationEvent.Overdue,
                dueSoonAmounts = integrationEvent.DueSoon,
            },
            JsonSerializerOptions.Web);
        foreach (var member in await members.WithPermissionAsync("finance.write", cancellationToken))
        {
            await sender.SendAsync(Notification.Create(integrationEvent.TenantId, member.UserId, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt), cancellationToken);
        }
    }
}
