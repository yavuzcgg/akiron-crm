using Akiron.BuildingBlocks.Domain;

namespace Akiron.Tests.Unit;

public sealed class MoneyTests
{
    [Fact]
    public void Of_WithThreeDecimals_IsRejectedNotRounded() =>
        Assert.Throws<ArgumentException>(() => Money.Of(10.005m, Currency.Try));

    [Fact]
    public void Of_WithANegativeAmount_IsAllowedForBalancesAndReturns() =>
        Assert.Equal(-12.50m, Money.Of(-12.50m, Currency.Try).Amount);

    [Theory]
    [InlineData(2.345, 2.35)]
    [InlineData(-2.345, -2.35)]
    [InlineData(2.344, 2.34)]
    public void Round_GoesHalfAwayFromZero(decimal computed, decimal expected) =>
        Assert.Equal(expected, Money.Round(computed, Currency.Try).Amount);

    [Fact]
    public void Add_InDifferentCurrencies_Throws() =>
        Assert.Throws<InvalidOperationException>(() => Money.Of(1m, Currency.Try) + Money.Of(1m, Currency.Usd));

    [Fact]
    public void ConvertTo_RoundsTheResultOnce()
    {
        var invoice = Money.Of(1250.00m, Currency.Usd);

        var inLira = invoice.ConvertTo(Currency.Try, 41.3617m);

        Assert.Equal(Money.Of(51702.13m, Currency.Try), inLira);
    }

    [Fact]
    public void Currency_From_AnUnknownCode_Throws() =>
        Assert.Throws<ArgumentException>(() => Currency.From("XYZ"));
}
