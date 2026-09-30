using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Crm.Domain;

public readonly record struct PartyContactId(Guid Value) : ITypedId<PartyContactId>
{
    public static PartyContactId New() => new(Guid.CreateVersion7());

    public static PartyContactId From(Guid value) => new(value);
}

public sealed record PartyContactDetails(string FullName, string? Title, string? Email, string? Phone, bool IsPrimary);

/// <summary>A person at a counterparty: who approves the quote, who pays the invoice.</summary>
public sealed class PartyContact : Entity<PartyContactId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int FullNameMaxLength = 200;

    private PartyContact(PartyContactId id, PartyId partyId)
        : base(id)
    {
        PartyId = partyId;
        FullName = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public PartyId PartyId { get; private set; }

    public string FullName { get; private set; }

    public string? Title { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>The default recipient for quotes and invoices; at most one per party.</summary>
    public bool IsPrimary { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static PartyContact Create(PartyId partyId, PartyContactDetails details)
    {
        var contact = new PartyContact(PartyContactId.New(), partyId);
        contact.Update(details);
        return contact;
    }

    public void Update(PartyContactDetails details)
    {
        FullName = details.FullName.Trim();
        Title = Clean(details.Title);
        Email = Clean(details.Email)?.ToLowerInvariant();
        Phone = Clean(details.Phone);
        IsPrimary = details.IsPrimary;
    }

    public void ClearPrimary() => IsPrimary = false;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
