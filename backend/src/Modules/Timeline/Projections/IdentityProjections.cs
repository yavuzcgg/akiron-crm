using System.Text.Json;
using Akiron.BuildingBlocks.Events;
using Akiron.Contracts.Identity;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;

namespace Akiron.Modules.Timeline.Projections;

/// <summary>Turns Identity's events into timeline lines (ADR-0009). One consumer per event type.</summary>
internal sealed class WorkspaceCreatedProjection(TimelineWriter writer) : IIntegrationEventConsumer<WorkspaceCreated>
{
    public Task HandleAsync(WorkspaceCreated integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.WorkspaceCreated,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.OwnerUserId, integrationEvent.OwnerName),
                Payload.Of(new { workspaceName = integrationEvent.WorkspaceName }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Workspace, integrationEvent.TenantId.Value),
                    new TimelineSubject(TimelineSubjects.User, integrationEvent.OwnerUserId),
                ]),
            cancellationToken);
}

internal sealed class MemberJoinedProjection(TimelineWriter writer) : IIntegrationEventConsumer<MemberJoined>
{
    public Task HandleAsync(MemberJoined integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.MemberJoined,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.UserId, integrationEvent.FullName),
                Payload.Of(new { role = integrationEvent.Role, invitedByName = integrationEvent.InvitedByName }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Workspace, integrationEvent.TenantId.Value),
                    new TimelineSubject(TimelineSubjects.User, integrationEvent.UserId),
                ]),
            cancellationToken);
}

internal sealed class InvitationSentProjection(TimelineWriter writer) : IIntegrationEventConsumer<InvitationSent>
{
    public Task HandleAsync(InvitationSent integrationEvent, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            TimelineEntry.Create(
                integrationEvent.TenantId,
                TimelineEntryTypes.InvitationSent,
                integrationEvent.OccurredAt,
                TimelineActor.User(integrationEvent.InvitedByUserId, integrationEvent.InvitedByName),
                Payload.Of(new { email = integrationEvent.Email, role = integrationEvent.Role }),
                integrationEvent.EventId.ToString(),
                [
                    new TimelineSubject(TimelineSubjects.Workspace, integrationEvent.TenantId.Value),
                    new TimelineSubject(TimelineSubjects.User, integrationEvent.InvitedByUserId),
                ]),
            cancellationToken);
}

internal static class Payload
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Of(object values) => JsonSerializer.Serialize(values, Options);
}
