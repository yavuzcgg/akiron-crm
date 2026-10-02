using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Sales.Domain;

public readonly record struct CatalogItemId(Guid Value) : ITypedId<CatalogItemId>
{
    public static CatalogItemId New() => new(Guid.CreateVersion7());

    public static CatalogItemId From(Guid value) => new(value);
}

/// <param name="WithholdingTenths">KDV tevkifatı as tenths of the VAT the buyer withholds (ticari reklam: 3 → 3/10); 0 for none.</param>
/// <param name="WithholdingCode">The GİB code the invoice will carry (e.g. 627); free text until Finance (Phase 5) brings the official list.</param>
public sealed record CatalogItemDetails(
    string Name,
    string? Description,
    string Unit,
    decimal UnitPrice,
    Currency Currency,
    bool PriceIncludesVat,
    int VatRate,
    int WithholdingTenths,
    string? WithholdingCode);

/// <summary>
/// A service or product the agency sells ("Logo tasarımı", "Aylık sosyal medya yönetimi"),
/// with the price and tax defaults a quote line starts from. Lines copy these values, so a
/// later price change never alters a quote that was already sent.
/// </summary>
public sealed class CatalogItem : Entity<CatalogItemId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 200;

    private CatalogItem(CatalogItemId id)
        : base(id)
    {
        Name = string.Empty;
        Unit = Units.Piece;
        Currency = Currency.Try;
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public string Unit { get; private set; }

    /// <summary>Four decimals: unit prices carry more precision than totals (ADR-0004).</summary>
    public decimal UnitPrice { get; private set; }

    public Currency Currency { get; private set; }

    /// <summary>Whether <see cref="UnitPrice"/> includes VAT (retail habit); quote lines always hold the net price.</summary>
    public bool PriceIncludesVat { get; private set; }

    public int VatRate { get; private set; }

    public int WithholdingTenths { get; private set; }

    public string? WithholdingCode { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    /// <summary>The price without VAT, as a quote line holds it.</summary>
    public decimal NetUnitPrice => PriceIncludesVat ? QuoteMath.WithoutVat(UnitPrice, VatRate) : UnitPrice;

    public static CatalogItem Create(CatalogItemDetails details)
    {
        var item = new CatalogItem(CatalogItemId.New());
        item.Update(details);
        return item;
    }

    public void Update(CatalogItemDetails details)
    {
        Name = details.Name.Trim();
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        Unit = details.Unit;
        UnitPrice = decimal.Round(details.UnitPrice, 4);
        Currency = details.Currency;
        PriceIncludesVat = details.PriceIncludesVat;
        VatRate = details.VatRate;
        WithholdingTenths = details.WithholdingTenths;
        WithholdingCode = string.IsNullOrWhiteSpace(details.WithholdingCode) ? null : details.WithholdingCode.Trim();
    }
}

/// <summary>Units a line can be priced in; stable keys the client translates.</summary>
public static class Units
{
    public const string Piece = "piece";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Piece, "hour", "day", "month", "year", "project", "package", "post", "page", "word",
    };
}

/// <summary>VAT rates in force in Türkiye (KDV Kanunu 28: 1, 10, 20; 0 for exempt supplies).</summary>
public static class VatRates
{
    public static readonly IReadOnlySet<int> All = new HashSet<int> { 0, 1, 10, 20 };
}
