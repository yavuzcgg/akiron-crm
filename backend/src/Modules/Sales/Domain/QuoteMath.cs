namespace Akiron.Modules.Sales.Domain;

/// <summary>What one line comes to, every amount already rounded to kuruş.</summary>
public readonly record struct LineAmounts(decimal Gross, decimal Discount, decimal Net, decimal Vat, decimal Withholding, decimal Total);

/// <summary>
/// Quote arithmetic, the way e-invoice integrators do it (ADR-0004): each line is rounded once,
/// half away from zero, and document totals are the sums of the rounded lines, so the PDF, the
/// screen and the later invoice never disagree by a kuruş.
/// </summary>
public static class QuoteMath
{
    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <param name="withholdingTenths">KDV tevkifatı: the buyer pays this many tenths of the VAT to the tax office instead of to us.</param>
    public static LineAmounts Line(decimal quantity, decimal unitPrice, decimal discountPercent, int vatRate, int withholdingTenths)
    {
        var gross = Round(quantity * unitPrice);
        var net = Round(quantity * unitPrice * (100m - discountPercent) / 100m);
        var vat = Round(net * vatRate / 100m);
        var withholding = Round(vat * withholdingTenths / 10m);
        return new LineAmounts(gross, gross - net, net, vat, withholding, net + vat - withholding);
    }

    /// <summary>A VAT-inclusive price as the net price a line holds, to four decimals.</summary>
    public static decimal WithoutVat(decimal grossPrice, int vatRate) =>
        decimal.Round(grossPrice * 100m / (100m + vatRate), 4, MidpointRounding.AwayFromZero);
}
