using Akiron.BuildingBlocks.Domain;

namespace Akiron.Contracts.Reference;

/// <summary>
/// One TCMB rate, normalised to one unit of the currency (TCMB quotes JPY per 100, for example).
/// </summary>
/// <param name="BulletinDate">The TCMB bulletin the rate comes from; may be earlier than the date asked for (weekends, holidays).</param>
/// <param name="ForexBuying">"Döviz alış": the rate Turkish tax practice uses to convert foreign-currency invoices.</param>
public sealed record ExchangeRateQuote(
    Currency Currency,
    DateOnly BulletinDate,
    decimal ForexBuying,
    decimal? ForexSelling,
    decimal? BanknoteBuying,
    decimal? BanknoteSelling);

/// <summary>
/// Read side of the Reference module's exchange rates, for any module that converts money
/// (ADR-0004: the rate is looked up once and stored on the document).
/// </summary>
public interface IExchangeRates
{
    /// <summary>
    /// The rate from the latest TCMB bulletin on or before <paramref name="date"/>, or null when
    /// none is stored. Which date to ask for is the caller's rule (an invoice asks for the business
    /// day before its own date).
    /// </summary>
    Task<ExchangeRateQuote?> FindAsync(Currency currency, DateOnly date, CancellationToken cancellationToken);
}
