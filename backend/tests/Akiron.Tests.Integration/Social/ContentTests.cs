using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Files.Features;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Portal.Features;
using Akiron.Modules.Social.Features;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Social;

/// <summary>The content calendar: plan, review inside the agency, the client's approval through the portal, publish.</summary>
public sealed class ContentTests(ApiFixture api)
{
    private const string Posts = "/api/v1/social/posts";
    private const string ClientPosts = "/api/v1/social/portal/posts";

    private static readonly string[] TwoPlatforms = ["instagram", "facebook"];
    private static readonly string[] OnePlatform = ["instagram"];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private async Task<(HttpClient Owner, SessionResponse Session, Guid PartyId)> AgencyAsync()
    {
        var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync();
        var session = (await registered.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!;
        return (owner, session, await PartyAsync(owner, "Mavi Kahve"));
    }

    private static async Task<Guid> PartyAsync(HttpClient owner, string name)
    {
        using var party = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name, isCustomer = true, isSupplier = false }, Cancel);
        return (await ReadAsync<PartyResponse>(party, HttpStatusCode.Created)).Id;
    }

    private async Task<HttpClient> MemberOfAsync(HttpClient owner)
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(email);
        var member = api.CreateClient();
        using var accepted = await member.AcceptInvitationAsync(api.InvitationTokenFor(email));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return member;
    }

    private async Task<(HttpClient Client, string Email)> ClientOfAsync(HttpClient owner, Guid partyId, string name = "Deniz Aksoy")
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var invite = await owner.PostAsJsonAsync("/api/v1/portal/users", new { partyId, fullName = name, email }, Cancel);
        await ReadAsync<PortalInvitationResponse>(invite, HttpStatusCode.OK);
        var client = api.CreateClient();
        using var enter = await client.PostAsJsonAsync("/api/v1/portal/auth/enter", new { token = api.PortalTokenFor(email) }, Cancel);
        Assert.Equal(HttpStatusCode.NoContent, enter.StatusCode);
        return (client, email);
    }

    private static async Task<ContentPostResponse> PlanAsync(HttpClient client, Guid partyId, string title, DateOnly date, string? caption = "Yeni sezon kahvelerimiz geldi.")
    {
        using var response = await client.PostAsJsonAsync(Posts, new { partyId, title, plannedDate = date, platforms = TwoPlatforms, format = "post", caption, hashtags = "#kahve" }, Cancel);
        return await ReadAsync<ContentPostResponse>(response, HttpStatusCode.Created);
    }

    private static async Task<HttpResponseMessage> ActAsync(HttpClient client, string path, object? body = null) =>
        body is null ? await client.PostAsync(new Uri(path, UriKind.Relative), null, Cancel) : await client.PostAsJsonAsync(path, body, Cancel);

    [Fact]
    public async Task Post_GoesFromDraftThroughInternalReviewToTheClient()
    {
        var (owner, _, partyId) = await AgencyAsync();
        var member = await MemberOfAsync(owner);

        var post = await PlanAsync(member, partyId, "Sezon duyurusu", Today.AddDays(7));
        using var memberSend = await ActAsync(member, $"{Posts}/{post.Id}/send");
        using var submit = await ActAsync(member, $"{Posts}/{post.Id}/submit");
        var inReview = await ReadAsync<ContentPostResponse>(submit, HttpStatusCode.OK);
        using var back = await ActAsync(owner, $"{Posts}/{post.Id}/return", new { note = "Görsel markanın renklerinde değil." });
        var returned = await ReadAsync<ContentPostResponse>(back, HttpStatusCode.OK);
        using var edit = await member.PutAsJsonAsync($"{Posts}/{post.Id}", new { partyId, title = "Sezon duyurusu", plannedDate = Today.AddDays(8), platforms = OnePlatform, format = "reel", caption = "Yeni sezon." }, Cancel);
        var edited = await ReadAsync<ContentPostResponse>(edit, HttpStatusCode.OK);
        using var resubmit = await ActAsync(member, $"{Posts}/{post.Id}/submit");
        using var send = await ActAsync(owner, $"{Posts}/{post.Id}/send");
        var sent = await ReadAsync<ContentPostResponse>(send, HttpStatusCode.OK);
        using var editWhileOut = await member.PutAsJsonAsync($"{Posts}/{post.Id}", new { partyId, title = "Değişti", plannedDate = Today.AddDays(8) }, Cancel);
        using var move = await ActAsync(member, $"{Posts}/{post.Id}/reschedule", new { plannedDate = Today.AddDays(10) });
        var moved = await ReadAsync<ContentPostResponse>(move, HttpStatusCode.OK);
        await api.DeliverOutboxAsync();
        using var ownerInbox = await owner.GetAsync("/api/v1/notifications", Cancel);
        using var memberInbox = await member.GetAsync("/api/v1/notifications", Cancel);

        Assert.Equal(("draft", 0, "Mavi Kahve"), (post.Status, post.Revision, post.PartyName));
        Assert.Equal(HttpStatusCode.Forbidden, memberSend.StatusCode);
        Assert.Equal("internal_review", inReview.Status);
        Assert.Equal(("draft", "Görsel markanın renklerinde değil."), (returned.Status, returned.ReviewNote));
        Assert.Equal(("reel", 1), (edited.Format, edited.Platforms.Count));
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
        Assert.Equal(("client_review", 1), (sent.Status, sent.Revision));
        Assert.Equal("social.content.not_editable", await editWhileOut.ReadErrorCodeAsync());
        Assert.Equal(("client_review", Today.AddDays(10)), (moved.Status, moved.PlannedDate));
        Assert.Contains("social.content.review_requested", await ownerInbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Contains("social.content.returned", await memberInbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_ApprovesOrAsksForChanges_AndSeesNothingBeforeItIsSent()
    {
        var (owner, _, partyId) = await AgencyAsync();
        var (client, _) = await ClientOfAsync(owner, partyId);
        var first = await PlanAsync(owner, partyId, "Pazartesi gönderisi", Today.AddDays(3));
        var second = await PlanAsync(owner, partyId, "Çarşamba gönderisi", Today.AddDays(5));
        var draft = await PlanAsync(owner, partyId, "Henüz hazır değil", Today.AddDays(9));
        using var media = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes("görsel")) { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } }, "file", "pazartesi.png" },
            { new StringContent("content_post"), "subjectType" },
            { new StringContent(first.Id.ToString()), "subjectId" },
        };
        using var upload = await owner.PostAsync(new Uri("/api/v1/files", UriKind.Relative), media, Cancel);
        var file = await ReadAsync<FileResponse>(upload, HttpStatusCode.Created);

        var before = await client.GetFromJsonAsync<IReadOnlyList<ClientContentPost>>(ClientPosts, Cancel);
        using var hiddenMedia = await client.GetAsync($"/api/v1/files/portal?subjectType=content_post&subjectId={first.Id}", Cancel);
        using var sendFirst = await ActAsync(owner, $"{Posts}/{first.Id}/send");
        using var sendSecond = await ActAsync(owner, $"{Posts}/{second.Id}/send");
        var waiting = await client.GetFromJsonAsync<IReadOnlyList<ClientContentPost>>(ClientPosts, Cancel);
        var shownMedia = await client.GetFromJsonAsync<IReadOnlyList<ClientFileResponse>>($"/api/v1/files/portal?subjectType=content_post&subjectId={first.Id}", Cancel);
        using var image = await client.GetAsync($"/api/v1/files/portal/{file.Id}/content", Cancel);
        using var draftForClient = await client.GetAsync($"{ClientPosts}/{draft.Id}", Cancel);
        using var approve = await ActAsync(client, $"{ClientPosts}/{first.Id}/approve");
        var approved = await ReadAsync<ClientContentPost>(approve, HttpStatusCode.OK);
        using var approveTwice = await ActAsync(client, $"{ClientPosts}/{first.Id}/request-changes", new { note = "Vazgeçtim" });
        using var noNote = await ActAsync(client, $"{ClientPosts}/{second.Id}/request-changes", new { note = "" });
        using var changes = await ActAsync(client, $"{ClientPosts}/{second.Id}/request-changes", new { note = "Fiyatı yazmayalım." });
        var changed = await ReadAsync<ClientContentPost>(changes, HttpStatusCode.OK);
        await api.DeliverOutboxAsync();
        var staffView = await owner.GetFromJsonAsync<ContentPostResponse>($"{Posts}/{second.Id}", Cancel);
        var thread = await client.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/portal/content_post/{second.Id}", Cancel);
        using var inbox = await owner.GetAsync("/api/v1/notifications", Cancel);

        Assert.Empty(before!);
        Assert.Equal("files.subject.unknown", await hiddenMedia.ReadErrorCodeAsync());
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (sendFirst.StatusCode, sendSecond.StatusCode));
        Assert.Equal(["Pazartesi gönderisi", "Çarşamba gönderisi"], waiting!.Select(post => post.Title));
        Assert.Equal("pazartesi.png", Assert.Single(shownMedia!).FileName);
        Assert.Equal("görsel", await image.Content.ReadAsStringAsync(Cancel));
        Assert.Equal(HttpStatusCode.NotFound, draftForClient.StatusCode);
        Assert.Equal(("approved", "Deniz Aksoy"), (approved.Status, approved.DecidedByName));
        Assert.Equal("social.content.wrong_state", await approveTwice.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        Assert.Equal(("changes_requested", "Fiyatı yazmayalım."), (changed.Status, changed.ClientNote));
        Assert.Equal(("changes_requested", "Fiyatı yazmayalım."), (staffView!.Status, staffView.ClientNote));
        Assert.Equal(["social.content.decided", "social.content.sent"], thread!.Items.Select(item => item.Type));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(await inbox.Content.ReadAsStringAsync(Cancel), "social.content.decided"));
    }

    [Fact]
    public async Task SendBatch_SendsTheMonthInOneEmail()
    {
        var (owner, _, partyId) = await AgencyAsync();
        var (client, email) = await ClientOfAsync(owner, partyId);
        var otherParty = await PartyAsync(owner, "Işık Tekstil");
        var monthStart = new DateOnly(Today.Year, Today.Month, 1).AddMonths(1);
        foreach (var day in new[] { 2, 9, 16 })
        {
            await PlanAsync(owner, partyId, $"{day} gönderisi", monthStart.AddDays(day));
        }

        await PlanAsync(owner, partyId, "Sonraki ay", monthStart.AddMonths(1).AddDays(3));
        await PlanAsync(owner, otherParty, "Başka müşteri", monthStart.AddDays(4));
        var notYetIn = ApiClientExtensions.UniqueEmail();
        using var invited = await owner.PostAsJsonAsync("/api/v1/portal/users", new { partyId, fullName = "Ece Tan", email = notYetIn }, Cancel);
        var before = api.EmailsTo(email).Count;

        using var batch = await ActAsync(owner, $"{Posts}/send-batch", new { partyId, from = monthStart, to = monthStart.AddMonths(1).AddDays(-1) });
        var sent = await ReadAsync<SendBatchResponse>(batch, HttpStatusCode.OK);
        using var again = await ActAsync(owner, $"{Posts}/send-batch", new { partyId, from = monthStart, to = monthStart.AddMonths(1).AddDays(-1) });
        await api.DeliverOutboxAsync();
        var forClient = await client.GetFromJsonAsync<IReadOnlyList<ClientContentPost>>(ClientPosts, Cancel);
        var emails = api.EmailsTo(email).Skip(before).ToList();

        Assert.Equal(3, sent.Sent);
        Assert.Equal("social.content.nothing_to_send", await again.ReadErrorCodeAsync());
        Assert.Equal(3, forClient!.Count);
        var message = Assert.Single(emails);
        Assert.Contains("3 içerik", message.Subject, StringComparison.Ordinal);
        Assert.Contains("/portal/content", message.TextBody, StringComparison.Ordinal);

        // Someone invited who has not opened the portal yet is told as well.
        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        Assert.Contains(api.EmailsTo(notYetIn), mail => mail.Subject.Contains("3 içerik", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApprovedPost_IsRemindedOnItsDay_ThenMarkedPublished()
    {
        var (owner, _, partyId) = await AgencyAsync();
        var post = await PlanAsync(owner, partyId, "Bugünün gönderisi", Today);
        var later = await PlanAsync(owner, partyId, "Haftaya", Today.AddDays(7));

        using var early = await ActAsync(owner, $"{Posts}/{post.Id}/publish", new { });
        using var approve = await ActAsync(owner, $"{Posts}/{post.Id}/approve");
        var approved = await ReadAsync<ContentPostResponse>(approve, HttpStatusCode.OK);
        using var approveLater = await ActAsync(owner, $"{Posts}/{later.Id}/approve");
        await api.RunJobAsync<ContentReminderJob>(Today);
        await api.RunJobAsync<ContentReminderJob>(Today);
        await api.DeliverOutboxAsync();
        using var inbox = await owner.GetAsync("/api/v1/notifications", Cancel);
        var body = await inbox.Content.ReadAsStringAsync(Cancel);
        using var publish = await ActAsync(owner, $"{Posts}/{post.Id}/publish", new { url = "https://instagram.com/p/abc123" });
        var published = await ReadAsync<ContentPostResponse>(publish, HttpStatusCode.OK);
        using var delete = await owner.DeleteAsync(new Uri($"{Posts}/{post.Id}", UriKind.Relative), Cancel);
        using var revise = await ActAsync(owner, $"{Posts}/{later.Id}/revise");
        var revised = await ReadAsync<ContentPostResponse>(revise, HttpStatusCode.OK);

        Assert.Equal("social.content.wrong_state", await early.ReadErrorCodeAsync());
        Assert.Equal(("approved", 1), (approved.Status, approved.Revision));
        Assert.Equal(HttpStatusCode.OK, approveLater.StatusCode);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(body, "social.content.publish_due"));
        Assert.Contains(post.Id.ToString(), body, StringComparison.Ordinal);
        Assert.Equal(("published", "https://instagram.com/p/abc123"), (published.Status, published.PublishedUrl));
        Assert.Equal("social.content.wrong_state", await delete.ReadErrorCodeAsync());
        Assert.Equal("draft", revised.Status);
    }

    [Fact]
    public async Task Calendar_FiltersByRangeAndClient_AndStaysInItsTenantAndParty()
    {
        var (owner, _, partyId) = await AgencyAsync();
        var otherParty = await PartyAsync(owner, "Işık Tekstil");
        var mine = await PlanAsync(owner, partyId, "Benim", Today.AddDays(2));
        var others = await PlanAsync(owner, otherParty, "Diğer müşteri", Today.AddDays(2));
        await PlanAsync(owner, partyId, "Uzak gelecek", Today.AddDays(90));
        using var send = await ActAsync(owner, $"{Posts}/{others.Id}/send");
        var (client, _) = await ClientOfAsync(owner, partyId);
        var (stranger, _, strangerParty) = await AgencyAsync();

        var week = await owner.GetFromJsonAsync<IReadOnlyList<ContentPostResponse>>($"{Posts}?from={Today:yyyy-MM-dd}&to={Today.AddDays(7):yyyy-MM-dd}", Cancel);
        var forParty = await owner.GetFromJsonAsync<IReadOnlyList<ContentPostResponse>>($"{Posts}?partyId={partyId}", Cancel);
        using var othersPostForClient = await client.GetAsync($"{ClientPosts}/{others.Id}", Cancel);
        using var othersThread = await client.GetAsync($"/api/v1/timeline/portal/content_post/{others.Id}", Cancel);
        using var strangerGet = await stranger.GetAsync($"{Posts}/{mine.Id}", Cancel);
        using var strangerPlan = await stranger.PostAsJsonAsync(Posts, new { partyId, title = "X", plannedDate = Today }, Cancel);
        var strangerList = await stranger.GetFromJsonAsync<IReadOnlyList<ContentPostResponse>>(Posts, Cancel);

        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        Assert.Equal(2, week!.Count);
        Assert.Equal(2, forParty!.Count);
        Assert.Equal(HttpStatusCode.NotFound, othersPostForClient.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, othersThread.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerGet.StatusCode);
        Assert.Equal("social.content.party_not_found", await strangerPlan.ReadErrorCodeAsync());
        Assert.Empty(strangerList!);
        Assert.NotEqual(partyId, strangerParty);
    }
}
