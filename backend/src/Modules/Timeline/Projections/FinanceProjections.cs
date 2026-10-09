using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Finance;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>Money on the party's stream: charges, collections and their reversals.</summary>
internal sealed class ChargeRecordedProjection(TimelineWriter writer) : IIntegrationEventConsumer<ChargeRecorded>
{
    public Task HandleAsync(ChargeRecorded integrationEvent, CancellationToken cancellationToken)
    {
        List<TimelineSubject> subjects = [new(TimelineSubjects.Party, integrationEvent.PartyId)];
        if (integrationEvent.QuoteId is { } quoteId)
        {
            subjects.Add(new TimelineSubject(TimelineSubjects.Quote, quoteId));
        }

        if (integrationEvent.ContractId is { } contractId)
        {
            subjects.Add(new TimelineSubject(TimelineSubjects.Contract, contractId));
        }

        return writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ChargeRecorded,
                integrationEvent.OccurredAt,
                integrationEvent.RecordedByUserId is { } userId ? TimelineActor.User(userId, integrationEvent.RecordedByName) : TimelineActor.SystemActor,
                Payload.Of(new
                {
                    chargeId = integrationEvent.ChargeId,
                    number = integrationEvent.Number,
                    description = integrationEvent.Description,
                    amount = integrationEvent.Amount,
                    currency = integrationEvent.Currency,
                    instalments = integrationEvent.Instalments,
                    partyId = integrationEvent.PartyId,
                    contractId = integrationEvent.ContractId,
                }),
                integrationEvent.EventId.ToString(),
                subjects),
            cancellationToken);
    }
}

internal sealed class ChargeCancelledProjection(TimelineWriter writer) : IIntegrationEventConsumer<ChargeCancelled>
{
    public Task HandleAsync(ChargeCancelled integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ChargeCancelled,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.CancelledByUserId, integrationEvent.CancelledByName),
                Payload.Of(new { number = integrationEvent.Number, amount = integrationEvent.Amount, currency = integrationEvent.Currency, reason = integrationEvent.Reason, partyId = integrationEvent.PartyId }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId)]),
            cancellationToken);
}

internal sealed class ReceiptRecordedProjection(TimelineWriter writer) : IIntegrationEventConsumer<ReceiptRecorded>
{
    public Task HandleAsync(ReceiptRecorded integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ReceiptRecorded,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.RecordedByUserId, integrationEvent.RecordedByName),
                Payload.Of(new
                {
                    receiptId = integrationEvent.ReceiptId,
                    number = integrationEvent.Number,
                    amount = integrationEvent.Amount,
                    unallocated = integrationEvent.Unallocated,
                    currency = integrationEvent.Currency,
                    method = integrationEvent.Method,
                    partyName = integrationEvent.PartyName,
                    partyId = integrationEvent.PartyId,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                    new TimelineSubject(TimelineSubjects.Workspace, integrationEvent.TenantId.Value),
                ]),
            cancellationToken);
}

internal sealed class ReceiptReversedProjection(TimelineWriter writer) : IIntegrationEventConsumer<ReceiptReversed>
{
    public Task HandleAsync(ReceiptReversed integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.ReceiptReversed,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.ReversedByUserId, integrationEvent.ReversedByName),
                Payload.Of(new { number = integrationEvent.Number, amount = integrationEvent.Amount, currency = integrationEvent.Currency, reason = integrationEvent.Reason, partyId = integrationEvent.PartyId }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId)]),
            cancellationToken);
}
