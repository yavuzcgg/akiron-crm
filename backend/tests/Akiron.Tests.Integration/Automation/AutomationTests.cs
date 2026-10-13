using System.Net;
using System.Net.Http.Json;
using Akiron.Modules.Automation.Features;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Automation;

/// <summary>Rules: when a business event happens, write to the client or tell the team, once per event.</summary>
public sealed class AutomationTests(ApiFixture api)
{
    private const string Rules = "/api/v1/automation/rules";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private async Task<(HttpClient Owner, SessionResponse Session)> OwnerAsync()
    {
        var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync(organizationName: "Akiron Ajans");
        return (owner, (await registered.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!);
    }

    private static async Task<Guid> PartyAsync(HttpClient owner, string name, string? email = null, string? phone = null)
    {
        using var party = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name, isCustomer = true, isSupplier = false }, Cancel);
        var id = (await ReadAsync<PartyResponse>(party, HttpStatusCode.Created)).Id;
        if (email is not null || phone is not null)
        {
            using var contact = await owner.PostAsJsonAsync($"/api/v1/crm/parties/{id}/contacts", new { fullName = "Oya Kaplan", email, phone, isPrimary = true }, Cancel);
            Assert.Equal(HttpStatusCode.Created, contact.StatusCode);
        }

        return id;
    }

    private static async Task<RuleResponse> RuleAsync(HttpClient owner, object body)
    {
        using var response = await owner.PostAsJsonAsync(Rules, body, Cancel);
        return await ReadAsync<RuleResponse>(response, HttpStatusCode.Created);
    }

    private static async Task<IReadOnlyList<RunResponse>> RunsAsync(HttpClient owner, Guid ruleId) =>
        (await owner.GetFromJsonAsync<IReadOnlyList<RunResponse>>($"{Rules}/{ruleId}/runs", Cancel))!;

    [Fact]
    public async Task QuoteAccepted_EmailsTheClientOnce_WithItsVariablesFilled()
    {
        var (owner, _) = await OwnerAsync();
        var email = ApiClientExtensions.UniqueEmail();
        var partyId = await PartyAsync(owner, "Mavi Kahve", email);
        var rule = await RuleAsync(owner, new
        {
            name = "Teklif teşekkürü",
            trigger = "quote.accepted",
            action = "email_client",
            subject = "{{number}} onaylandı, teşekkürler",
            body = "Merhaba {{person}},\n{{party}} için {{title}} ({{amount}}) teklifini onayladınız. — {{workspace}}",
        });
        var disabled = await RuleAsync(owner, new { name = "Kapalı", trigger = "quote.accepted", action = "notify_team", body = "x", enabled = false });
        using var created = await owner.PostAsJsonAsync("/api/v1/sales/quotes", new
        {
            partyId,
            title = "Kurumsal kimlik",
            recipientEmail = "satin.alma@mavikahve.example",
            lines = new[] { new { name = "Logo", quantity = 1m, unitPrice = 10_000m, unit = "project", discountPercent = 0m, vatRate = 20, withholdingTenths = 0, priceIncludesVat = false } },
        }, Cancel);
        var quoteId = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Cancel)).GetProperty("id").GetGuid();
        using var send = await owner.PostAsJsonAsync($"/api/v1/sales/quotes/{quoteId}/send", new { email = true }, Cancel);

        using var accept = await api.CreateClient().PostAsJsonAsync($"/api/v1/sales/public/quotes/{api.QuoteTokenFor("satin.alma@mavikahve.example")}/accept", new { name = "Can Demir" }, Cancel);
        await api.DeliverOutboxAsync();
        await api.DeliverOutboxAsync();
        var runs = await RunsAsync(owner, rule.Id);
        var rules = await owner.GetFromJsonAsync<IReadOnlyList<RuleResponse>>(Rules, Cancel);
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/party/{partyId}", Cancel);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (send.StatusCode, accept.StatusCode));
        var message = Assert.Single(api.EmailsTo(email));
        Assert.Equal("TKL-" + DateTime.UtcNow.Year + "-0001 onaylandı, teşekkürler", message.Subject);
        Assert.Equal("Merhaba Can Demir,\nMavi Kahve için Kurumsal kimlik (12.000,00 TRY) teklifini onayladınız. — Akiron Ajans", message.TextBody);
        Assert.Equal(("done", email), (Assert.Single(runs).Outcome, runs[0].Detail));
        Assert.Equal(1, rules!.Single(item => item.Id == rule.Id).RunCount);
        Assert.Empty(await RunsAsync(owner, disabled.Id));
        Assert.Contains(timeline!.Items, item => item.Type == "automation.message.sent" && item.Payload.GetProperty("ruleName").GetString() == "Teklif teşekkürü");
    }

    [Fact]
    public async Task ReceiptRecorded_TextsTheClientsMobile_OrSaysWhyItCouldNot()
    {
        var (owner, _) = await OwnerAsync();
        var mobile = $"0532 {Random.Shared.Next(100, 999)} {Random.Shared.Next(10, 99)} {Random.Shared.Next(10, 99)}";
        var normalized = "+90" + new string(mobile.Where(char.IsDigit).Skip(1).ToArray());
        var withMobile = await PartyAsync(owner, "Mavi Kahve", phone: mobile);
        var withLandLine = await PartyAsync(owner, "Işık Tekstil", phone: "0224 123 45 67");
        var rule = await RuleAsync(owner, new { name = "Tahsilat teşekkürü", trigger = "receipt.recorded", action = "sms_client", body = "{{party}}: {{amount}} ödemeniz alındı. Teşekkürler." });

        using var first = await owner.PostAsJsonAsync("/api/v1/finance/receipts", new { partyId = withMobile, amount = 2_500m, method = "transfer" }, Cancel);
        using var second = await owner.PostAsJsonAsync("/api/v1/finance/receipts", new { partyId = withLandLine, amount = 900m, method = "cash" }, Cancel);
        await api.DeliverOutboxAsync();
        var runs = await RunsAsync(owner, rule.Id);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (first.StatusCode, second.StatusCode));
        Assert.Equal("Mavi Kahve: 2.500,00 TRY ödemeniz alındı. Teşekkürler.", Assert.Single(api.TextsTo(normalized)).Text);
        Assert.Equal(2, runs.Count);
        Assert.Contains(runs, run => run is { Outcome: "done" } && run.Detail == normalized);
        Assert.Contains(runs, run => run is { Outcome: "skipped", Detail: "no_mobile" });
    }

    [Fact]
    public async Task Notices_ReachOnePersonOrTheWholeTeam()
    {
        var (owner, _) = await OwnerAsync();
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(memberEmail);
        var member = api.CreateClient();
        using var accepted = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));
        var memberId = (await accepted.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!.UserId;
        var toMember = await RuleAsync(owner, new { name = "Yeni fırsat", trigger = "lead.created", action = "notify_user", target = memberId.ToString(), body = "Yeni fırsat: {{title}} ({{company}})" });
        await RuleAsync(owner, new { name = "Herkese", trigger = "lead.created", action = "notify_team", body = "Huniye {{title}} düştü" });

        using var lead = await owner.PostAsJsonAsync("/api/v1/crm/leads", new { title = "Web sitesi", companyName = "Deniz Otel", source = "referral" }, Cancel);
        await api.DeliverOutboxAsync();
        await api.DeliverOutboxAsync();
        using var memberInbox = await member.GetAsync("/api/v1/notifications", Cancel);
        using var ownerInbox = await owner.GetAsync("/api/v1/notifications", Cancel);
        var memberBody = await memberInbox.Content.ReadAsStringAsync(Cancel);
        var ownerBody = await ownerInbox.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.Created, lead.StatusCode);
        Assert.Equal("Ayşe Yılmaz", toMember.TargetName);
        Assert.Contains("Yeni fırsat: Web sitesi (Deniz Otel)", memberBody, StringComparison.Ordinal);
        Assert.Contains("Huniye Web sitesi düştü", memberBody, StringComparison.Ordinal);
        Assert.Contains("Huniye Web sitesi düştü", ownerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Yeni fırsat: Web sitesi", ownerBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rules_AreCheckedAndBelongToThoseWhoMayManageThem()
    {
        var (owner, _) = await OwnerAsync();
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(memberEmail);
        var member = api.CreateClient();
        using var accepted = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));
        var (stranger, strangerSession) = await OwnerAsync();
        var rule = await RuleAsync(owner, new { name = "Muhasebeciye", trigger = "quote.accepted", action = "email_address", target = "muhasebe@example.com", subject = "Yeni iş", body = "{{party}} {{amount}}" });

        using var noClient = await owner.PostAsJsonAsync(Rules, new { name = "Olmaz", trigger = "lead.created", action = "email_client", subject = "x", body = "x" }, Cancel);
        using var notMember = await owner.PostAsJsonAsync(Rules, new { name = "Yabancı", trigger = "ticket.opened", action = "notify_user", target = strangerSession.UserId.ToString(), body = "x" }, Cancel);
        using var noSubject = await owner.PostAsJsonAsync(Rules, new { name = "Konusuz", trigger = "quote.accepted", action = "email_client", body = "x" }, Cancel);
        using var unknownTrigger = await owner.PostAsJsonAsync(Rules, new { name = "Bilinmeyen", trigger = "moon.landed", action = "notify_team", body = "x" }, Cancel);
        using var memberList = await member.GetAsync(Rules, Cancel);
        using var strangerRuns = await stranger.GetAsync($"{Rules}/{rule.Id}/runs", Cancel);
        using var strangerUpdate = await stranger.PutAsJsonAsync($"{Rules}/{rule.Id}", new { name = "Ele geçirildi", trigger = "quote.accepted", action = "notify_team", body = "x" }, Cancel);
        using var off = await owner.PutAsJsonAsync($"{Rules}/{rule.Id}", new { name = "Muhasebeciye", trigger = "quote.accepted", action = "email_address", target = "muhasebe@example.com", subject = "Yeni iş", body = "{{party}} {{amount}}", enabled = false }, Cancel);
        var switchedOff = await ReadAsync<RuleResponse>(off, HttpStatusCode.OK);
        var catalog = await owner.GetFromJsonAsync<CatalogResponse>("/api/v1/automation/catalog", Cancel);

        Assert.Equal("automation.rule.trigger_has_no_client", await noClient.ReadErrorCodeAsync());
        Assert.Equal("automation.rule.target_not_member", await notMember.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noSubject.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownTrigger.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerRuns.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerUpdate.StatusCode);
        Assert.False(switchedOff.Enabled);
        Assert.Contains(catalog!.Triggers, trigger => trigger is { Code: "lead.created", HasClient: false });
        Assert.Contains("sms_client", catalog.Actions);
    }
}
