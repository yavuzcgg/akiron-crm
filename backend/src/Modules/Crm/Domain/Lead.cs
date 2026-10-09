using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Crm.Domain;

public readonly record struct LeadId(Guid Value) : ITypedId<LeadId>
{
    public static LeadId New() => new(Guid.CreateVersion7());

    public static LeadId From(Guid value) => new(value);
}

/// <summary>The funnel's columns, in order. <see cref="Won"/> and <see cref="Lost"/> close the lead.</summary>
public enum LeadStage
{
    New,
    Contacted,
    Meeting,
    Proposal,
    Won,
    Lost,
}

public enum LeadSource
{
    Website,
    WebForm,
    Referral,
    Instagram,
    Whatsapp,
    Phone,
    Email,
    Event,
    Other,
}

/// <summary>What a person can change on a lead; the stage moves separately.</summary>
public sealed record LeadDetails(
    string Title,
    string? CompanyName,
    string? ContactName,
    string? Email,
    string? Phone,
    LeadSource Source,
    decimal? EstimatedValue,
    Currency Currency,
    DateOnly? ExpectedCloseDate,
    Guid? OwnerUserId,
    string? OwnerName,
    string? NextStep,
    DateOnly? NextStepDate,
    string? Notes);

/// <summary>
/// A sales opportunity (fırsat / aday müşteri): someone who might buy, before or alongside a party.
/// It moves through the funnel on a board; winning it usually means a party exists (converted, or
/// linked from the start) and a quote was accepted. History lives on the timeline (subject "lead").
/// </summary>
public sealed class Lead : Entity<LeadId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int NotesMaxLength = 4000;

    private Lead(LeadId id)
        : base(id)
    {
        Title = string.Empty;
        Currency = Currency.Try;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>What they want, e.g. "Web sitesi yenileme".</summary>
    public string Title { get; private set; }

    public string? CompanyName { get; private set; }

    public string? ContactName { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>The party the lead belongs to: chosen at the start or created when it was converted.</summary>
    public Guid? PartyId { get; private set; }

    public LeadSource Source { get; private set; }

    public LeadStage Stage { get; private set; }

    /// <summary>Order within the stage's column (fractional, like work orders).</summary>
    public double Rank { get; private set; }

    public decimal? EstimatedValue { get; private set; }

    public Currency Currency { get; private set; }

    public DateOnly? ExpectedCloseDate { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public string? OwnerName { get; private set; }

    /// <summary>The next thing to do ("Teklif sonrası ara") and when; its owner is reminded that day.</summary>
    public string? NextStep { get; private set; }

    public DateOnly? NextStepDate { get; private set; }

    /// <summary>The next-step date a reminder was sent for; changing the date re-arms it.</summary>
    public DateOnly? FollowUpSentFor { get; private set; }

    public string? Notes { get; private set; }

    public string? LostReason { get; private set; }

    /// <summary>The latest quote sent to this lead's party while the lead was open.</summary>
    public Guid? QuoteId { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public bool IsOpen => Stage is not (LeadStage.Won or LeadStage.Lost);

    public static Lead Create(LeadDetails details, Guid? partyId, double rank)
    {
        var lead = new Lead(LeadId.New()) { PartyId = partyId, Stage = LeadStage.New, Rank = rank };
        lead.Update(details);
        return lead;
    }

    public void Update(LeadDetails details)
    {
        if (details.NextStepDate != NextStepDate)
        {
            FollowUpSentFor = null;
        }

        Title = details.Title.Trim();
        CompanyName = Clean(details.CompanyName);
        ContactName = Clean(details.ContactName);
        Email = Clean(details.Email);
        Phone = Clean(details.Phone);
        Source = details.Source;
        EstimatedValue = details.EstimatedValue;
        Currency = details.Currency;
        ExpectedCloseDate = details.ExpectedCloseDate;
        OwnerUserId = details.OwnerUserId;
        OwnerName = details.OwnerUserId is null ? null : details.OwnerName;
        NextStep = Clean(details.NextStep);
        NextStepDate = details.NextStepDate;
        Notes = Clean(details.Notes);
    }

    /// <summary>Moves on the board; returns the stage it left, or null when it did not change stage.</summary>
    public LeadStage? MoveTo(LeadStage stage, double rank, DateTimeOffset now, string? lostReason = null)
    {
        Rank = rank;
        if (stage == Stage)
        {
            return null;
        }

        var from = Stage;
        Stage = stage;
        LostReason = stage == LeadStage.Lost ? lostReason?.Trim() : null;
        ClosedAt = IsOpen ? null : now;
        return from;
    }

    public void Rerank(double rank) => Rank = rank;

    public void LinkParty(Guid partyId) => PartyId = partyId;

    public void NoteQuote(Guid quoteId) => QuoteId = quoteId;

    /// <summary>Whether the follow-up reminder is due on <paramref name="today"/>; marks it sent.</summary>
    public bool TakeFollowUpIfDue(DateOnly today)
    {
        if (!IsOpen || OwnerUserId is null || NextStepDate is not { } date || date > today || FollowUpSentFor == date)
        {
            return false;
        }

        FollowUpSentFor = date;
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public readonly record struct LeadFormId(Guid Value) : ITypedId<LeadFormId>
{
    public static LeadFormId New() => new(Guid.CreateVersion7());

    public static LeadFormId From(Guid value) => new(value);
}

/// <summary>
/// The tenant's public web form: a site posts to <c>/api/v1/crm/public/forms/{key}</c> and a lead
/// appears on the board. The key is public by design (it sits in the site's HTML); rotating it
/// stops old embeds, and disabling stops all of them.
/// </summary>
public sealed class LeadForm : Entity<LeadFormId>, ITenantScoped, IAuditable
{
    private LeadForm(LeadFormId id, string key)
        : base(id)
    {
        Key = key;
    }

    public TenantId TenantId { get; private set; }

    public string Key { get; private set; }

    public bool Enabled { get; private set; }

    /// <summary>Where the visitor lands after sending; empty shows Akiron's thank-you page.</summary>
    public string? RedirectUrl { get; private set; }

    /// <summary>Who gets new web leads (and the notification); empty notifies everyone who manages leads.</summary>
    public Guid? OwnerUserId { get; private set; }

    public string? OwnerName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static LeadForm Create(string key) => new(LeadFormId.New(), key) { Enabled = true };

    public void Configure(bool enabled, string? redirectUrl, Guid? ownerUserId, string? ownerName)
    {
        Enabled = enabled;
        RedirectUrl = string.IsNullOrWhiteSpace(redirectUrl) ? null : redirectUrl.Trim();
        OwnerUserId = ownerUserId;
        OwnerName = ownerUserId is null ? null : ownerName;
    }

    public void Rotate(string key) => Key = key;
}
