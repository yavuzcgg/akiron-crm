using Akiron.BuildingBlocks.Domain;
using Akiron.Modules.Sales.Domain;

namespace Akiron.Tests.Unit;

public sealed class QuoteMathTests
{
    [Fact]
    public void Line_WithDiscountAndVat_RoundsOnceToKurus()
    {
        var line = QuoteMath.Line(quantity: 3m, unitPrice: 333.3333m, discountPercent: 10m, vatRate: 20, withholdingTenths: 0);

        Assert.Equal(1000.00m, line.Gross);
        Assert.Equal(900.00m, line.Net);
        Assert.Equal(100.00m, line.Discount);
        Assert.Equal(180.00m, line.Vat);
        Assert.Equal(1080.00m, line.Total);
    }

    [Fact]
    public void Line_WithAdvertisingWithholding_LeavesSevenTenthsOfTheVat()
    {
        // Ticari reklam hizmeti: 3/10 of the VAT is withheld by the buyer.
        var line = QuoteMath.Line(1m, 10_000m, 0m, 20, 3);

        Assert.Equal(2000.00m, line.Vat);
        Assert.Equal(600.00m, line.Withholding);
        Assert.Equal(11_400.00m, line.Total);
    }

    [Theory]
    [InlineData(120, 20, 100)]
    [InlineData(110, 10, 100)]
    [InlineData(99.99, 20, 83.325)]
    public void WithoutVat_TurnsAGrossPriceIntoTheNetOne(decimal gross, int rate, decimal expected) =>
        Assert.Equal(expected, QuoteMath.WithoutVat(gross, rate));

    [Fact]
    public void Quote_TotalsAreTheSumsOfRoundedLines_AndTryUsesTheRateSnapshot()
    {
        var quote = Quote.Draft("TKL-2026-0001", new QuoteDetails(
            Guid.NewGuid(), "ABC", "Web", null, null, Currency.Usd, 41.3617m, new DateOnly(2026, 9, 30),
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null,
            [
                new QuoteLineInput(null, "Tasarım", null, 1m, "project", 1000m, 0m, 20, 0),
                new QuoteLineInput(null, "Bakım", null, 12m, "month", 49.995m, 0m, 20, 0),
            ]));

        Assert.Equal(1599.94m, quote.NetTotal);
        Assert.Equal(319.99m, quote.VatTotal);
        Assert.Equal(1919.93m, quote.GrandTotal);
        Assert.Equal(66_176.24m, quote.NetTotalTry);
    }

    [Fact]
    public void Quote_OnceSent_IsFrozenUntilRevisedAndTheLinkChanges()
    {
        var quote = Quote.Draft("TKL-2026-0002", new QuoteDetails(
            Guid.NewGuid(), "ABC", "Logo", null, null, Currency.Try, null, null,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null,
            [new QuoteLineInput(null, "Logo", null, 1m, "project", 15_000m, 0m, 20, 0)]));

        var firstLink = quote.Send(DateTimeOffset.UtcNow);
        var firstHash = quote.LinkTokenHash;
        var editedWhileSent = quote.Edit(new QuoteDetails(quote.PartyId, "ABC", "X", null, null, Currency.Try, null, null, quote.IssueDate, quote.ValidUntil, null, []));
        var revised = quote.Revise();
        var secondLink = quote.Send(DateTimeOffset.UtcNow);

        Assert.NotNull(firstLink);
        Assert.False(editedWhileSent);
        Assert.True(revised);
        Assert.Equal(2, quote.Revision);
        Assert.NotEqual(firstHash, quote.LinkTokenHash);
        Assert.NotEqual(firstLink, secondLink);
    }
}
