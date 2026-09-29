using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Notifications.Domain;

public readonly record struct NotificationId(Guid Value) : ITypedId<NotificationId>
{
    public static NotificationId New() => new(Guid.CreateVersion7());

    public static NotificationId From(Guid value) => new(value);
}

/// <summary>
/// Something one person should look at. Like timeline entries it has a stable type and a payload
/// the client renders; unlike them it has one recipient and a read state.
/// </summary>
public sealed class Notification : Entity<NotificationId>, ITenantScoped
{
    private Notification(NotificationId id, TenantId tenantId, Guid recipientUserId, string type, string payload, string idempotencyKey, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        RecipientUserId = recipientUserId;
        Type = type;
        Payload = payload;
        IdempotencyKey = idempotencyKey;
        CreatedAt = createdAt;
    }

    public TenantId TenantId { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public string Type { get; private set; }

    /// <summary>JSON (<c>jsonb</c>) with the values the notification renders.</summary>
    public string Payload { get; private set; }

    /// <summary>Source event id plus recipient; re-delivered events never notify twice.</summary>
    public string IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(TenantId tenantId, Guid recipientUserId, string type, string payload, Guid sourceEventId, DateTimeOffset createdAt) =>
        new(NotificationId.New(), tenantId, recipientUserId, type, payload, $"{sourceEventId:N}:{recipientUserId:N}", createdAt);

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}
