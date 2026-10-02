using Akiron.BuildingBlocks.Domain;
using Akiron.Modules.Sales.Domain;
using Akiron.Modules.Sales.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Sales.Features.Catalog;

internal sealed record CatalogItemCommand(
    string Name,
    string? Description,
    string Unit,
    decimal UnitPrice,
    string Currency = "TRY",
    bool PriceIncludesVat = false,
    int VatRate = 20,
    int WithholdingTenths = 0,
    string? WithholdingCode = null);

internal sealed class CatalogItemValidator : AbstractValidator<CatalogItemCommand>
{
    public CatalogItemValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(CatalogItem.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(2000);
        RuleFor(command => command.Unit).Must(unit => Units.All.Contains(unit)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.UnitPrice).InclusiveBetween(0m, 1_000_000_000m);
        RuleFor(command => command.Currency).Must(Currency.IsSupported).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.VatRate).Must(rate => VatRates.All.Contains(rate)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.WithholdingTenths).InclusiveBetween(0, 10);
        RuleFor(command => command.WithholdingCode).MaximumLength(10);
    }
}

internal sealed record CatalogItemResponse(
    Guid Id,
    string Name,
    string? Description,
    string Unit,
    decimal UnitPrice,
    string Currency,
    bool PriceIncludesVat,
    int VatRate,
    int WithholdingTenths,
    string? WithholdingCode,
    decimal NetUnitPrice);

/// <summary>The services and products the agency sells; quote lines start from these.</summary>
internal sealed class CatalogHandler(SalesDbContext db)
{
    public async Task<IReadOnlyList<CatalogItemResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var query = db.CatalogItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(item => EF.Functions.ILike(item.Name, pattern, "\\"));
        }

        return (await query.OrderBy(item => item.Name).Take(500).ToListAsync(cancellationToken)).Select(ToResponse).ToList();
    }

    public async Task<CatalogItemResponse> CreateAsync(CatalogItemCommand command, CancellationToken cancellationToken)
    {
        var item = CatalogItem.Create(Details(command));
        db.CatalogItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    public async Task<Result<CatalogItemResponse>> UpdateAsync(Guid id, CatalogItemCommand command, CancellationToken cancellationToken)
    {
        var item = await FindAsync(id, cancellationToken);
        if (item is null)
        {
            return SalesErrors.CatalogItemNotFound;
        }

        item.Update(Details(command));
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    /// <summary>Archives the item: quotes that used it keep their own copy of every value.</summary>
    public async Task<Result<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await FindAsync(id, cancellationToken);
        if (item is null)
        {
            return SalesErrors.CatalogItemNotFound;
        }

        db.CatalogItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<CatalogItem?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var itemId = CatalogItemId.From(id);
        return db.CatalogItems.FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);
    }

    private static CatalogItemDetails Details(CatalogItemCommand command) => new(
        command.Name,
        command.Description,
        command.Unit,
        command.UnitPrice,
        Currency.From(command.Currency),
        command.PriceIncludesVat,
        command.VatRate,
        command.WithholdingTenths,
        command.WithholdingCode);

    private static CatalogItemResponse ToResponse(CatalogItem item) => new(
        item.Id.Value,
        item.Name,
        item.Description,
        item.Unit,
        item.UnitPrice,
        item.Currency.Code,
        item.PriceIncludesVat,
        item.VatRate,
        item.WithholdingTenths,
        item.WithholdingCode,
        item.NetUnitPrice);
}
