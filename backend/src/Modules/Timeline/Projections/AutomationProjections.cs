using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Automation;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>What a rule sent to a client, on the party's stream: nobody should wonder who wrote to them.</summary>
internal sealed class AutomationMessageSentProjection(TimelineWriter writer) : IIntegrationEventConsumer<AutomationMessageSent>
{
    public Task HandleAsync(AutomationMessageSent integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.AutomationMessageSent,
                integrationEvent.OccurredAt,
                TimelineActor.SystemActor,
                Payload.Of(new
                {
                    ruleName = integrationEvent.RuleName,
                    channel = integrationEvent.Channel,
                    recipient = integrationEvent.Recipient,
                    partyId = integrationEvent.PartyId,
                }),
                integrationEvent.EventId.ToString(),
                [new TimelineSubject(TimelineSubjects.Party, integrationEvent.PartyId)]),
            cancellationToken);
}
