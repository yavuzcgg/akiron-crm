using Akiron.Modules.Reference.Tcmb;

namespace Akiron.Tests.Unit;

public sealed class TcmbBulletinParserTests
{
    /// <summary>Trimmed from a real bulletin; the shape TCMB has published for years.</summary>
    internal const string Sample = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Tarih_Date Tarih="26.09.2026" Date="09/26/2026" Bulten_No="2026/184">
          <Currency CrossOrder="0" Kod="USD" CurrencyCode="USD">
            <Unit>1</Unit><Isim>ABD DOLARI</Isim><CurrencyName>US DOLLAR</CurrencyName>
            <ForexBuying>41.3617</ForexBuying><ForexSelling>41.4362</ForexSelling>
            <BanknoteBuying>41.3327</BanknoteBuying><BanknoteSelling>41.4984</BanknoteSelling>
            <CrossRateUSD/><CrossRateOther/>
          </Currency>
          <Currency CrossOrder="1" Kod="JPY" CurrencyCode="JPY">
            <Unit>100</Unit><Isim>JAPON YENİ</Isim><CurrencyName>JAPENESE YEN</CurrencyName>
            <ForexBuying>27.6510</ForexBuying><ForexSelling>27.8340</ForexSelling>
            <BanknoteBuying>27.5310</BanknoteBuying><BanknoteSelling>27.9380</BanknoteSelling>
            <CrossRateUSD>149.58</CrossRateUSD><CrossRateOther/>
          </Currency>
          <Currency CrossOrder="18" Kod="XDR" CurrencyCode="XDR">
            <Unit>1</Unit><Isim>ÖZEL ÇEKME HAKKI (SDR)</Isim><CurrencyName>SPECIAL DRAWING RIGHT (SDR)</CurrencyName>
            <ForexBuying>56.1234</ForexBuying><ForexSelling/><BanknoteBuying/><BanknoteSelling/>
            <CrossRateUSD/><CrossRateOther>1.35</CrossRateOther>
          </Currency>
        </Tarih_Date>
        """;

    [Fact]
    public void Parse_ReadsTheBulletinDate() =>
        Assert.Equal(new DateOnly(2026, 9, 26), TcmbBulletinParser.Parse(Sample).Date);

    [Fact]
    public void Parse_KeepsRatesAsPublished()
    {
        var usd = TcmbBulletinParser.Parse(Sample).Rates.Single(rate => rate.CurrencyCode == "USD");

        Assert.Equal(41.3617m, usd.ForexBuying);
        Assert.Equal(41.4984m, usd.BanknoteSelling);
    }

    [Fact]
    public void Parse_WithAUnitOfOneHundred_NormalisesToOneUnit() =>
        Assert.Equal(0.276510m, TcmbBulletinParser.Parse(Sample).Rates.Single(rate => rate.CurrencyCode == "JPY").ForexBuying);

    [Fact]
    public void Parse_SkipsCurrenciesTheProductDoesNotHold() =>
        Assert.DoesNotContain(TcmbBulletinParser.Parse(Sample).Rates, rate => rate.CurrencyCode == "XDR");
}
