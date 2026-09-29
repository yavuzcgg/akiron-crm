namespace Akiron.Modules.Reference.Domain;

/// <summary>
/// One currency's line in one TCMB bulletin, per one unit of the currency. Global reference data:
/// the same for every tenant, never edited after it is stored.
/// </summary>
public sealed class ExchangeRate
{
    private ExchangeRate(DateOnly bulletinDate, string currencyCode, decimal forexBuying, decimal? forexSelling, decimal? banknoteBuying, decimal? banknoteSelling, DateTimeOffset fetchedAt)
    {
        BulletinDate = bulletinDate;
        CurrencyCode = currencyCode;
        ForexBuying = forexBuying;
        ForexSelling = forexSelling;
        BanknoteBuying = banknoteBuying;
        BanknoteSelling = banknoteSelling;
        FetchedAt = fetchedAt;
    }

    public DateOnly BulletinDate { get; private set; }

    public string CurrencyCode { get; private set; }

    public decimal ForexBuying { get; private set; }

    public decimal? ForexSelling { get; private set; }

    public decimal? BanknoteBuying { get; private set; }

    public decimal? BanknoteSelling { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; }

    public static ExchangeRate Create(DateOnly bulletinDate, string currencyCode, decimal forexBuying, decimal? forexSelling, decimal? banknoteBuying, decimal? banknoteSelling, DateTimeOffset fetchedAt) =>
        new(bulletinDate, currencyCode, forexBuying, forexSelling, banknoteBuying, banknoteSelling, fetchedAt);
}
