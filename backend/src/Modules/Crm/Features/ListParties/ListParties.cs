using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.ListParties;

/// <summary>Bound from the query string next to <see cref="PageRequest"/>.</summary>
internal sealed record PartyFilter(
    [FromQuery(Name = "search")] string? Search = null,
    [FromQuery(Name = "role")] string? Role = null,
    [FromQuery(Name = "kind")] string? Kind = null);

internal sealed class PartyFilterValidator : AbstractValidator<PartyFilter>
{
    public PartyFilterValidator()
    {
        RuleFor(filter => filter.Search).MaximumLength(100);
        RuleFor(filter => filter.Role).Must(role => role is null or "customer" or "supplier").WithErrorCode("validation.invalid_value");
        RuleFor(filter => filter.Kind).Must(kind => kind is null or PartyKinds.Company or PartyKinds.Person).WithErrorCode("validation.invalid_value");
    }
}

internal sealed record PartyListItem(
    Guid Id,
    string Code,
    string Kind,
    string Name,
    bool IsCustomer,
    bool IsSupplier,
    string? TaxNumber,
    string? City,
    string? Email,
    string? Phone);

internal sealed class ListPartiesHandler(CrmDbContext db)
{
    public async Task<PagedResult<PartyListItem>> HandleAsync(PageRequest page, PartyFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Parties.AsNoTracking();

        // Search runs on the folded text, so "isik" finds "IŞIK" and "Işık" (TurkishText).
        var search = TurkishText.Fold(filter.Search);
        if (search.Length > 0)
        {
            var pattern = TurkishText.ContainsPattern(search);
            query = query.Where(party => EF.Functions.Like(party.SearchText, pattern, TurkishText.LikeEscape));
        }

        query = filter.Role switch
        {
            "customer" => query.Where(party => party.IsCustomer),
            "supplier" => query.Where(party => party.IsSupplier),
            _ => query,
        };

        if (filter.Kind is not null)
        {
            var kind = PartyKinds.Parse(filter.Kind);
            query = query.Where(party => party.Kind == kind);
        }

        var result = await query
            .OrderBy(party => party.Name)
            .ThenBy(party => party.Code)
            .ToPagedResultAsync(page, cancellationToken);

        return new PagedResult<PartyListItem>(
            result.Items.Select(ToItem).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    private static PartyListItem ToItem(Party party) => new(
        party.Id.Value,
        party.Code,
        PartyKinds.ToApi(party.Kind),
        party.Name,
        party.IsCustomer,
        party.IsSupplier,
        party.TaxNumber,
        party.City,
        party.Email,
        party.Phone);
}
