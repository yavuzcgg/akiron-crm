using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Sales;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>Quote lines on the quote's own stream and its client's; an answer also reaches the workspace.</summary>
internal sealed class QuoteSentProjection(TimelineWriter writer) : IIntegrationEventConsumer<QuoteSent>
{
    public Task HandleAsync(QuoteSent integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.QuoteSent,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.SentByUserId, integrationEvent.SentByName),
                Payload.Of(new
                {
                    quoteId = integrationEvent.QuoteId,
                    number = integrationEvent.Number,
                    revision = integrationEvent.Revision,
                    title = integrationEvent.Title,
                    partyName = integrationEvent.PartyName,
                    total = integrationEvent.GrandTotal,
                    currency = integrationEvent.Currency,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Quote, integrationEvent.QuoteId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ]),
            cancellationToken);
}

internal sealed class QuoteViewedProjection(TimelineWriter writer) : IIntegrationEventConsumer<QuoteViewed>
{
    public Task HandleAsync(QuoteViewed integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.QuoteViewed,
                integrationEvent.OccurredAt,
                TimelineActor.SystemActor,
                Payload.Of(new { quoteId = integrationEvent.QuoteId, number = integrationEvent.Number, title = integrationEvent.Title, partyName = integrationEvent.PartyName }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Quote, integrationEvent.QuoteId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                ]),
            cancellationToken);
}

internal sealed class QuoteDecidedProjection(TimelineWriter writer) : IIntegrationEventConsumer<QuoteDecided>
{
    public Task HandleAsync(QuoteDecided integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.QuoteDecided,
                integrationEvent.OccurredAt,
                TimelineActor.ClientContact(integrationEvent.DecidedByName),
                Payload.Of(new
                {
                    quoteId = integrationEvent.QuoteId,
                    number = integrationEvent.Number,
                    title = integrationEvent.Title,
                    partyName = integrationEvent.PartyName,
                    accepted = integrationEvent.Accepted,
                    decidedByName = integrationEvent.DecidedByName,
                    note = integrationEvent.Note,
                    total = integrationEvent.GrandTotal,
                    currency = integrationEvent.Currency,
                }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Quote, integrationEvent.QuoteId),
                    new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId),
                    new TimelineSubject(TimelineSubjects.Workspace, integrationEvent.TenantId.Value),
                ]),
            cancellationToken);
}
