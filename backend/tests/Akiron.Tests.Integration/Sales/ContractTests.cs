using System.Net;
using System.Net.Http.Json;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Finance.Features.Charges;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Sales.Features.Contracts;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Sales;

/// <summary>
/// Retainers bill themselves. Every test that runs the scheduler for a future day lives in this class,
/// so they run one after another and never bill another class's contracts behind its back.
/// </summary>
public sealed class ContractTests(ApiFixture api)
{
    private const string Contracts = "/api/v1/sales/contracts";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Today in Türkiye, as the API sees it.</summary>
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private async Task<(HttpClient Owner, Guid PartyId)> OwnerWithClientAsync()
    {
        var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        using var party = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "Mavi Kahve", isCustomer = true, isSupplier = false }, Cancel);
        return (owner, (await ReadAsync<PartyResponse>(party, HttpStatusCode.Created)).Id);
    }

    private static async Task<ContractResponse> CreateAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(Contracts, body, Cancel);
        return await ReadAsync<ContractResponse>(response, HttpStatusCode.Created);
    }

    private async Task<IReadOnlyList<ChargeResponse>> ChargesAsync(HttpClient client, Guid contractId)
    {
        await api.DeliverOutboxAsync();
        return (await client.GetFromJsonAsync<IReadOnlyList<ChargeResponse>>($"/api/v1/finance/charges?contractId={contractId}", Cancel))!;
    }

    private Task RunSchedulerAsync(DateOnly day) => api.RunJobAsync<ContractScheduler>(day);

    [Fact]
    public async Task Create_StartingTwoMonthsAgo_BillsEachStartedMonthOnceAndTheNextOnItsDay()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var start = Today.AddMonths(-2);

        var contract = await CreateAsync(owner, new { partyId, title = "Sosyal medya yönetimi", amount = 12_000m, startDate = start, paymentTermDays = 10, scope = "12 gönderi, 4 reels" });
        var billed = await ChargesAsync(owner, contract.Id);
        await RunSchedulerAsync(Today);
        var sameDay = await ChargesAsync(owner, contract.Id);
        await RunSchedulerAsync(start.AddMonths(3));
        await RunSchedulerAsync(start.AddMonths(3));
        var nextMonth = await ChargesAsync(owner, contract.Id);
        var statement = await owner.GetFromJsonAsync<Akiron.Modules.Finance.Features.Statements.StatementResponse>($"/api/v1/finance/parties/{partyId}/statement", Cancel);
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/contract/{contract.Id}", Cancel);

        Assert.Equal($"SZL-{start.Year}-0001", contract.Number);
        Assert.Equal("active", contract.Status);
        Assert.Equal(3, contract.BilledPeriods);
        Assert.Equal(start.AddMonths(3), contract.NextBillingDate);
        Assert.Equal(3, billed.Count);
        Assert.Equal(3, sameDay.Count);
        Assert.Equal(4, nextMonth.Count);
        var latest = nextMonth[0];
        Assert.Equal(start.AddMonths(3), latest.Period);
        Assert.Equal(start.AddMonths(3).AddDays(10), Assert.Single(latest.Instalments).DueDate);
        Assert.Equal(12_000m, latest.Amount);
        Assert.StartsWith(contract.Number, latest.Description, StringComparison.Ordinal);
        Assert.Equal(48_000m, Assert.Single(statement!.Balances).Debit);
        Assert.Contains(timeline!.Items, item => item.Type == "sales.contract.started");
        Assert.Contains(timeline.Items, item => item.Type == "finance.charge.recorded" && item.Actor.Kind == "system");
    }

    [Fact]
    public async Task Create_WithAFirstBillingDate_SkipsTheMonthsBilledOutsideAkiron()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var start = Today.AddMonths(-5);

        var contract = await CreateAsync(owner, new { partyId, title = "SEO", amount = 8_000m, startDate = start, firstBillingDate = start.AddMonths(5) });
        var charges = await ChargesAsync(owner, contract.Id);

        Assert.Equal(1, contract.BilledPeriods);
        Assert.Equal(start.AddMonths(5), Assert.Single(charges).Period);
    }

    [Fact]
    public async Task Pause_StopsBilling_AndResumeSkipsThePausedMonths()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var contract = await CreateAsync(owner, new { partyId, title = "Reklam yönetimi", amount = 5_000m, startDate = Today });

        using var pause = await owner.PostAsync(new Uri($"{Contracts}/{contract.Id}/pause", UriKind.Relative), null, Cancel);
        var paused = await ReadAsync<ContractResponse>(pause, HttpStatusCode.OK);
        await RunSchedulerAsync(Today.AddMonths(2));
        var whilePaused = await ChargesAsync(owner, contract.Id);
        using var pauseAgain = await owner.PostAsync(new Uri($"{Contracts}/{contract.Id}/pause", UriKind.Relative), null, Cancel);
        using var resume = await owner.PostAsync(new Uri($"{Contracts}/{contract.Id}/resume", UriKind.Relative), null, Cancel);
        var resumed = await ReadAsync<ContractResponse>(resume, HttpStatusCode.OK);

        Assert.Equal("paused", paused.Status);
        Assert.Null(paused.NextBillingDate);
        Assert.Single(whilePaused);
        Assert.Equal("sales.contract.not_active", await pauseAgain.ReadErrorCodeAsync());
        Assert.Equal("active", resumed.Status);
        Assert.Equal(Today.AddMonths(1), resumed.NextBillingDate);
    }

    [Fact]
    public async Task End_StopsBillingAndIsOnTheTimeline()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var contract = await CreateAsync(owner, new { partyId, title = "Web bakım", amount = 2_500m, startDate = Today });

        using var end = await owner.PostAsJsonAsync($"{Contracts}/{contract.Id}/end", new { reason = "Müşteri ajans değiştirdi" }, Cancel);
        var ended = await ReadAsync<ContractResponse>(end, HttpStatusCode.OK);
        await RunSchedulerAsync(Today.AddMonths(3));
        var charges = await ChargesAsync(owner, contract.Id);
        using var update = await owner.PutAsJsonAsync($"{Contracts}/{contract.Id}", new { title = "Web bakım", amount = 3_000m }, Cancel);
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/party/{partyId}", Cancel);

        Assert.Equal("ended", ended.Status);
        Assert.Equal(Today, ended.EndedOn);
        Assert.Single(charges);
        Assert.Equal("sales.contract.ended", await update.ReadErrorCodeAsync());
        Assert.Contains(timeline!.Items, item => item.Type == "sales.contract.ended" && item.Payload.GetProperty("reason").GetString() == "Müşteri ajans değiştirdi");
    }

    [Fact]
    public async Task EndDate_WithoutAutoRenew_EndsTheContractAndBillsNothingAfterIt()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var start = Today.AddMonths(-1);
        var contract = await CreateAsync(owner, new { partyId, title = "Lansman kampanyası", amount = 20_000m, startDate = start, endDate = start.AddMonths(2) });

        await RunSchedulerAsync(start.AddMonths(2));
        var after = await owner.GetFromJsonAsync<ContractResponse>($"{Contracts}/{contract.Id}", Cancel);
        var charges = await ChargesAsync(owner, contract.Id);

        Assert.Equal(2, charges.Count);
        Assert.Equal("ended", after!.Status);
        Assert.Equal(start.AddMonths(2), after.EndedOn);
    }

    [Fact]
    public async Task EndDate_WithAutoRenew_RemindsTheOwnerThenExtendsByTheTerm()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var start = Today.AddMonths(-11);
        var end = start.AddMonths(12);
        var contract = await CreateAsync(owner, new
        {
            partyId,
            title = "Yıllık retainer",
            amount = 15_000m,
            startDate = start,
            endDate = end,
            autoRenew = true,
            noticeDays = 45,
            firstBillingDate = Today.AddMonths(-1).AddDays(1),
        });

        await RunSchedulerAsync(Today);
        await RunSchedulerAsync(Today);
        await api.DeliverOutboxAsync();
        using var inbox = await owner.GetAsync("/api/v1/notifications", Cancel);
        var notices = System.Text.RegularExpressions.Regex.Count(await inbox.Content.ReadAsStringAsync(Cancel), "sales.contract.ending");
        await RunSchedulerAsync(end);
        var renewed = await owner.GetFromJsonAsync<ContractResponse>($"{Contracts}/{contract.Id}", Cancel);
        var charges = await ChargesAsync(owner, contract.Id);

        Assert.Equal(1, notices);
        Assert.Equal("active", renewed!.Status);
        Assert.Equal(end.AddMonths(12), renewed.EndDate);
        Assert.Contains(charges, charge => charge.Period == end);
    }

    [Fact]
    public async Task Contracts_AreForSalesManagers_AndStayInTheirTenant()
    {
        var (owner, partyId) = await OwnerWithClientAsync();
        var contract = await CreateAsync(owner, new { partyId, title = "Gizli", amount = 1_000m, startDate = Today.AddDays(5) });
        var email = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(email);
        var member = api.CreateClient();
        using var accepted = await member.AcceptInvitationAsync(api.InvitationTokenFor(email));
        var stranger = api.CreateClient();
        using var __ = await stranger.RegisterAsync();

        using var memberList = await member.GetAsync(Contracts, Cancel);
        using var strangerGet = await stranger.GetAsync($"{Contracts}/{contract.Id}", Cancel);
        using var strangerCreate = await stranger.PostAsJsonAsync(Contracts, new { partyId, title = "X", amount = 1m, startDate = Today }, Cancel);

        Assert.Equal(0, contract.BilledPeriods);
        Assert.Equal(HttpStatusCode.Forbidden, memberList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerGet.StatusCode);
        Assert.Equal("sales.contract.party_not_found", await strangerCreate.ReadErrorCodeAsync());
        Assert.NotNull(await accepted.Content.ReadFromJsonAsync<SessionResponse>(Cancel));
    }
}
