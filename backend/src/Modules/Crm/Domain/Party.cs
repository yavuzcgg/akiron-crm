using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Crm.Domain;

public readonly record struct PartyId(Guid Value) : ITypedId<PartyId>
{
    public static PartyId New() => new(Guid.CreateVersion7());

    public static PartyId From(Guid value) => new(value);
}

public enum PartyKind
{
    Company,
    Person,
}

/// <summary>What can be changed on a party after creation; the code cannot.</summary>
public sealed record PartyDetails(
    PartyKind Kind,
    string Name,
    bool IsCustomer,
    bool IsSupplier,
    string? TaxNumber,
    string? TaxOffice,
    string? Email,
    string? Phone,
    string? Website,
    string? City,
    string? District,
    string? AddressLine);

/// <summary>
/// A counterparty — the Turkish "cari" (ADR-0008): one record whether it buys, sells, or both.
/// Every quote, job, invoice and payment will hang off a party.
/// </summary>
public sealed class Party : Entity<PartyId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 250;
    public const int CodeMaxLength = 30;

    private Party(PartyId id, string code)
        : base(id)
    {
        Code = code;
        Name = string.Empty;
        SearchText = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>Short, stable, per-tenant code (C00001); what accountants and Logo sync refer to.</summary>
    public string Code { get; private set; }

    public PartyKind Kind { get; private set; }

    public string Name { get; private set; }

    public bool IsCustomer { get; private set; }

    public bool IsSupplier { get; private set; }

    public string? TaxNumber { get; private set; }

    public string? TaxOffice { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Website { get; private set; }

    public string? City { get; private set; }

    public string? District { get; private set; }

    public string? AddressLine { get; private set; }

    /// <summary>Folded name, code, tax number, e-mail and phone for Turkish-aware search (<see cref="TurkishText"/>).</summary>
    [AuditIgnore]
    public string SearchText { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static Party Create(string code, PartyDetails details)
    {
        var party = new Party(PartyId.New(), code.Trim());
        party.Update(details);
        return party;
    }

    /// <summary>Applies the details and returns the camelCase names of the fields that actually changed.</summary>
    public IReadOnlyList<string> Update(PartyDetails details)
    {
        var changed = new List<string>();

        Kind = Set(Kind, details.Kind, "kind");
        Name = Set(Name, details.Name.Trim(), "name");
        IsCustomer = Set(IsCustomer, details.IsCustomer, "isCustomer");
        IsSupplier = Set(IsSupplier, details.IsSupplier, "isSupplier");
        TaxNumber = Set(TaxNumber, Clean(details.TaxNumber), "taxNumber");
        TaxOffice = Set(TaxOffice, Clean(details.TaxOffice), "taxOffice");
        Email = Set(Email, Clean(details.Email)?.ToLowerInvariant(), "email");
        Phone = Set(Phone, Clean(details.Phone), "phone");
        Website = Set(Website, Clean(details.Website), "website");
        City = Set(City, Clean(details.City), "city");
        District = Set(District, Clean(details.District), "district");
        AddressLine = Set(AddressLine, Clean(details.AddressLine), "addressLine");
        SearchText = TurkishText.Fold(string.Join(' ', new[] { Name, Code, TaxNumber, Email, Phone }.Where(part => !string.IsNullOrEmpty(part))));

        return changed;

        T Set<T>(T current, T next, string field)
        {
            if (!EqualityComparer<T>.Default.Equals(current, next))
            {
                changed.Add(field);
            }

            return next;
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
