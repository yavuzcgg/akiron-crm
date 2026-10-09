using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Finance;
using Akiron.Contracts.Sales;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>Leads on their own stream and their party's; a new lead also shows on the workspace feed.</summary>
internal sealed class LeadCreatedProjection(TimelineWriter writer) : IIntegrationEventConsumer<LeadCreated>
{
    public Task HandleAsync(LeadCreated integrationEvent, CancellationToken cancellationToken)
    {
        List<TimelineSubject> subjects =
        [
            new(TimelineSubjects.Lead, integrationEvent.LeadId),
            new(TimelineSubjects.Workspace, integrationEvent.TenantId.Value),
        ];
        if (integrationEvent.PartyId is { } partyId)
        {
            subjects.Add(new TimelineSubject(TimelineSubjects.Party, partyId));
        }

        return writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.LeadCreated,
                integrationEvent.OccurredAt,
                integrationEvent.CreatedByUserId is { } userId
                    ? TimelineActor.User(userId, integrationEvent.CreatedByName)
                    : TimelineActor.ClientContact(integrationEvent.ContactName ?? integrationEvent.CompanyName),
                Payload.Of(new
                {
                    leadId = integrationEvent.LeadId,
                    title = integrationEvent.Title,
                    companyName = integrationEvent.CompanyName,
                    contactName = integrationEvent.ContactName,
                    source = integrationEvent.Source,
                }),
                integrationEvent.EventId.ToString(),
                subjects),
            cancellationToken);
    }
}

internal sealed class LeadMovedProjection(TimelineWriter writer) : IIntegrationEventConsumer<LeadMoved>
{
    public Task HandleAsync(LeadMoved integrationEvent, CancellationToken cancellationToken)
    {
        List<TimelineSubject> subjects = [new(TimelineSubjects.Lead, integrationEvent.LeadId)];
        if (integrationEvent.PartyId is { } partyId)
        {
            subjects.Add(new TimelineSubject(TimelineSubjects.Party, partyId));
        }

        return writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.LeadMoved,
                integrationEvent.OccurredAt,
                integrationEvent.MovedByUserId is { } userId ? TimelineActor.User(userId, integrationEvent.MovedByName) : TimelineActor.SystemActor,
                Payload.Of(new
                {
                    leadId = integrationEvent.LeadId,
                    title = integrationEvent.Title,
                    from = integrationEvent.FromStage,
                    to = integrationEvent.ToStage,
                    lostReason = integrationEvent.LostReason,
                }),
                integrationEvent.EventId.ToString(),
                subjects),
            cancellationToken);
    }
}

internal sealed class LeadConvertedProjection(TimelineWriter writer) : IIntegrationEventConsumer<LeadConverted>
{
    public Task HandleAsync(LeadConverted integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.LeadConverted,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.ConvertedByUserId, integrationEvent.ConvertedByName),
                Payload.Of(new
                {
                    leadId = integrationEvent.LeadId,
                    title = integrationEvent.Title,
                    partyId = integrationEvent.PartyId,
                    partyName = integrationEvent.PartyName,
                    partyCreated = integrationEvent.PartyCreated,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Lead, integrationEvent.LeadId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ]),
            cancellationToken);
}

/// <summary>Contracts on their own stream and their party's.</summary>
internal sealed class ContractStartedProjection(TimelineWriter writer) : IIntegrationEventConsumer<ContractStarted>
{
    public Task HandleAsync(ContractStarted integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ContractStarted,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.CreatedByUserId, integrationEvent.CreatedByName),
                Payload.Of(new
                {
                    contractId = integrationEvent.ContractId,
                    number = integrationEvent.Number,
                    title = integrationEvent.Title,
                    partyName = integrationEvent.PartyName,
                    amount = integrationEvent.Amount,
                    currency = integrationEvent.Currency,
                    startDate = integrationEvent.StartDate,
                    endDate = integrationEvent.EndDate,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Contract, integrationEvent.ContractId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ]),
            cancellationToken);
}

internal sealed class ContractRenewedProjection(TimelineWriter writer) : IIntegrationEventConsumer<ContractRenewed>
{
    public Task HandleAsync(ContractRenewed integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ContractRenewed,
                integrationEvent.OccurredAt,
                TimelineActor.SystemActor,
                Payload.Of(new { contractId = integrationEvent.ContractId, number = integrationEvent.Number, title = integrationEvent.Title, newEndDate = integrationEvent.NewEndDate }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Contract, integrationEvent.ContractId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ]),
            cancellationToken);
}

internal sealed class ContractEndedProjection(TimelineWriter writer) : IIntegrationEventConsumer<ContractEnded>
{
    public Task HandleAsync(ContractEnded integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ContractEnded,
                integrationEvent.OccurredAt,
                integrationEvent.EndedByUserId is { } userId ? TimelineActor.User(userId, integrationEvent.EndedByName) : TimelineActor.SystemActor,
                Payload.Of(new
                {
                    contractId = integrationEvent.ContractId,
                    number = integrationEvent.Number,
                    title = integrationEvent.Title,
                    endDate = integrationEvent.EndDate,
                    reason = integrationEvent.Reason,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Contract, integrationEvent.ContractId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ]),
            cancellationToken);
}

/// <summary>A payment reminder e-mailed to the client, on the party's stream.</summary>
internal sealed class PaymentReminderSentProjection(TimelineWriter writer) : IIntegrationEventConsumer<PaymentReminderSent>
{
    public Task HandleAsync(PaymentReminderSent integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.PaymentReminderSent,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.SentByUserId, integrationEvent.SentByName),
                Payload.Of(new
                {
                    number = integrationEvent.ChargeNumber,
                    amount = integrationEvent.Remaining,
                    currency = integrationEvent.Currency,
                    dueDate = integrationEvent.DueDate,
                    email = integrationEvent.Email,
                    partyId = integrationEvent.PartyId,
                }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId)]),
            cancellationToken);
}
