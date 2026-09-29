using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Akiron.Tests.Integration;

/// <summary>Answers TCMB bulletin requests (<c>yyyyMM/ddMMyyyy.xml</c>) with the same rates, dated as requested.</summary>
internal sealed partial class FakeTcmbHandler : HttpMessageHandler
{
    public const decimal UsdForexBuying = 41.3617m;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var match = BulletinPath().Match(request.RequestUri!.AbsolutePath);
        if (!match.Success)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        var date = $"{match.Groups["day"].Value}.{match.Groups["month"].Value}.{match.Groups["year"].Value}";
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Tarih_Date Tarih="{date}" Date="" Bulten_No="test">
              <Currency Kod="USD" CurrencyCode="USD"><Unit>1</Unit><ForexBuying>41.3617</ForexBuying><ForexSelling>41.4362</ForexSelling><BanknoteBuying/><BanknoteSelling/></Currency>
              <Currency Kod="EUR" CurrencyCode="EUR"><Unit>1</Unit><ForexBuying>48.2100</ForexBuying><ForexSelling>48.2968</ForexSelling><BanknoteBuying/><BanknoteSelling/></Currency>
            </Tarih_Date>
            """;

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml"),
        });
    }

    [GeneratedRegex(@"/(?<year>\d{4})(?<month>\d{2})/(?<day>\d{2})\d{2}\d{4}\.xml$")]
    private static partial Regex BulletinPath();
}
