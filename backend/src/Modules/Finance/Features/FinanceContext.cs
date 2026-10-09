using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Reference;

namespace Akiron.Modules.Finance.Features;

/// <summary>What every money handler needs: the client's name, today in Türkiye, the rate snapshot and the actor.</summary>
internal sealed class FinanceContext(IPartyDirectory parties, IExchangeRates exchangeRates, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public DateTimeOffset Now => timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Istanbul).DateTime);

    public Guid UserId => (currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.")).Value;

    public string? UserName => currentUser.DisplayName;

    public Task<PartySummary?> PartyAsync(Guid partyId, CancellationToken cancellationToken) => parties.FindAsync(partyId, cancellationToken);

    /// <summary>The typed rate, or TCMB döviz alış of the business day before <paramref name="date"/>; null when neither exists.</summary>
    public async Task<decimal?> RateAsync(Currency currency, decimal? typed, DateOnly date, CancellationToken cancellationToken)
    {
        if (currency == Currency.Try)
        {
            return 1m;
        }

        if (typed is { } rate)
        {
            return decimal.Round(rate, 6);
        }

        return (await exchangeRates.FindAsync(currency, date.AddDays(-1), cancellationToken))?.ForexBuying;
    }
}

internal static class FinanceApi
{
    public static readonly string[] Methods = ["transfer", "cash", "card", "check", "note", "other"];

    public static string Method(Domain.PaymentMethod method) => method.ToString().ToLowerInvariant();

    public static Domain.PaymentMethod ParseMethod(string value) => Enum.Parse<Domain.PaymentMethod>(value, ignoreCase: true);

    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
