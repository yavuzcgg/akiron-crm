using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Social.Domain;

public readonly record struct ContentPostId(Guid Value) : ITypedId<ContentPostId>
{
    public static ContentPostId New() => new(Guid.CreateVersion7());

    public static ContentPostId From(Guid value) => new(value);
}

/// <summary>
/// Where a post is on its way to going live. The client sees it from <see cref="ClientReview"/> on.
/// </summary>
public enum ContentStatus
{
    /// <summary>Being written; only the team sees it.</summary>
    Draft,

    /// <summary>Waiting for someone in the agency to check it before the client does.</summary>
    InternalReview,

    /// <summary>With the client, waiting for their answer.</summary>
    ClientReview,

    /// <summary>The client asked for changes; the note says what.</summary>
    ChangesRequested,

    /// <summary>Approved; waits for its day.</summary>
    Approved,

    Published,
}

/// <summary>What a person writes on a post; the status moves separately.</summary>
public sealed record ContentDetails(
    string Title,
    string? Caption,
    string? Hashtags,
    IReadOnlyList<string> Platforms,
    string Format,
    DateOnly PlannedDate,
    TimeOnly? PlannedTime);

/// <summary>
/// One planned social media post for a client (içerik takvimi): what goes out, where and when, and
/// its approval. Media are files attached to it. Publishing is recorded by hand until the networks'
/// APIs are connected (Phase 6); the plan and the approvals are what the agency sells today.
/// </summary>
public sealed class ContentPost : Entity<ContentPostId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int CaptionMaxLength = 5000;
    public const int HashtagsMaxLength = 1000;
    public const int NoteMaxLength = 2000;

    public static readonly IReadOnlyList<string> KnownPlatforms = ["instagram", "facebook", "linkedin", "x", "tiktok", "youtube"];
    public static readonly IReadOnlyList<string> KnownFormats = ["post", "story", "reel", "carousel", "video"];

    private ContentPost(ContentPostId id)
        : base(id)
    {
        Title = string.Empty;
        PartyName = string.Empty;
        Format = "post";
        PlatformCodes = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid PartyId { get; private set; }

    public string PartyName { get; private set; }

    /// <summary>The team's name for the post ("Kasım kampanya duyurusu"); the caption is what goes out.</summary>
    public string Title { get; private set; }

    public string? Caption { get; private set; }

    public string? Hashtags { get; private set; }

    /// <summary>The networks it goes to, comma-separated ("instagram,facebook"); read through <see cref="Platforms"/>.</summary>
    public string PlatformCodes { get; private set; }

    public IReadOnlyList<string> Platforms => PlatformCodes.Length == 0 ? [] : PlatformCodes.Split(',');

    public string Format { get; private set; }

    public DateOnly PlannedDate { get; private set; }

    public TimeOnly? PlannedTime { get; private set; }

    public ContentStatus Status { get; private set; }

    /// <summary>1 for the first version the client saw; each revision sent after that raises it.</summary>
    public int Revision { get; private set; }

    /// <summary>The client's last request for changes.</summary>
    public string? ClientNote { get; private set; }

    /// <summary>The internal reviewer's note when it was sent back to the author.</summary>
    public string? ReviewNote { get; private set; }

    public string? DecidedByName { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public DateTimeOffset? SentToClientAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public string? PublishedUrl { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public string? OwnerName { get; private set; }

    /// <summary>The planned date the "publish today" reminder went out for; moving the date re-arms it.</summary>
    public DateOnly? PublishReminderSentFor { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    /// <summary>Whether the client may see the post: from the moment it was sent to them.</summary>
    public bool IsVisibleToClient => Status is ContentStatus.ClientReview or ContentStatus.ChangesRequested or ContentStatus.Approved or ContentStatus.Published;

    /// <summary>Whether its text and media can still change without a new round with the client.</summary>
    public bool IsEditable => Status is ContentStatus.Draft or ContentStatus.InternalReview or ContentStatus.ChangesRequested;

    public static ContentPost Create(Guid partyId, string partyName, ContentDetails details, Guid ownerUserId, string? ownerName)
    {
        var post = new ContentPost(ContentPostId.New())
        {
            PartyId = partyId,
            PartyName = partyName,
            Status = ContentStatus.Draft,
            OwnerUserId = ownerUserId,
            OwnerName = ownerName,
        };
        post.Apply(details);
        return post;
    }

    public bool Update(ContentDetails details)
    {
        if (!IsEditable)
        {
            return false;
        }

        Apply(details);
        return true;
    }

    /// <summary>The date can move at any time before publishing without a new approval; the content cannot.</summary>
    public bool Reschedule(DateOnly date, TimeOnly? time)
    {
        if (Status == ContentStatus.Published)
        {
            return false;
        }

        PlannedDate = date;
        PlannedTime = time;
        return true;
    }

    public bool SubmitForReview()
    {
        if (Status is not (ContentStatus.Draft or ContentStatus.ChangesRequested))
        {
            return false;
        }

        Status = ContentStatus.InternalReview;
        ReviewNote = null;
        return true;
    }

    public bool ReturnToAuthor(string note)
    {
        if (Status != ContentStatus.InternalReview)
        {
            return false;
        }

        Status = ContentStatus.Draft;
        ReviewNote = note.Trim();
        return true;
    }

    public bool SendToClient(DateTimeOffset now)
    {
        if (!IsEditable)
        {
            return false;
        }

        Status = ContentStatus.ClientReview;
        Revision++;
        SentToClientAt = now;
        ReviewNote = null;
        ClientNote = null;
        DecidedAt = null;
        DecidedByName = null;
        return true;
    }

    /// <summary>The client's answer (or the client's word, recorded by staff). Approval needs nothing pending; a change request needs it to be with the client.</summary>
    public bool Decide(bool approve, string decidedByName, string? note, DateTimeOffset now)
    {
        var allowed = approve
            ? Status is ContentStatus.ClientReview or ContentStatus.Draft or ContentStatus.InternalReview or ContentStatus.ChangesRequested
            : Status == ContentStatus.ClientReview;
        if (!allowed)
        {
            return false;
        }

        if (approve && Revision == 0)
        {
            Revision = 1;
        }

        Status = approve ? ContentStatus.Approved : ContentStatus.ChangesRequested;
        ClientNote = approve ? null : note?.Trim();
        DecidedByName = decidedByName.Trim();
        DecidedAt = now;
        return true;
    }

    /// <summary>Opens a post that is with the client or approved for another round of edits.</summary>
    public bool Revise()
    {
        if (Status is not (ContentStatus.ClientReview or ContentStatus.Approved))
        {
            return false;
        }

        Status = ContentStatus.Draft;
        return true;
    }

    public bool MarkPublished(string? url, DateTimeOffset now)
    {
        if (Status != ContentStatus.Approved)
        {
            return false;
        }

        Status = ContentStatus.Published;
        PublishedAt = now;
        PublishedUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        return true;
    }

    /// <summary>Whether the "publish today" reminder is due; marks it sent.</summary>
    public bool TakePublishReminderIfDue(DateOnly today)
    {
        if (Status != ContentStatus.Approved || PlannedDate > today || PublishReminderSentFor == PlannedDate)
        {
            return false;
        }

        PublishReminderSentFor = PlannedDate;
        return true;
    }

    private void Apply(ContentDetails details)
    {
        Title = details.Title.Trim();
        Caption = string.IsNullOrWhiteSpace(details.Caption) ? null : details.Caption.Trim();
        Hashtags = string.IsNullOrWhiteSpace(details.Hashtags) ? null : details.Hashtags.Trim();
        PlatformCodes = string.Join(",", details.Platforms.Distinct());
        Format = details.Format;
        PlannedDate = details.PlannedDate;
        PlannedTime = details.PlannedTime;
    }
}
