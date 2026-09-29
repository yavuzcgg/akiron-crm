using System.Globalization;
using System.Xml.Linq;
using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Reference.Tcmb;

internal sealed record TcmbRate(string CurrencyCode, decimal ForexBuying, decimal? ForexSelling, decimal? BanknoteBuying, decimal? BanknoteSelling);

internal sealed record TcmbBulletin(DateOnly Date, IReadOnlyList<TcmbRate> Rates);

/// <summary>
/// Reads a TCMB daily bulletin (<c>kurlar/yyyyMM/ddMMyyyy.xml</c>). Rates are divided by the
/// quoted unit (JPY is per 100), currencies the product does not support are skipped, and lines
/// without a forex buying rate (SDR) are dropped.
/// </summary>
internal static class TcmbBulletinParser
{
    public static TcmbBulletin Parse(string xml)
    {
        var root = XDocument.Parse(xml).Root ?? throw new FormatException("The TCMB bulletin is empty.");
        var date = DateOnly.ParseExact(
            (string?)root.Attribute("Tarih") ?? throw new FormatException("The TCMB bulletin has no date."),
            "dd.MM.yyyy",
            CultureInfo.InvariantCulture);

        var rates = new List<TcmbRate>();
        foreach (var line in root.Elements("Currency"))
        {
            var code = (string?)line.Attribute("CurrencyCode") ?? (string?)line.Attribute("Kod");
            var unit = ParseDecimal(line.Element("Unit")) ?? 1m;
            var forexBuying = ParseDecimal(line.Element("ForexBuying"));

            if (!Currency.IsSupported(code) || forexBuying is not { } buying || unit <= 0)
            {
                continue;
            }

            rates.Add(new TcmbRate(
                code!,
                buying / unit,
                ParseDecimal(line.Element("ForexSelling")) / unit,
                ParseDecimal(line.Element("BanknoteBuying")) / unit,
                ParseDecimal(line.Element("BanknoteSelling")) / unit));
        }

        return new TcmbBulletin(date, rates);
    }

    private static decimal? ParseDecimal(XElement? element) =>
        decimal.TryParse(element?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
}
