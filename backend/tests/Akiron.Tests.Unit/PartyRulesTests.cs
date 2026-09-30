using Akiron.Modules.Crm.Domain;

namespace Akiron.Tests.Unit;

public sealed class PartyRulesTests
{
    [Theory]
    [InlineData("1234567890")]
    [InlineData("4567890128")]
    public void IsValidVkn_WithCorrectCheckDigit_ReturnsTrue(string vkn) => Assert.True(TaxNumber.IsValidVkn(vkn));

    [Theory]
    [InlineData("1234567891")]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    [InlineData("12345A7890")]
    [InlineData("")]
    public void IsValidVkn_WithTypoOrWrongShape_ReturnsFalse(string vkn) => Assert.False(TaxNumber.IsValidVkn(vkn));

    [Fact]
    public void IsValidTckn_WithCorrectCheckDigits_ReturnsTrue() => Assert.True(TaxNumber.IsValidTckn("10000000146"));

    [Theory]
    [InlineData("10000000147")]
    [InlineData("10000000156")]
    [InlineData("00000000146")]
    [InlineData("1000000014")]
    public void IsValidTckn_WithTypoOrLeadingZero_ReturnsFalse(string tckn) => Assert.False(TaxNumber.IsValidTckn(tckn));

    [Fact]
    public void IsValidFor_ACompany_AcceptsOnlyAVkn()
    {
        Assert.True(TaxNumber.IsValidFor(PartyKind.Company, "1234567890"));
        Assert.False(TaxNumber.IsValidFor(PartyKind.Company, "10000000146"));
    }

    [Fact]
    public void IsValidFor_APerson_AcceptsATcknOrASoleTradersVkn()
    {
        Assert.True(TaxNumber.IsValidFor(PartyKind.Person, "10000000146"));
        Assert.True(TaxNumber.IsValidFor(PartyKind.Person, "1234567890"));
    }

    [Theory]
    [InlineData("IŞIK Tekstil", "isik tekstil")]
    [InlineData("Işık", "isik")]
    [InlineData("İSTANBUL Çiçekçilik", "istanbul cicekcilik")]
    [InlineData("  Göğüş Ürün  ", "gogus urun")]
    [InlineData(null, "")]
    public void Fold_WithTurkishLetters_GivesPlainLowercase(string? input, string expected) =>
        Assert.Equal(expected, TurkishText.Fold(input));

    [Fact]
    public void Update_WithSameValues_ReportsNoChanges()
    {
        var details = new PartyDetails(PartyKind.Company, "ABC Mobilya", true, false, "1234567890", "Kadıköy", null, null, null, "İstanbul", null, null);
        var party = Party.Create("C00001", details);

        Assert.Empty(party.Update(details));
        Assert.Equal(["name", "city"], party.Update(details with { Name = "ABC Mobilya A.Ş.", City = "Ankara" }));
    }
}
