using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Modules.Finance.Features;
using Akiron.Modules.Finance.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Finance;

public static class FinancePermissions
{
    /// <summary>Balances, statements, collections: the agency's money.</summary>
    public const string Read = "finance.read";

    /// <summary>Recording charges and collections, allocating, reversing.</summary>
    public const string Write = "finance.write";

    public static IReadOnlyCollection<string> All { get; } = [Read, Write];
}

internal static class FinanceErrors
{
    public static readonly Error PartyNotFound = Error.Validation("finance.party_not_found", "The party does not exist in this organisation.");

    public static readonly Error QuoteNotAccepted = Error.Rule("finance.charge.quote_not_accepted", "Only an accepted quote can be charged.");

    public static readonly Error QuoteAlreadyCharged = Error.Conflict("finance.charge.quote_already_charged", "This quote was already charged.");

    public static readonly Error ChargeNotFound = Error.NotFound("finance.charge.not_found", "No such charge.");

    public static readonly Error ChargeNotCancellable = Error.Rule("finance.charge.not_cancellable", "Reverse the collections allocated to this charge before cancelling it.");

    public static readonly Error ReceiptNotFound = Error.NotFound("finance.receipt.not_found", "No such collection.");

    public static readonly Error ReceiptReversed = Error.Rule("finance.receipt.reversed", "The collection was reversed.");

    public static readonly Error OpenItemNotFound = Error.Validation("finance.allocation.item_not_found", "An item to allocate to was not found or is closed.");

    public static readonly Error CurrencyMismatch = Error.Validation("finance.allocation.currency_mismatch", "Allocate only to items in the collection's currency.");

    public static readonly Error OverAllocated = Error.Validation("finance.allocation.over_allocated", "The allocations exceed the collection or the items' remaining amounts.");

    public static readonly Error RateMissing = Error.Validation("finance.rate_missing", "No TCMB rate for that currency and date; enter the rate yourself.");
}

/// <summary>Finance Lite (Phase 3): the party ledger, charges with payment plans, collections and their allocation (ADR-0004).</summary>
public sealed class FinanceModule : IModule
{
    public string Name => FinanceDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => FinancePermissions.All;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<FinanceDbContext>(configuration, FinanceDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(FinanceModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<FinanceModule>(includeInternalTypes: true);
        services.AddScoped<FinanceContext>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => FinanceEndpoints.Map(endpoints);
}
