using System.Net.Http.Json;
using Akiron.BuildingBlocks.Domain;
using Akiron.Contracts.Reference;
using Akiron.Modules.Reference.Features;
using Akiron.Modules.Reference.Tcmb;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Tests.Integration.Reference;

public sealed class ExchangeRateTests(ApiFixture api)
{
    [Fact]
    public async Task Sync_StoresWeekdayBulletins_AndIsIdempotent()
    {
        var sync = api.Services.GetRequiredService<ExchangeRateSync>();
        await sync.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, await sync.SyncAsync(TestContext.Current.CancellationToken));

        using var client = api.CreateClient();
        using var _ = await client.RegisterAsync();
        var bulletin = await client.GetFromJsonAsync<ExchangeRateBulletinResponse>("/api/v1/reference/exchange-rates", TestContext.Current.CancellationToken);

        Assert.NotNull(bulletin);
        Assert.DoesNotContain(bulletin.BulletinDate.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
        Assert.Equal(FakeTcmbHandler.UsdForexBuying, bulletin.Rates.Single(rate => rate.Currency == "USD").ForexBuying);
    }

    [Fact]
    public async Task FindAsync_OnASunday_UsesTheFridayBulletin()
    {
        await api.Services.GetRequiredService<ExchangeRateSync>().SyncAsync(TestContext.Current.CancellationToken);
        var lastSunday = LastSunday();

        await using var scope = api.CreateScope();
        var quote = await scope.ServiceProvider.GetRequiredService<IExchangeRates>()
            .FindAsync(Currency.Usd, lastSunday, TestContext.Current.CancellationToken);

        Assert.NotNull(quote);
        Assert.Equal(lastSunday.AddDays(-2), quote.BulletinDate);
    }

    [Fact]
    public async Task FindAsync_ForLira_IsOneWithoutAnyBulletin()
    {
        await using var scope = api.CreateScope();
        var quote = await scope.ServiceProvider.GetRequiredService<IExchangeRates>()
            .FindAsync(Currency.Try, new DateOnly(2001, 1, 1), TestContext.Current.CancellationToken);

        Assert.Equal(1m, quote!.ForexBuying);
    }

    private static DateOnly LastSunday()
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).DateTime);
        var daysBack = today.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)today.DayOfWeek;
        return today.AddDays(-daysBack);
    }
}
