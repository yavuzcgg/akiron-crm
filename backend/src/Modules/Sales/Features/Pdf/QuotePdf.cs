using System.Globalization;
using Akiron.Modules.Sales.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Akiron.Modules.Sales.Features.Pdf;

/// <summary>
/// The quote as a PDF (ADR-0010: QuestPDF). Turkish by default, English on request; amounts use
/// the document's currency with Turkish number formatting, as the client receives them.
/// </summary>
internal static class QuotePdf
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] Columns = ["item", "qty", "price", "discount", "vat", "amount"];

    private static readonly Dictionary<string, (string Tr, string En)> Labels = new(StringComparer.Ordinal)
    {
        ["title"] = ("TEKLİF", "QUOTE"),
        ["to"] = ("Sayın", "To"),
        ["date"] = ("Tarih", "Date"),
        ["valid"] = ("Geçerlilik", "Valid until"),
        ["revision"] = ("Revizyon", "Revision"),
        ["item"] = ("Hizmet / ürün", "Item"),
        ["qty"] = ("Miktar", "Qty"),
        ["price"] = ("Birim fiyat", "Unit price"),
        ["discount"] = ("İnd.", "Disc."),
        ["vat"] = ("KDV", "VAT"),
        ["amount"] = ("Tutar", "Amount"),
        ["gross"] = ("Ara toplam", "Subtotal"),
        ["discountTotal"] = ("İndirim", "Discount"),
        ["net"] = ("KDV hariç toplam", "Total excl. VAT"),
        ["vatTotal"] = ("KDV", "VAT"),
        ["withholding"] = ("KDV tevkifatı", "VAT withholding"),
        ["grand"] = ("Genel toplam", "Total"),
        ["rate"] = ("Kur", "Exchange rate"),
        ["notes"] = ("Notlar ve koşullar", "Notes and terms"),
        ["footer"] = ("Bu teklif bir fatura değildir.", "This quote is not an invoice."),
    };

    private static readonly Dictionary<string, (string Tr, string En)> UnitNames = new(StringComparer.Ordinal)
    {
        ["piece"] = ("adet", "pcs"),
        ["hour"] = ("saat", "h"),
        ["day"] = ("gün", "day"),
        ["month"] = ("ay", "month"),
        ["year"] = ("yıl", "year"),
        ["project"] = ("proje", "project"),
        ["package"] = ("paket", "package"),
        ["post"] = ("gönderi", "post"),
        ["page"] = ("sayfa", "page"),
        ["word"] = ("kelime", "word"),
    };

    public static byte[] Render(Quote quote, string workspaceName, string language = "tr")
    {
        var english = language == "en";
        string L(string key) => english ? Labels[key].En : Labels[key].Tr;
        string Unit(string key) => UnitNames.TryGetValue(key, out var name) ? english ? name.En : name.Tr : key;
        string Money(decimal amount) => $"{amount.ToString("N2", Turkish)} {quote.Currency.Code}";
        string Number(decimal value) => value.ToString("0.####", Turkish);

        var primary = Color.FromHex("#4F46E5");
        var muted = Color.FromHex("#64748B");

        return Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(9.5f).FontColor(Color.FromHex("#0F172A")));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(workspaceName).FontSize(16).SemiBold();
                            left.Item().PaddingTop(16).Text(L("to")).FontColor(muted);
                            left.Item().Text(quote.PartyName).SemiBold();
                            if (quote.RecipientName is not null)
                            {
                                left.Item().Text(quote.RecipientName);
                            }
                        });
                        row.ConstantItem(200).AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text(L("title")).FontSize(20).Bold().FontColor(primary);
                            right.Item().AlignRight().Text(quote.Number).SemiBold();
                            right.Item().PaddingTop(8).AlignRight().Text($"{L("date")}: {quote.IssueDate.ToString("dd.MM.yyyy", Turkish)}");
                            right.Item().AlignRight().Text($"{L("valid")}: {quote.ValidUntil.ToString("dd.MM.yyyy", Turkish)}");
                            if (quote.Revision > 1)
                            {
                                right.Item().AlignRight().Text($"{L("revision")}: {quote.Revision}").FontColor(muted);
                            }
                        });
                    });
                    header.Item().PaddingTop(18).Text(quote.Title).FontSize(13).SemiBold();
                });

                page.Content().PaddingTop(12).Column(content =>
                {
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2.2f);
                        });

                        table.Header(head =>
                        {
                            foreach (var key in Columns)
                            {
                                var cell = head.Cell().Background(Color.FromHex("#F1F5F9")).PaddingVertical(6).PaddingHorizontal(4);
                                (key == "item" ? cell : cell.AlignRight()).Text(L(key)).FontSize(8.5f).SemiBold().FontColor(muted);
                            }
                        });

                        foreach (var line in quote.Lines.OrderBy(line => line.Position))
                        {
                            IContainer Cell() => table.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex("#E2E8F0")).PaddingVertical(6).PaddingHorizontal(4);

                            Cell().Column(item =>
                            {
                                item.Item().Text(line.Name).SemiBold();
                                if (line.Description is not null)
                                {
                                    item.Item().Text(line.Description).FontSize(8.5f).FontColor(muted);
                                }
                            });
                            Cell().AlignRight().Text($"{Number(line.Quantity)} {Unit(line.Unit)}");
                            Cell().AlignRight().Text(Money(line.UnitPrice));
                            Cell().AlignRight().Text(line.DiscountPercent == 0 ? "—" : $"%{Number(line.DiscountPercent)}");
                            Cell().AlignRight().Text($"%{line.VatRate}");
                            Cell().AlignRight().Text(Money(line.Net));
                        }
                    });

                    content.Item().PaddingTop(12).AlignRight().Width(240).Column(totals =>
                    {
                        void Row(string label, decimal amount, bool strong = false)
                        {
                            totals.Item().PaddingVertical(2).Row(row =>
                            {
                                var left = row.RelativeItem().Text(label);
                                var right = row.RelativeItem().AlignRight().Text(Money(amount));
                                if (strong)
                                {
                                    left.SemiBold().FontSize(11);
                                    right.SemiBold().FontSize(11);
                                }
                            });
                        }

                        if (quote.DiscountTotal > 0)
                        {
                            Row(L("gross"), quote.GrossTotal);
                            Row(L("discountTotal"), -quote.DiscountTotal);
                        }

                        Row(L("net"), quote.NetTotal);
                        Row(L("vatTotal"), quote.VatTotal);
                        if (quote.WithholdingTotal > 0)
                        {
                            Row(L("withholding"), -quote.WithholdingTotal);
                        }

                        totals.Item().PaddingTop(4).BorderTop(1).BorderColor(primary).PaddingTop(4).Element(_ => { });
                        Row(L("grand"), quote.GrandTotal, strong: true);
                        if (quote.ExchangeRate is { } rate)
                        {
                            totals.Item().PaddingTop(4).AlignRight()
                                .Text($"{L("rate")}: 1 {quote.Currency.Code} = {rate.ToString("N4", Turkish)} TRY ({quote.RateDate?.ToString("dd.MM.yyyy", Turkish)})")
                                .FontSize(8).FontColor(muted);
                        }
                    });

                    if (quote.Notes is not null)
                    {
                        content.Item().PaddingTop(20).Text(L("notes")).SemiBold();
                        content.Item().PaddingTop(4).Text(quote.Notes).FontColor(Color.FromHex("#334155"));
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text(L("footer")).FontSize(8).FontColor(muted);
                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor(muted));
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            }))
            .GeneratePdf();
    }
}
