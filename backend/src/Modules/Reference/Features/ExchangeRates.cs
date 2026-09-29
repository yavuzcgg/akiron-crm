using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Reference;
using Akiron.Modules.Reference.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Reference.Features;

/// <summary>Implements the cross-module contract (ADR-0001) over the stored bulletins.</summary>
internal sealed class ExchangeRateQuery(ReferenceDbContext db) : IExchangeRates
{
    public async Task<ExchangeRateQuote?> FindAsync(Currency currency, DateOnly date, CancellationToken cancellationToken)
    {
        if (currency == Currency.Try)
        {
            return new ExchangeRateQuote(currency, date, 1m, 1m, 1m, 1m);
        }

        var rate = await db.ExchangeRates
            .Where(candidate => candidate.CurrencyCode == currency.Code && candidate.BulletinDate <= date)
            .OrderByDescending(candidate => candidate.BulletinDate)
            .FirstOrDefaultAsync(cancellationToken);

        return rate is null
            ? null
            : new ExchangeRateQuote(currency, rate.BulletinDate, rate.ForexBuying, rate.ForexSelling, rate.BanknoteBuying, rate.BanknoteSelling);
    }
}

internal sealed record ExchangeRateResponse(string Currency, decimal ForexBuying, decimal? ForexSelling, decimal? BanknoteBuying, decimal? BanknoteSelling);

internal sealed record ExchangeRateBulletinResponse(DateOnly BulletinDate, IReadOnlyList<ExchangeRateResponse> Rates);

internal sealed class GetExchangeRatesHandler(ReferenceDbContext db)
{
    public static readonly Error NoBulletin =
        Error.NotFound("reference.exchange_rates.not_found", "No TCMB bulletin is stored for that date or before it.");

    public async Task<Result<ExchangeRateBulletinResponse>> HandleAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var bulletinDate = await db.ExchangeRates
            .Where(rate => rate.BulletinDate <= date)
            .MaxAsync(rate => (DateOnly?)rate.BulletinDate, cancellationToken);

        if (bulletinDate is not { } found)
        {
            return NoBulletin;
        }

        var rates = await db.ExchangeRates
            .Where(rate => rate.BulletinDate == found)
            .OrderBy(rate => rate.CurrencyCode)
            .Select(rate => new ExchangeRateResponse(rate.CurrencyCode, rate.ForexBuying, rate.ForexSelling, rate.BanknoteBuying, rate.BanknoteSelling))
            .ToListAsync(cancellationToken);

        return new ExchangeRateBulletinResponse(found, rates);
    }
}

internal static class ExchangeRateEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/exchange-rates", async (DateOnly? date, GetExchangeRatesHandler handler, TimeProvider timeProvider, CancellationToken cancellationToken) =>
            {
                var asOf = date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(3)).DateTime);
                var result = await handler.HandleAsync(asOf, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            // Public reference data, the same for every tenant: any signed-in user may read it.
            .RequireAuthorization()
            .Produces<ExchangeRateBulletinResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("TCMB exchange rates of the latest bulletin on or before a date");
}
