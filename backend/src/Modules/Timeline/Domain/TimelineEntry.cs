using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Timeline.Domain;

public readonly record struct TimelineEntryId(Guid Value) : ITypedId<TimelineEntryId>
{
    public static TimelineEntryId New() => new(Guid.CreateVersion7());

    public static TimelineEntryId From(Guid value) => new(value);
}

/// <summary>Who caused the entry. Stored as text so the column reads well in SQL.</summary>
public enum ActorKind
{
    User,
    PortalContact,
    System,
    Integration,
}

/// <summary><c>Portal</c> entries are also shown to the customer in the client portal (phase 4).</summary>
public enum TimelineVisibility
{
    Internal,
    Portal,
}

/// <summary>
/// One line of business history (ADR-0009). Immutable: corrections are new entries. The payload
/// holds snapshots of what the line displays, so it renders without asking the source module.
/// </summary>
public sealed class TimelineEntry : Entity<TimelineEntryId>, ITenantScoped
{
    private readonly List<TimelineLink> _links = [];

    private TimelineEntry(
        TimelineEntryId id,
        TenantId tenantId,
        string type,
        DateTimeOffset occurredAt,
        ActorKind actorKind,
        Guid? actorId,
        string? actorName,
        string payload,
        string idempotencyKey,
        TimelineVisibility visibility)
        : base(id)
    {
        TenantId = tenantId;
        Type = type;
        OccurredAt = occurredAt;
        ActorKind = actorKind;
        ActorId = actorId;
        ActorName = actorName;
        Payload = payload;
        IdempotencyKey = idempotencyKey;
        Visibility = visibility;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>Stable key, e.g. <c>identity.member.joined</c>; the client maps it to an icon and a template.</summary>
    public string Type { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public Guid? ActorId { get; private set; }

    public string? ActorName { get; private set; }

    /// <summary>JSON object (<c>jsonb</c>) with the values the line renders.</summary>
    public string Payload { get; private set; }

    /// <summary>The source event id, or a fresh id for entries written directly; unique per tenant.</summary>
    public string IdempotencyKey { get; private set; }

    public TimelineVisibility Visibility { get; private set; }

    public IReadOnlyList<TimelineLink> Links => _links;

    public static TimelineEntry Create(
        TenantId tenantId,
        string type,
        DateTimeOffset occurredAt,
        TimelineActor actor,
        string payload,
        string idempotencyKey,
        IEnumerable<TimelineSubject> subjects,
        TimelineVisibility visibility = TimelineVisibility.Internal)
    {
        var entry = new TimelineEntry(
            TimelineEntryId.New(), tenantId, type, occurredAt, actor.Kind, actor.Id, actor.Name, payload, idempotencyKey, visibility);

        foreach (var subject in subjects.Distinct())
        {
            entry._links.Add(new TimelineLink(entry.Id, tenantId, subject.Type, subject.Id, occurredAt));
        }

        return entry;
    }
}

/// <summary>Places an entry in one stream: the party's, the invoice's, the user's …</summary>
public sealed class TimelineLink : ITenantScoped
{
    internal TimelineLink(TimelineEntryId entryId, TenantId tenantId, string subjectType, Guid subjectId, DateTimeOffset occurredAt)
    {
        EntryId = entryId;
        TenantId = tenantId;
        SubjectType = subjectType;
        SubjectId = subjectId;
        OccurredAt = occurredAt;
    }

    public TimelineEntryId EntryId { get; private set; }

    public TenantId TenantId { get; private set; }

    public string SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    /// <summary>Copied from the entry so a stream is read from this table's index alone.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Database identity: insertion order, the tie-breaker for equal timestamps in keyset paging.</summary>
    public long Sequence { get; private set; }
}

public sealed record TimelineActor(ActorKind Kind, Guid? Id, string? Name)
{
    public static TimelineActor User(Guid id, string? name) => new(ActorKind.User, id, name);

    public static readonly TimelineActor SystemActor = new(ActorKind.System, null, null);
}

public sealed record TimelineSubject(string Type, Guid Id);
