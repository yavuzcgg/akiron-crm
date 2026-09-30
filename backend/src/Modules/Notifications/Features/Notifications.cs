using System.Text.Json;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Jobs;
using Akiron.Contracts.People;
using Akiron.Contracts.Timeline;
using Akiron.Modules.Notifications.Domain;
using Akiron.Modules.Notifications.Persistence;
using Akiron.Modules.Notifications.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Akiron.Modules.Notifications.Features;

internal sealed record NotificationResponse(Guid Id, string Type, JsonElement Payload, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

internal sealed record NotificationListResponse(IReadOnlyList<NotificationResponse> Items, int UnreadCount);

/// <summary>Stores a notification once and pushes it to the recipient's open tabs.</summary>
internal sealed class NotificationSender(NotificationsDbContext db, IHubContext<NotificationHub> hub)
{
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (await db.Notifications.AnyAsync(existing => existing.IdempotencyKey == notification.IdempotencyKey, cancellationToken))
        {
            return;
        }

        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: NotificationConfiguration.IdempotencyUnique,
        })
        {
            db.ChangeTracker.Clear();
            return;
        }

        // Best effort: the row is the truth, the push only saves a refresh.
        await hub.Clients.Group(NotificationHub.GroupOf(notification.TenantId, notification.RecipientUserId))
            .SendAsync(NotificationHub.NotificationMethod, NotificationEndpoints.ToResponse(notification), cancellationToken);
    }
}

/// <summary>Tells the person who sent an invitation that it was accepted.</summary>
internal sealed class MemberJoinedNotification(NotificationSender sender) : IIntegrationEventConsumer<MemberJoined>
{
    public const string Type = "identity.invitation.accepted";

    public Task HandleAsync(MemberJoined integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.InvitedByUserId is not { } inviter || inviter == integrationEvent.UserId)
        {
            return Task.CompletedTask;
        }

        var payload = JsonSerializer.Serialize(new { memberName = integrationEvent.FullName, role = integrationEvent.Role }, JsonSerializerOptions.Web);
        return sender.SendAsync(
            Notification.Create(integrationEvent.TenantId, inviter, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt),
            cancellationToken);
    }
}

/// <summary>Tells people they were put on a work order (not the person who did it).</summary>
internal sealed class WorkOrderAssignedNotification(NotificationSender sender) : IIntegrationEventConsumer<WorkOrderAssigned>
{
    public const string Type = "jobs.work_order.assigned";

    public async Task HandleAsync(WorkOrderAssigned integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                workOrderId = integrationEvent.WorkOrderId,
                number = integrationEvent.Number,
                title = integrationEvent.Title,
                assignedByName = integrationEvent.AssignedByName,
            },
            JsonSerializerOptions.Web);

        foreach (var userId in integrationEvent.AddedUserIds.Where(userId => userId != integrationEvent.AssignedByUserId))
        {
            await sender.SendAsync(
                Notification.Create(integrationEvent.TenantId, userId, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt),
                cancellationToken);
        }
    }
}

/// <summary>Tells everyone who can approve leave that a request is waiting (not the requester).</summary>
internal sealed class LeaveRequestedNotification(NotificationSender sender, IMemberDirectory members) : IIntegrationEventConsumer<LeaveRequested>
{
    public const string Type = "people.leave.requested";

    public async Task HandleAsync(LeaveRequested integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                leaveRequestId = integrationEvent.LeaveRequestId,
                userName = integrationEvent.UserName,
                type = integrationEvent.Type,
                startDate = integrationEvent.StartDate,
                endDate = integrationEvent.EndDate,
                days = integrationEvent.Days,
            },
            JsonSerializerOptions.Web);

        foreach (var approver in await members.WithPermissionAsync("people.leave.approve", cancellationToken))
        {
            if (approver.UserId != integrationEvent.UserId)
            {
                await sender.SendAsync(
                    Notification.Create(integrationEvent.TenantId, approver.UserId, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt),
                    cancellationToken);
            }
        }
    }
}

/// <summary>Tells the requester how their leave request was decided.</summary>
internal sealed class LeaveDecidedNotification(NotificationSender sender) : IIntegrationEventConsumer<LeaveDecided>
{
    public const string Type = "people.leave.decided";

    public Task HandleAsync(LeaveDecided integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.UserId == integrationEvent.DecidedByUserId)
        {
            return Task.CompletedTask;
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                leaveRequestId = integrationEvent.LeaveRequestId,
                approved = integrationEvent.Approved,
                startDate = integrationEvent.StartDate,
                endDate = integrationEvent.EndDate,
                decidedByName = integrationEvent.DecidedByName,
                note = integrationEvent.Note,
            },
            JsonSerializerOptions.Web);
        return sender.SendAsync(
            Notification.Create(integrationEvent.TenantId, integrationEvent.UserId, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt),
            cancellationToken);
    }
}

/// <summary>Tells people they were @mentioned in a note (not the author mentioning themselves).</summary>
internal sealed class NoteMentionedNotification(NotificationSender sender) : IIntegrationEventConsumer<NoteMentioned>
{
    public const string Type = "timeline.note.mentioned";

    public async Task HandleAsync(NoteMentioned integrationEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                subjectType = integrationEvent.SubjectType,
                subjectId = integrationEvent.SubjectId,
                excerpt = integrationEvent.Excerpt,
                authorName = integrationEvent.AuthorName,
            },
            JsonSerializerOptions.Web);

        foreach (var userId in integrationEvent.MentionedUserIds.Where(userId => userId != integrationEvent.AuthorUserId))
        {
            await sender.SendAsync(
                Notification.Create(integrationEvent.TenantId, userId, Type, payload, integrationEvent.EventId, integrationEvent.OccurredAt),
                cancellationToken);
        }
    }
}

internal static class NotificationEndpoints
{
    private const int MaxItems = 50;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // A person's own inbox: every signed-in member has one, so no extra permission is needed.
        endpoints.MapGet("/", async (bool? unreadOnly, NotificationsDbContext db, ICurrentUser currentUser, CancellationToken cancellationToken) =>
            {
                var me = RecipientOf(currentUser);
                var mine = db.Notifications.Where(notification => notification.RecipientUserId == me);
                var items = await mine
                    .Where(notification => unreadOnly != true || notification.ReadAt == null)
                    .OrderByDescending(notification => notification.CreatedAt)
                    .Take(MaxItems)
                    .ToListAsync(cancellationToken);
                var unread = await mine.CountAsync(notification => notification.ReadAt == null, cancellationToken);

                return Results.Ok(new NotificationListResponse(items.Select(ToResponse).ToList(), unread));
            })
            .RequireAuthorization()
            .Produces<NotificationListResponse>()
            .WithSummary("The signed-in person's newest notifications and unread count");

        endpoints.MapPost("/{id:guid}/read", async (Guid id, NotificationsDbContext db, ICurrentUser currentUser, TimeProvider timeProvider, CancellationToken cancellationToken) =>
            {
                var me = RecipientOf(currentUser);
                var notificationId = NotificationId.From(id);
                var notification = await db.Notifications
                    .FirstOrDefaultAsync(candidate => candidate.Id == notificationId && candidate.RecipientUserId == me, cancellationToken);
                if (notification is null)
                {
                    return Error.NotFound("notifications.notification.not_found", "No such notification for you.").ToProblem();
                }

                notification.MarkRead(timeProvider.GetUtcNow());
                await db.SaveChangesAsync(cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Mark one notification read");

        endpoints.MapPost("/read-all", async (NotificationsDbContext db, ICurrentUser currentUser, TimeProvider timeProvider, CancellationToken cancellationToken) =>
            {
                var me = RecipientOf(currentUser);
                var now = timeProvider.GetUtcNow();
                await db.Notifications
                    .Where(notification => notification.RecipientUserId == me && notification.ReadAt == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAt, now), cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Mark all of the signed-in person's notifications read");
    }

    internal static NotificationResponse ToResponse(Notification notification)
    {
        using var payload = JsonDocument.Parse(notification.Payload);
        return new NotificationResponse(notification.Id.Value, notification.Type, payload.RootElement.Clone(), notification.CreatedAt, notification.ReadAt);
    }

    private static Guid RecipientOf(ICurrentUser currentUser) =>
        currentUser.UserId?.Value ?? throw new InvalidOperationException("Notifications need a signed-in user.");
}
