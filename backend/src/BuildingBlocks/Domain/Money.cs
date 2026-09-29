using System.Globalization;

namespace Akiron.BuildingBlocks.Domain;

/// <summary>An ISO 4217 currency the product can hold money in: Turkish lira and the currencies TCMB publishes rates for.</summary>
public readonly record struct Currency
{
    public static readonly Currency Try = new("TRY");
    public static readonly Currency Usd = new("USD");
    public static readonly Currency Eur = new("EUR");

    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
    {
        "TRY", "USD", "EUR", "GBP", "CHF", "JPY", "CAD", "AUD", "DKK", "SEK", "NOK",
        "SAR", "AED", "QAR", "KWD", "RUB", "CNY", "AZN", "BGN", "RON", "KRW", "PKR",
    };

    private Currency(string code) => Code = code;

    public string Code { get; }

    public static bool IsSupported(string? code) => code is not null && Supported.Contains(code);

    public static Currency From(string code) =>
        IsSupported(code) ? new Currency(code) : throw new ArgumentException($"Currency '{code}' is not supported.", nameof(code));

    public override string ToString() => Code;
}

/// <summary>
/// An amount in a currency (ADR-0004). Negative amounts are allowed (balances, returns); more than
/// two decimal places are rejected, not rounded, because a silently dropped fraction surfaces
/// months later as a reconciliation difference. Arithmetic across currencies throws.
/// </summary>
public readonly record struct Money
{
    public const int DecimalPlaces = 2;

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public static Money Of(decimal amount, Currency currency)
    {
        if (decimal.Round(amount, DecimalPlaces) != amount)
        {
            throw new ArgumentException($"Amount {amount} has more than {DecimalPlaces} decimal places.", nameof(amount));
        }

        return new Money(amount, currency);
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    /// <summary>
    /// Rounds a computed amount (line total, tax, conversion) half away from zero, the rule the
    /// e-invoice integrators apply. Rounding happens once per line and once per total, never on
    /// intermediate steps.
    /// </summary>
    public static Money Round(decimal amount, Currency currency) =>
        new(decimal.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero), currency);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount, SameCurrency(left, right));

    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount, SameCurrency(left, right));

    public static Money operator -(Money value) => new(-value.Amount, value.Currency);

    public Money Add(Money other) => this + other;

    public Money Subtract(Money other) => this - other;

    public Money Negate() => -this;

    /// <summary>Converts with a rate quoted as "one unit of this currency in the target currency".</summary>
    public Money ConvertTo(Currency target, decimal rate)
    {
        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "An exchange rate must be positive.");
        }

        return target == Currency ? this : Round(Amount * rate, target);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Amount:F2} {Currency}");

    private static Currency SameCurrency(Money left, Money right) =>
        left.Currency == right.Currency
            ? left.Currency
            : throw new InvalidOperationException($"Cannot combine {left.Currency} with {right.Currency}; convert first.");
}
