using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Contracts.Sales;
using Akiron.Modules.Sales.Features;
using Akiron.Modules.Sales.Features.Quotes;
using Akiron.Modules.Sales.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Sales;

public static class SalesPermissions
{
    public const string QuotesRead = "sales.quotes.read";
    public const string QuotesWrite = "sales.quotes.write";
    public const string CatalogManage = "sales.catalog.manage";

    public static IReadOnlyCollection<string> All { get; } = [QuotesRead, QuotesWrite, CatalogManage];
}

internal static class SalesErrors
{
    public static readonly Error CatalogItemNotFound = Error.NotFound("sales.catalog_item.not_found", "No such catalog item.");

    public static readonly Error QuoteNotFound = Error.NotFound("sales.quote.not_found", "No such quote in this organisation.");

    public static readonly Error PartyNotFound = Error.Validation("sales.quote.party_not_found", "The client does not exist in this organisation.");

    public static readonly Error NotEditable = Error.Rule("sales.quote.not_editable", "Only a draft can be changed; revise the quote first.");

    public static readonly Error NoLines = Error.Rule("sales.quote.no_lines", "A quote needs at least one line before it is sent.");

    public static readonly Error NotRevisable = Error.Rule("sales.quote.not_revisable", "Only sent or rejected quotes can be revised.");

    public static readonly Error OnlyDrafts = Error.Rule("sales.quote.only_drafts_deleted", "Only drafts can be deleted; a sent quote is part of the record.");

    public static readonly Error RateMissing = Error.Validation("sales.quote.rate_missing", "No TCMB rate for that currency and date; enter the rate yourself.");

    public static readonly Error LinkInvalid = Error.NotFound("sales.quote.link_invalid", "This link is invalid or was replaced by a newer revision.");

    public static readonly Error Expired = Error.Rule("sales.quote.expired", "The quote is past its validity date.");

    public static readonly Error AlreadyDecided = Error.Rule("sales.quote.already_decided", "The quote was already answered.");
}

/// <summary>Selling (Phase 3): the catalog of services and products, and quotes with a public approval link.</summary>
public sealed class SalesModule : IModule
{
    public string Name => SalesDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => SalesPermissions.All;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        services.AddModuleDbContext<SalesDbContext>(configuration, SalesDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(SalesModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<SalesModule>(includeInternalTypes: true);
        services.AddScoped<QuoteReader>();
        services.AddScoped<QuoteDetailsBuilder>();
        services.AddScoped<IAcceptedQuotes, AcceptedQuotes>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => SalesEndpoints.Map(endpoints);
}
