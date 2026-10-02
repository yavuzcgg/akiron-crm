using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Jobs.Features;
using Akiron.Modules.Reference.Tcmb;
using Akiron.Modules.Sales.Features.Catalog;
using Akiron.Modules.Sales.Features.PublicQuotes;
using Akiron.Modules.Sales.Features.Quotes;
using Akiron.Modules.Timeline.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Tests.Integration.Sales;

public sealed class QuoteTests(ApiFixture api)
{
    private const string Quotes = "/api/v1/sales/quotes";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private async Task<(HttpClient Owner, PartyResponse Party)> OwnerWithClientAsync(string clientName = "ABC Mobilya")
    {
        var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        using var created = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = clientName, isCustomer = true, isSupplier = false }, Cancel);
        return (owner, await ReadAsync<PartyResponse>(created, HttpStatusCode.Created));
    }

    private static object LogoQuote(Guid partyId, string? email = null, string? issueDate = null, string? validUntil = null) => new
    {
        partyId,
        title = "Kurumsal kimlik",
        recipientName = "Ayşe Yılmaz",
        recipientEmail = email,
        issueDate,
        validUntil,
        notes = "Fiyatlara baskı dahil değildir.",
        lines = new object[]
        {
            new { name = "Logo tasarımı", quantity = 1, unit = "project", unitPrice = 18_000m, priceIncludesVat = true, vatRate = 20 },
            new { name = "Sosyal medya yönetimi", quantity = 3, unit = "month", unitPrice = 10_000m, discountPercent = 10m, vatRate = 20, withholdingTenths = 3 },
        },
    };

    private static async Task<QuoteResponse> CreateAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(Quotes, body, Cancel);
        return await ReadAsync<QuoteResponse>(response, HttpStatusCode.Created);
    }

    [Fact]
    public async Task Catalog_IsKeptByAdminsAndReadableByQuoteWriters()
    {
        var (owner, _) = await OwnerWithClientAsync();
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(memberEmail);
        using var member = api.CreateClient();
        using var __ = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));

        using var created = await owner.PostAsJsonAsync("/api/v1/sales/catalog", new
        {
            name = "Logo tasarımı",
            unit = "project",
            unitPrice = 18_000m,
            priceIncludesVat = true,
            vatRate = 20,
        }, Cancel);
        var item = await ReadAsync<CatalogItemResponse>(created, HttpStatusCode.Created);
        var list = await owner.GetFromJsonAsync<List<CatalogItemResponse>>("/api/v1/sales/catalog?search=logo", Cancel);
        using var memberReads = await member.GetAsync("/api/v1/sales/catalog", Cancel);
        using var badRate = await owner.PostAsJsonAsync("/api/v1/sales/catalog", new { name = "X", unit = "piece", unitPrice = 1m, vatRate = 18 }, Cancel);

        Assert.Equal(15_000m, item.NetUnitPrice);
        Assert.Equal(item.Id, Assert.Single(list!).Id);
        Assert.Equal(HttpStatusCode.Forbidden, memberReads.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badRate.StatusCode);
    }

    [Fact]
    public async Task Create_ComputesNetVatWithholdingAndTotals()
    {
        var (owner, party) = await OwnerWithClientAsync();

        var quote = await CreateAsync(owner, LogoQuote(party.Id));

        Assert.Equal($"TKL-{DateTimeOffset.UtcNow.Year}-0001", quote.Number);
        Assert.Equal("draft", quote.Status);
        Assert.Equal("ABC Mobilya", quote.PartyName);
        Assert.Equal(15_000m, quote.Lines[0].UnitPrice);
        Assert.Equal(27_000m, quote.Lines[1].Net);
        Assert.Equal(1_620m, quote.Lines[1].Withholding);
        Assert.Equal(new QuoteTotals(45_000m, 3_000m, 42_000m, 8_400m, 1_620m, 48_780m, 42_000m), quote.Totals);
    }

    [Fact]
    public async Task SendViewAccept_RunsThroughTheLinkAndReachesTheTimelineAndTheAuthor()
    {
        var (owner, party) = await OwnerWithClientAsync();
        var email = ApiClientExtensions.UniqueEmail();
        var quote = await CreateAsync(owner, LogoQuote(party.Id, email));

        using var sent = await owner.PostAsJsonAsync($"{Quotes}/{quote.Id}/send", new { email = true }, Cancel);
        var sendResult = await ReadAsync<SendQuoteResponse>(sent, HttpStatusCode.OK);
        var token = api.QuoteTokenFor(email);
        using var editSent = await owner.PutAsJsonAsync($"{Quotes}/{quote.Id}", LogoQuote(party.Id), Cancel);

        using var client = api.CreateClient();
        var view = await client.GetFromJsonAsync<PublicQuoteResponse>($"/api/v1/sales/public/quotes/{token}", Cancel);
        using var accepted = await client.PostAsJsonAsync($"/api/v1/sales/public/quotes/{token}/accept", new { name = "Ayşe Yılmaz", note = "Başlayalım" }, Cancel);
        using var again = await client.PostAsJsonAsync($"/api/v1/sales/public/quotes/{token}/reject", new { name = "Biri" }, Cancel);
        await api.DeliverOutboxAsync();

        var after = await owner.GetFromJsonAsync<QuoteResponse>($"{Quotes}/{quote.Id}", Cancel);
        var partyTimeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/party/{party.Id}", Cancel);
        var inbox = await owner.GetStringAsync("/api/v1/notifications", Cancel);

        Assert.True(sendResult.Emailed);
        Assert.EndsWith(token!, sendResult.Link, StringComparison.Ordinal);
        Assert.Equal("sales.quote.not_editable", await editSent.ReadErrorCodeAsync());
        Assert.Equal("sent", view!.Status);
        Assert.Equal(48_780m, view.Totals.Grand);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("sales.quote.already_decided", await again.ReadErrorCodeAsync());
        Assert.Equal("accepted", after!.Status);
        Assert.Equal("Ayşe Yılmaz", after.DecidedByName);
        Assert.NotNull(after.ViewedAt);
        Assert.Equal(["sales.quote.decided", "sales.quote.viewed", "sales.quote.sent"], partyTimeline!.Items.Take(3).Select(item => item.Type));
        Assert.Contains("sales.quote.viewed", inbox, StringComparison.Ordinal);
        Assert.Contains("sales.quote.decided", inbox, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revise_ClosesTheOldLinkAndTheNextSendOpensANewOne()
    {
        var (owner, party) = await OwnerWithClientAsync();
        var quote = await CreateAsync(owner, LogoQuote(party.Id));
        var first = await ReadAsync<SendQuoteResponse>(await owner.PostAsJsonAsync($"{Quotes}/{quote.Id}/send", new { email = false }, Cancel), HttpStatusCode.OK);
        var firstToken = first.Link.Split('/')[^1];

        var revised = await ReadAsync<QuoteResponse>(await owner.PostAsync(new Uri($"{Quotes}/{quote.Id}/revise", UriKind.Relative), null, Cancel), HttpStatusCode.OK);
        using var oldLink = await api.CreateClient().GetAsync($"/api/v1/sales/public/quotes/{firstToken}", Cancel);
        using var edited = await owner.PutAsJsonAsync($"{Quotes}/{quote.Id}", LogoQuote(party.Id), Cancel);
        var second = await ReadAsync<SendQuoteResponse>(await owner.PostAsJsonAsync($"{Quotes}/{quote.Id}/send", new { email = false }, Cancel), HttpStatusCode.OK);
        using var deleteSent = await owner.DeleteAsync($"{Quotes}/{quote.Id}", Cancel);

        Assert.Equal((2, "draft"), (revised.Revision, revised.Status));
        Assert.Equal("sales.quote.link_invalid", await oldLink.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.NotEqual(first.Link, second.Link);
        Assert.Equal(quote.Number, second.Quote.Number);
        Assert.Equal("sales.quote.only_drafts_deleted", await deleteSent.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Accept_AfterTheValidityDate_IsRefused()
    {
        var (owner, party) = await OwnerWithClientAsync();
        var quote = await CreateAsync(owner, LogoQuote(party.Id, issueDate: "2026-01-05", validUntil: "2026-01-20"));
        var sent = await ReadAsync<SendQuoteResponse>(await owner.PostAsJsonAsync($"{Quotes}/{quote.Id}/send", new { email = false }, Cancel), HttpStatusCode.OK);

        using var accept = await api.CreateClient().PostAsJsonAsync($"/api/v1/sales/public/quotes/{sent.Link.Split('/')[^1]}/accept", new { name = "Geç Kalan" }, Cancel);
        var list = await owner.GetFromJsonAsync<JsonElement>($"{Quotes}?status=expired", Cancel);

        Assert.Equal("sales.quote.expired", await accept.ReadErrorCodeAsync());
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ForeignCurrency_TakesTheTcmbRateOfThePreviousDay()
    {
        await api.Services.GetRequiredService<ExchangeRateSync>().SyncAsync(Cancel);
        var (owner, party) = await OwnerWithClientAsync();

        var quote = await CreateAsync(owner, new
        {
            partyId = party.Id,
            title = "Web sitesi",
            currency = "USD",
            lines = new object[] { new { name = "Web sitesi", quantity = 1, unit = "project", unitPrice = 2_000m, vatRate = 20 } },
        });

        Assert.Equal(FakeTcmbHandler.UsdForexBuying, quote.ExchangeRate);
        Assert.Equal(82_723.40m, quote.Totals.NetTry);
    }

    [Fact]
    public async Task WorkOrder_FromAnAcceptedQuote_TakesItsClientBudgetAndLines()
    {
        var (owner, party) = await OwnerWithClientAsync();
        var draft = await CreateAsync(owner, LogoQuote(party.Id));
        using var tooEarly = await owner.PostAsJsonAsync("/api/v1/jobs/work-orders", new { title = "Kimlik", quoteId = draft.Id }, Cancel);
        var sent = await ReadAsync<SendQuoteResponse>(await owner.PostAsJsonAsync($"{Quotes}/{draft.Id}/send", new { email = false }, Cancel), HttpStatusCode.OK);
        using var _ = await api.CreateClient().PostAsJsonAsync($"/api/v1/sales/public/quotes/{sent.Link.Split('/')[^1]}/accept", new { name = "Ayşe" }, Cancel);

        using var created = await owner.PostAsJsonAsync("/api/v1/jobs/work-orders", new { title = "Kurumsal kimlik", quoteId = draft.Id }, Cancel);
        var workOrder = await ReadAsync<WorkOrderResponse>(created, HttpStatusCode.Created);

        Assert.Equal("jobs.work_order.quote_not_accepted", await tooEarly.ReadErrorCodeAsync());
        Assert.Equal(party.Id, workOrder.PartyId);
        Assert.Equal(draft.Id, workOrder.SourceQuoteId);
        Assert.Equal(42_000m, workOrder.Financials!.Budget);
        Assert.Equal(["Logo tasarımı", "Sosyal medya yönetimi"], workOrder.Tasks.Select(task => task.Title));
    }

    [Fact]
    public async Task Pdf_IsAPdfForStaffAndForTheLink()
    {
        var (owner, party) = await OwnerWithClientAsync("IŞIK Tekstil Çağlayan Şubesi");
        var quote = await CreateAsync(owner, LogoQuote(party.Id));
        var sent = await ReadAsync<SendQuoteResponse>(await owner.PostAsJsonAsync($"{Quotes}/{quote.Id}/send", new { email = false }, Cancel), HttpStatusCode.OK);

        using var staff = await owner.GetAsync($"{Quotes}/{quote.Id}/pdf", Cancel);
        using var client = await api.CreateClient().GetAsync($"/api/v1/sales/public/quotes/{sent.Link.Split('/')[^1]}/pdf?lang=en", Cancel);
        var bytes = await staff.Content.ReadAsByteArrayAsync(Cancel);

        Assert.Equal("application/pdf", staff.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
        Assert.Equal(HttpStatusCode.OK, client.StatusCode);
    }

    [Fact]
    public async Task Quotes_AreIsolatedButTheLinkWorksForASignedInOutsider()
    {
        var (owner, party) = await OwnerWithClientAsync();
        var (intruder, _) = await OwnerWithClientAsync("Başka Ajans Müşterisi");
        var quote = await CreateAsync(owner, LogoQuote(party.Id));

        using var read = await intruder.GetAsync($"{Quotes}/{quote.Id}", Cancel);
        using var send = await intruder.PostAsJsonAsync($"{Quotes}/{quote.Id}/send", new { email = false }, Cancel);
        using var withForeignParty = await intruder.PostAsJsonAsync(Quotes, LogoQuote(party.Id), Cancel);
        var list = await intruder.GetFromJsonAsync<JsonElement>(Quotes, Cancel);

        var sent = await ReadAsync<SendQuoteResponse>(await owner.PostAsJsonAsync($"{Quotes}/{quote.Id}/send", new { email = false }, Cancel), HttpStatusCode.OK);
        using var outsiderView = await intruder.GetAsync($"/api/v1/sales/public/quotes/{sent.Link.Split('/')[^1]}", Cancel);
        using var nonsense = await intruder.GetAsync("/api/v1/sales/public/quotes/ABCDEF", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);
        Assert.Equal("sales.quote.party_not_found", await withForeignParty.ReadErrorCodeAsync());
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, outsiderView.StatusCode);
        Assert.Equal("sales.quote.link_invalid", await nonsense.ReadErrorCodeAsync());
    }
}
