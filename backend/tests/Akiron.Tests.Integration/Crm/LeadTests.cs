using System.Net;
using System.Net.Http.Json;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Crm.Features.Leads;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Crm;

public sealed class LeadTests(ApiFixture api)
{
    private const string Leads = "/api/v1/crm/leads";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private async Task<(HttpClient Client, SessionResponse Session)> OwnerAsync()
    {
        var client = api.CreateClient();
        using var registered = await client.RegisterAsync();
        return (client, (await registered.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!);
    }

    private async Task<(HttpClient Client, Guid UserId)> MemberOfAsync(HttpClient owner)
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(email);
        var member = api.CreateClient();
        using var accepted = await member.AcceptInvitationAsync(api.InvitationTokenFor(email));
        return (member, (await accepted.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!.UserId);
    }

    private static async Task<LeadResponse> CreateAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(Leads, body, Cancel);
        return await ReadAsync<LeadResponse>(response, HttpStatusCode.Created);
    }

    private static async Task<HttpResponseMessage> MoveAsync(HttpClient client, Guid id, object body) =>
        await client.PostAsJsonAsync($"{Leads}/{id}/move", body, Cancel);

    private static async Task<IReadOnlyList<LeadResponse>> BoardAsync(HttpClient client, string view = "open") =>
        (await client.GetFromJsonAsync<IReadOnlyList<LeadResponse>>($"{Leads}?view={view}", Cancel))!;

    [Fact]
    public async Task Board_MembersAddAndMoveLeads_LostNeedsAReason()
    {
        var (owner, session) = await OwnerAsync();
        var (member, memberId) = await MemberOfAsync(owner);

        var first = await CreateAsync(member, new { title = "Web sitesi yenileme", companyName = "Işık Tekstil", contactName = "Ayşe Yılmaz", source = "referral", estimatedValue = 90_000m, ownerUserId = session.UserId });
        var second = await CreateAsync(member, new { title = "Instagram yönetimi", contactName = "Mert Kaya", phone = "0532 000 00 00", source = "instagram" });
        using var toContacted = await MoveAsync(member, second.Id, new { stage = "contacted" });
        using var reorder = await MoveAsync(member, first.Id, new { stage = "contacted", index = 0 });
        using var lostWithoutReason = await MoveAsync(member, second.Id, new { stage = "lost" });
        using var lost = await MoveAsync(member, second.Id, new { stage = "lost", lostReason = "Bütçe yetersiz" });
        var open = await BoardAsync(member);
        var closed = await BoardAsync(member, "closed");
        await api.DeliverOutboxAsync();
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/lead/{second.Id}", Cancel);
        using var ownerInbox = await owner.GetAsync("/api/v1/notifications", Cancel);

        Assert.Equal("new", first.Stage);
        Assert.Equal("Yavuz Çelik", first.OwnerName);
        Assert.Equal(HttpStatusCode.OK, toContacted.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, lostWithoutReason.StatusCode);
        var lostLead = await ReadAsync<LeadResponse>(lost, HttpStatusCode.OK);
        Assert.Equal(("lost", "Bütçe yetersiz"), (lostLead.Stage, lostLead.LostReason));
        Assert.NotNull(lostLead.ClosedAt);
        Assert.Equal("contacted", Assert.Single(open).Stage);
        Assert.Equal(second.Id, Assert.Single(closed).Id);
        Assert.Equal(["crm.lead.moved", "crm.lead.moved", "crm.lead.created"], timeline!.Items.Select(item => item.Type));
        Assert.Contains("crm.lead.created", await ownerInbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.NotEqual(Guid.Empty, memberId);
    }

    [Fact]
    public async Task Convert_CreatesACustomerWithItsContact_Once()
    {
        var (owner, _) = await OwnerAsync();
        var lead = await CreateAsync(owner, new { title = "Kurumsal kimlik", companyName = "Mavi Kahve", contactName = "Can Demir", email = "can@mavikahve.example", source = "website" });

        using var convert = await owner.PostAsJsonAsync($"{Leads}/{lead.Id}/convert", new { }, Cancel);
        var converted = await ReadAsync<ConvertLeadResponse>(convert, HttpStatusCode.OK);
        using var again = await owner.PostAsJsonAsync($"{Leads}/{lead.Id}/convert", new { }, Cancel);
        var party = await owner.GetFromJsonAsync<PartyResponse>($"/api/v1/crm/parties/{converted.PartyId}", Cancel);
        await api.DeliverOutboxAsync();
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/party/{converted.PartyId}", Cancel);

        Assert.True(converted.PartyCreated);
        Assert.Equal(converted.PartyId, converted.Lead.PartyId);
        Assert.Equal("Mavi Kahve", converted.Lead.PartyName);
        Assert.Equal("crm.lead.already_converted", await again.ReadErrorCodeAsync());
        Assert.Equal(("Mavi Kahve", true), (party!.Name, party.IsCustomer));
        var contact = Assert.Single(party.Contacts);
        Assert.Equal(("Can Demir", "can@mavikahve.example", true), (contact.FullName, contact.Email, contact.IsPrimary));
        Assert.Contains(timeline!.Items, item => item.Type == "crm.lead.converted");
    }

    [Fact]
    public async Task Quotes_MoveTheirPartysOnlyOpenLead_ToProposalThenWon()
    {
        var (owner, _) = await OwnerAsync();
        using var partyResponse = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "Deniz Otel", isCustomer = true, isSupplier = false }, Cancel);
        var party = await ReadAsync<PartyResponse>(partyResponse, HttpStatusCode.Created);
        var lead = await CreateAsync(owner, new { title = "Sezon kampanyası", partyId = party.Id, source = "phone" });
        using var quoteResponse = await owner.PostAsJsonAsync("/api/v1/sales/quotes", new
        {
            partyId = party.Id,
            title = "Sezon kampanyası",
            recipientEmail = "satin.alma@denizotel.example",
            lines = new[] { new { name = "Kampanya yönetimi", quantity = 1m, unitPrice = 40_000m, unit = "project", discountPercent = 0m, vatRate = 20, withholdingTenths = 0, priceIncludesVat = false } },
        }, Cancel);
        var quote = await quoteResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Cancel);
        var quoteId = quote.GetProperty("id").GetGuid();

        using var send = await owner.PostAsJsonAsync($"/api/v1/sales/quotes/{quoteId}/send", new { email = true }, Cancel);
        await api.DeliverOutboxAsync();
        var afterSend = await owner.GetFromJsonAsync<LeadResponse>($"{Leads}/{lead.Id}", Cancel);
        var client = api.CreateClient();
        using var accept = await client.PostAsJsonAsync($"/api/v1/sales/public/quotes/{api.QuoteTokenFor("satin.alma@denizotel.example")}/accept", new { name = "Selin Aydın" }, Cancel);
        await api.DeliverOutboxAsync();
        var afterAccept = await owner.GetFromJsonAsync<LeadResponse>($"{Leads}/{lead.Id}", Cancel);

        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        Assert.Equal(("proposal", (Guid?)quoteId), (afterSend!.Stage, afterSend.QuoteId));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal("won", afterAccept!.Stage);
    }

    [Fact]
    public async Task WebForm_TurnsAVisitorIntoALead_AndIgnoresBots()
    {
        var (owner, session) = await OwnerAsync();
        using var save = await owner.PutAsJsonAsync("/api/v1/crm/lead-form", new { enabled = true, ownerUserId = session.UserId }, Cancel);
        var form = await ReadAsync<LeadFormResponse>(save, HttpStatusCode.OK);
        var visitor = api.CreateClient();

        using var json = await visitor.PostAsJsonAsync($"/api/v1/crm/public/forms/{form.Key}", new { name = "Elif Şahin", email = "elif@example.com", company = "Şahin Hukuk", message = "Web sitemiz için teklif almak istiyoruz." }, Cancel);
        using var html = await visitor.PostAsync(
            new Uri($"/api/v1/crm/public/forms/{form.Key}", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "Burak Yıldız", ["phone"] = "0555 111 22 33" }),
            Cancel);
        using var bot = await visitor.PostAsync(
            new Uri($"/api/v1/crm/public/forms/{form.Key}", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "Bot", ["website"] = "http://spam.example" }),
            Cancel);
        using var empty = await visitor.PostAsJsonAsync($"/api/v1/crm/public/forms/{form.Key}", new { message = "merhaba" }, Cancel);
        using var unknown = await visitor.PostAsJsonAsync($"/api/v1/crm/public/forms/{new string('a', 32)}", new { name = "X" }, Cancel);
        var board = await BoardAsync(owner);
        await api.DeliverOutboxAsync();
        using var inbox = await owner.GetAsync("/api/v1/notifications", Cancel);

        using var off = await owner.PutAsJsonAsync("/api/v1/crm/lead-form", new { enabled = false }, Cancel);
        using var whileOff = await visitor.PostAsJsonAsync($"/api/v1/crm/public/forms/{form.Key}", new { name = "Geç kalan" }, Cancel);

        Assert.Equal(32, form.Key!.Length);
        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, html.StatusCode);
        Assert.EndsWith("/forms/thanks", html.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, bot.StatusCode);
        Assert.Equal("crm.lead_form.contact_required", await empty.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(2, board.Count);
        var web = Assert.Single(board, lead => lead.CompanyName == "Şahin Hukuk");
        Assert.Equal(("web_form", "Şahin Hukuk", "Elif Şahin", session.UserId), (web.Source, web.Title, web.ContactName, web.OwnerUserId!.Value));
        Assert.Equal("Web sitemiz için teklif almak istiyoruz.", web.Notes);
        Assert.Contains("crm.lead.created", await inbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, whileOff.StatusCode);
    }

    [Fact]
    public async Task FollowUp_RemindsTheOwnerOnceOnTheDay()
    {
        var (owner, session) = await OwnerAsync();
        var lead = await CreateAsync(owner, new { title = "Video prodüksiyon", contactName = "Zeynep Ak", ownerUserId = session.UserId, nextStep = "Teklif sonrası ara", nextStepDate = Today });

        await api.RunJobAsync<LeadFollowUpJob>(Today);
        await api.RunJobAsync<LeadFollowUpJob>(Today);
        await api.DeliverOutboxAsync();
        using var inbox = await owner.GetAsync("/api/v1/notifications", Cancel);
        var body = await inbox.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(body, "crm.lead.follow_up_due"));
        Assert.Contains(lead.Id.ToString(), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leads_StayInTheirTenant()
    {
        var (owner, _) = await OwnerAsync();
        var lead = await CreateAsync(owner, new { title = "Gizli fırsat", contactName = "A" });
        var (stranger, _) = await OwnerAsync();

        using var get = await stranger.GetAsync($"{Leads}/{lead.Id}", Cancel);
        using var move = await MoveAsync(stranger, lead.Id, new { stage = "won" });
        var board = await BoardAsync(stranger);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, move.StatusCode);
        Assert.Empty(board);
    }
}
