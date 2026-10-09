using System.Net;
using System.Net.Http.Json;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Finance.Features.Charges;
using Akiron.Modules.Finance.Features.Receipts;
using Akiron.Modules.Finance.Features.Statements;
using Akiron.Modules.Sales.Features.Quotes;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Finance;

public sealed class FinanceTests(ApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private async Task<(HttpClient Owner, Guid PartyId)> OwnerWithClientAsync()
    {
        var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        using var party = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "ABC Mobilya", isCustomer = true, isSupplier = false }, Cancel);
        return (owner, (await ReadAsync<PartyResponse>(party, HttpStatusCode.Created)).Id);
    }

    private static async Task<ChargeResponse> ChargeAsync(HttpClient client, object body) =>
        await ReadAsync<ChargeResponse>(await client.PostAsJsonAsync("/api/v1/finance/charges", body, Cancel), HttpStatusCode.Created);

    private static async Task<ReceiptResponse> ReceiveAsync(HttpClient client, object body) =>
        await ReadAsync<ReceiptResponse>(await client.PostAsJsonAsync("/api/v1/finance/receipts", body, Cancel), HttpStatusCode.Created);

    private static Task<StatementResponse?> StatementAsync(HttpClient client, Guid partyId) =>
        client.GetFromJsonAsync<StatementResponse>($"/api/v1/finance/parties/{partyId}/statement", Cancel);

    [Fact]
    public async Task Charge_WithAPaymentPlan_PostsTheDebitAndOpensEachInstalment()
    {
        var (owner, partyId) = await OwnerWithClientAsync();

        var charge = await ChargeAsync(owner, new
        {
            partyId,
            description = "Web sitesi",
            amount = 30_000m,
            date = "2026-09-01",
            instalments = new object[]
            {
                new { dueDate = "2026-09-01", amount = 10_000m },
                new { dueDate = "2026-10-01", amount = 10_000m },
                new { dueDate = "2027-11-01", amount = 10_000m },
            },
        });
        using var mismatch = await owner.PostAsJsonAsync("/api/v1/finance/charges", new
        {
            partyId,
            description = "Hatalı",
            amount = 100m,
            instalments = new object[] { new { dueDate = "2026-10-01", amount = 90m } },
        }, Cancel);
        var statement = await StatementAsync(owner, partyId);

        Assert.Equal($"BRC-2026-0001", charge.Number);
        Assert.Equal(3, charge.Instalments.Count);
        Assert.True(charge.Instalments[0].IsOverdue);
        Assert.False(charge.Instalments[2].IsOverdue);
        Assert.Equal("finance.charge.plan_mismatch", await mismatch.ReadErrorCodeAsync());
        var balance = Assert.Single(statement!.Balances);
        Assert.Equal((30_000m, 30_000m, 20_000m), (balance.Debit, balance.Balance, balance.Overdue));
        Assert.Equal(3, statement.OpenItems.Count);
    }

    [Fact]
    public async Task Receipt_AllocatesToTheOldestDueFirst_AndKeepsTheRestAsAnAdvance()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var charge = await ChargeAsync(owner, new
        {
            partyId,
            description = "Kampanya",
            amount = 20_000m,
            instalments = new object[] { new { dueDate = "2026-11-01", amount = 10_000m }, new { dueDate = "2026-10-01", amount = 10_000m } },
        });

        var receipt = await ReceiveAsync(owner, new { partyId, amount = 25_000m, method = "transfer", account = "Garanti TL", reference = "EFT-123" });
        var statement = await StatementAsync(owner, partyId);

        Assert.Equal($"TAH-{DateTime.UtcNow.Year}-0001", receipt.Number);
        Assert.Equal(5_000m, receipt.Unallocated);
        Assert.Equal([1, 2], receipt.Allocations.Select(allocation => allocation.Sequence));
        Assert.Equal(new DateOnly(2026, 10, 1), receipt.Allocations[0].DueDate);
        Assert.Empty(statement!.OpenItems);
        Assert.Equal(-5_000m, Assert.Single(statement.Balances).Balance);
        Assert.Equal(5_000m, Assert.Single(statement.Advances).Unallocated);
        Assert.Equal(charge.Number, statement.Lines[1].DocumentNumber);
    }

    [Fact]
    public async Task Advance_IsAllocatedLaterToANewCharge()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var advance = await ReceiveAsync(owner, new { partyId, amount = 7_500m, method = "cash" });
        var charge = await ChargeAsync(owner, new { partyId, description = "Çekim", amount = 10_000m });

        using var allocate = await owner.PostAsJsonAsync($"/api/v1/finance/receipts/{advance.Id}/allocate", new { autoAllocate = true }, Cancel);
        var after = await ReadAsync<ReceiptResponse>(allocate, HttpStatusCode.OK);
        var updated = await owner.GetFromJsonAsync<ChargeResponse>($"/api/v1/finance/charges/{charge.Id}", Cancel);

        Assert.Equal(0m, after.Unallocated);
        Assert.Equal(7_500m, updated!.Collected);
        Assert.Equal(2_500m, updated.Instalments[0].Remaining);
    }

    [Fact]
    public async Task ExplicitAllocation_IsCheckedAgainstWhatIsLeftAndTheCurrency()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var charge = await ChargeAsync(owner, new { partyId, description = "Logo", amount = 1_000m });
        var item = charge.Instalments[0].Id;

        using var tooMuch = await owner.PostAsJsonAsync("/api/v1/finance/receipts", new { partyId, amount = 2_000m, method = "card", allocations = new[] { new { openItemId = item, amount = 1_500m } } }, Cancel);
        using var otherCurrency = await owner.PostAsJsonAsync("/api/v1/finance/receipts", new { partyId, amount = 100m, method = "transfer", currency = "USD", exchangeRate = 41m, allocations = new[] { new { openItemId = item, amount = 100m } } }, Cancel);
        var exact = await ReceiveAsync(owner, new { partyId, amount = 400m, method = "transfer", allocations = new[] { new { openItemId = item, amount = 400m } } });

        Assert.Equal("finance.allocation.over_allocated", await tooMuch.ReadErrorCodeAsync());
        Assert.Equal("finance.allocation.currency_mismatch", await otherCurrency.ReadErrorCodeAsync());
        Assert.Equal(400m, Assert.Single(exact.Allocations).Amount);
    }

    [Fact]
    public async Task Reverse_UndoesTheReceiptReopensItemsAndPostsAMirrorLine()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var charge = await ChargeAsync(owner, new { partyId, description = "Bakım", amount = 5_000m });
        var receipt = await ReceiveAsync(owner, new { partyId, amount = 5_000m, method = "transfer" });

        using var cancelPaid = await owner.PostAsJsonAsync($"/api/v1/finance/charges/{charge.Id}/cancel", new { reason = "Yanlış" }, Cancel);
        using var reverse = await owner.PostAsJsonAsync($"/api/v1/finance/receipts/{receipt.Id}/reverse", new { reason = "Banka iade etti" }, Cancel);
        var reversed = await ReadAsync<ReceiptResponse>(reverse, HttpStatusCode.OK);
        using var twice = await owner.PostAsJsonAsync($"/api/v1/finance/receipts/{receipt.Id}/reverse", new { reason = "Tekrar" }, Cancel);
        var statement = await StatementAsync(owner, partyId);
        using var cancel = await owner.PostAsJsonAsync($"/api/v1/finance/charges/{charge.Id}/cancel", new { reason = "Müşteri vazgeçti" }, Cancel);
        var afterCancel = await StatementAsync(owner, partyId);

        Assert.Equal("finance.charge.not_cancellable", await cancelPaid.ReadErrorCodeAsync());
        Assert.Equal("reversed", reversed.Status);
        Assert.True(reversed.Allocations.All(allocation => allocation.IsReversed));
        Assert.Equal("finance.receipt.reversed", await twice.ReadErrorCodeAsync());
        Assert.Equal(5_000m, Assert.Single(statement!.Balances).Balance);
        Assert.Single(statement.OpenItems);
        Assert.True(statement.Lines[0].IsReversal);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(0m, Assert.Single(afterCancel!.Balances).Balance);
        Assert.Empty(afterCancel.OpenItems);
        Assert.Equal(4, afterCancel.Lines.Count);
    }

    [Fact]
    public async Task ChargeFromAnAcceptedQuote_UsesItsTotalOnceAndReachesTheTimeline()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        using var created = await owner.PostAsJsonAsync("/api/v1/sales/quotes", new
        {
            partyId,
            title = "Kimlik",
            lines = new object[] { new { name = "Logo", quantity = 1, unit = "project", unitPrice = 10_000m, vatRate = 20 } },
        }, Cancel);
        var quote = await ReadAsync<QuoteResponse>(created, HttpStatusCode.Created);
        using var draft = await owner.PostAsJsonAsync("/api/v1/finance/charges", new { quoteId = quote.Id }, Cancel);
        var sent = await ReadAsync<SendQuoteResponse>(await owner.PostAsJsonAsync($"/api/v1/sales/quotes/{quote.Id}/send", new { email = false }, Cancel), HttpStatusCode.OK);
        using var _ = await api.CreateClient().PostAsJsonAsync($"/api/v1/sales/public/quotes/{sent.Link.Split('/')[^1]}/accept", new { name = "Ayşe" }, Cancel);

        var charge = await ChargeAsync(owner, new
        {
            quoteId = quote.Id,
            instalments = new object[] { new { dueDate = "2026-10-15", amount = 6_000m }, new { dueDate = "2026-11-15", amount = 6_000m } },
        });
        using var again = await owner.PostAsJsonAsync("/api/v1/finance/charges", new { quoteId = quote.Id }, Cancel);
        await api.DeliverOutboxAsync();
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/party/{partyId}", Cancel);
        var toInvoice = await owner.GetFromJsonAsync<List<ChargeResponse>>("/api/v1/finance/charges/to-invoice", Cancel);
        using var invoiced = await owner.PostAsJsonAsync($"/api/v1/finance/charges/{charge.Id}/invoiced", new { invoiceNumber = "GIB2026000000123", invoiceDate = "2026-10-15" }, Cancel);
        var toInvoiceAfter = await owner.GetFromJsonAsync<List<ChargeResponse>>("/api/v1/finance/charges/to-invoice", Cancel);

        Assert.Equal("finance.charge.quote_not_accepted", await draft.ReadErrorCodeAsync());
        Assert.Equal(12_000m, charge.Amount);
        Assert.Equal(quote.Id, charge.QuoteId);
        Assert.Equal("finance.charge.quote_already_charged", await again.ReadErrorCodeAsync());
        Assert.Contains(timeline!.Items, item => item.Type == "finance.charge.recorded");
        Assert.Equal(charge.Id, Assert.Single(toInvoice!).Id);
        Assert.Equal(HttpStatusCode.OK, invoiced.StatusCode);
        Assert.Empty(toInvoiceAfter!);
    }

    [Fact]
    public async Task Summary_AndBalances_AddUpAcrossClients()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        using var other = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "Deniz Lojistik", isCustomer = true, isSupplier = false }, Cancel);
        var otherId = (await ReadAsync<PartyResponse>(other, HttpStatusCode.Created)).Id;
        await ChargeAsync(owner, new { partyId, description = "Eski borç", amount = 3_000m, kind = "opening", date = "2026-01-10" });
        await ChargeAsync(owner, new { partyId = otherId, description = "Yeni iş", amount = 8_000m, dueDate = "2099-01-01" });
        await ReceiveAsync(owner, new { partyId = otherId, amount = 1_000m, method = "cash" });

        var summary = await owner.GetFromJsonAsync<FinanceSummary>("/api/v1/finance/summary", Cancel);
        var balances = await owner.GetFromJsonAsync<List<PartyBalanceRow>>("/api/v1/finance/balances", Cancel);
        var overdue = await owner.GetFromJsonAsync<List<OpenItemRow>>("/api/v1/finance/open-items?overdue=true", Cancel);

        Assert.Equal(10_000m, Assert.Single(summary!.Receivable).Amount);
        Assert.Equal(3_000m, Assert.Single(summary.Overdue).Amount);
        Assert.Equal(1_000m, Assert.Single(summary.CollectedThisMonth).Amount);
        Assert.Equal(1, summary.ToInvoiceCount);
        Assert.Equal(["ABC Mobilya", "Deniz Lojistik"], balances!.Select(row => row.PartyName));
        Assert.Equal(7_000m, balances![1].Balance);
        Assert.Equal("Eski borç", Assert.Single(overdue!).Description);
    }

    [Fact]
    public async Task Finance_IsIsolatedAndClosedToMembers()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var charge = await ChargeAsync(owner, new { partyId, description = "Gizli", amount = 1_000m });
        var (intruder, _) = await OwnerWithClientAsync();
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var __ = await owner.InviteAsync(memberEmail);
        using var member = api.CreateClient();
        using var ___ = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));

        using var read = await intruder.GetAsync($"/api/v1/finance/charges/{charge.Id}", Cancel);
        using var pay = await intruder.PostAsJsonAsync("/api/v1/finance/receipts", new { partyId, amount = 1_000m, method = "cash" }, Cancel);
        var foreignStatement = await StatementAsync(intruder, partyId);
        using var memberRead = await member.GetAsync("/api/v1/finance/summary", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("finance.party_not_found", await pay.ReadErrorCodeAsync());
        Assert.Empty(foreignStatement!.Lines);
        Assert.Equal(HttpStatusCode.Forbidden, memberRead.StatusCode);
    }
}
