using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Files.Features;
using Akiron.Modules.Finance.Features.ClientPortal;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Jobs.Features;
using Akiron.Modules.Jobs.Features.ClientPortal;
using Akiron.Modules.Portal.Features;
using Akiron.Modules.Sales.Features.ClientPortal;
using Akiron.Modules.Sales.Features.PublicQuotes;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Portal;

/// <summary>
/// The client portal: a separate principal that signs in by link, sees only its own party's records,
/// and never crosses into staff endpoints (ADR-0011).
/// </summary>
public sealed class PortalTests(ApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private sealed record Agency(HttpClient Owner, SessionResponse Session, Guid PartyId);

    private async Task<Agency> AgencyAsync(string partyName = "Işık Tekstil")
    {
        var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync();
        var session = (await registered.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!;
        return new Agency(owner, session, await PartyAsync(owner, partyName));
    }

    private static async Task<Guid> PartyAsync(HttpClient owner, string name)
    {
        using var party = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name, isCustomer = true, isSupplier = false }, Cancel);
        return (await ReadAsync<PartyResponse>(party, HttpStatusCode.Created)).Id;
    }

    /// <summary>Invites a contact and signs them in through the e-mailed link, as a browser of their own.</summary>
    private async Task<(HttpClient Client, string Email, PortalUserResponse User)> ClientOfAsync(HttpClient owner, Guid partyId, string name = "Ayşe Yılmaz")
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var invite = await owner.PostAsJsonAsync("/api/v1/portal/users", new { partyId, fullName = name, email }, Cancel);
        var invitation = await ReadAsync<PortalInvitationResponse>(invite, HttpStatusCode.OK);

        var client = api.CreateClient();
        using var enter = await client.PostAsJsonAsync("/api/v1/portal/auth/enter", new { token = api.PortalTokenFor(email) }, Cancel);
        Assert.Equal(HttpStatusCode.NoContent, enter.StatusCode);
        return (client, email, invitation.User);
    }

    private static async Task<Guid> WorkOrderAsync(HttpClient owner, Guid? partyId, string title)
    {
        using var response = await owner.PostAsJsonAsync("/api/v1/jobs/work-orders", new { title, partyId }, Cancel);
        return (await ReadAsync<WorkOrderResponse>(response, HttpStatusCode.Created)).Id;
    }

    private static MultipartFormDataContent Upload(string fileName, string content, string subjectType, Guid subjectId)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        return new MultipartFormDataContent
        {
            { file, "file", fileName },
            { new StringContent(subjectType), "subjectType" },
            { new StringContent(subjectId.ToString()), "subjectId" },
        };
    }

    [Fact]
    public async Task Invite_EmailsALink_ThatSignsTheClientInOnce()
    {
        var agency = await AgencyAsync();
        var email = ApiClientExtensions.UniqueEmail();

        using var invite = await agency.Owner.PostAsJsonAsync("/api/v1/portal/users", new { partyId = agency.PartyId, fullName = "Ayşe Yılmaz", email = email.ToUpperInvariant() }, Cancel);
        var invitation = await ReadAsync<PortalInvitationResponse>(invite, HttpStatusCode.OK);
        var token = api.PortalTokenFor(email);
        var client = api.CreateClient();
        using var before = await client.GetAsync("/api/v1/portal/me", Cancel);
        using var enter = await client.PostAsJsonAsync("/api/v1/portal/auth/enter", new { token }, Cancel);
        var me = await client.GetFromJsonAsync<PortalMeResponse>("/api/v1/portal/me", Cancel);
        using var again = await api.CreateClient().PostAsJsonAsync("/api/v1/portal/auth/enter", new { token }, Cancel);
        var users = await agency.Owner.GetFromJsonAsync<IReadOnlyList<PortalUserResponse>>($"/api/v1/portal/users?partyId={agency.PartyId}", Cancel);
        await api.DeliverOutboxAsync();
        var timeline = await agency.Owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/party/{agency.PartyId}", Cancel);

        Assert.Equal(("invited", email), (invitation.User.Status, invitation.User.Email));
        Assert.EndsWith($"/portal/enter?token={token}", invitation.Link, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, enter.StatusCode);
        Assert.Equal(("Ayşe Yılmaz", "Işık Tekstil", agency.Session.TenantName, agency.PartyId), (me!.Name, me.PartyName, me.WorkspaceName, me.PartyId));
        Assert.Equal("portal.link.invalid", await again.ReadErrorCodeAsync());
        Assert.Equal("active", Assert.Single(users!).Status);
        Assert.Contains(timeline!.Items, item => item.Type == "portal.user.invited");
    }

    [Fact]
    public async Task RequestLink_SendsAFreshLinkToKnownAddressesOnly()
    {
        var agency = await AgencyAsync();
        var (client, email, _) = await ClientOfAsync(agency.Owner, agency.PartyId);
        var used = api.PortalTokenFor(email);
        var stranger = ApiClientExtensions.UniqueEmail();

        using var logout = await client.PostAsync(new Uri("/api/v1/portal/auth/logout", UriKind.Relative), null, Cancel);
        using var afterLogout = await client.GetAsync("/api/v1/portal/me", Cancel);
        using var known = await client.PostAsJsonAsync("/api/v1/portal/auth/request-link", new { email }, Cancel);
        using var unknown = await client.PostAsJsonAsync("/api/v1/portal/auth/request-link", new { email = stranger }, Cancel);
        var fresh = api.PortalTokenFor(email);
        using var enter = await client.PostAsJsonAsync("/api/v1/portal/auth/enter", new { token = fresh }, Cancel);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (known.StatusCode, unknown.StatusCode));
        Assert.NotEqual(used, fresh);
        Assert.Null(api.PortalTokenFor(stranger));
        Assert.Equal(HttpStatusCode.NoContent, enter.StatusCode);
    }

    [Fact]
    public async Task PortalAndStaffSessions_NeverOpenEachOthersEndpoints()
    {
        var agency = await AgencyAsync();
        var (client, _, _) = await ClientOfAsync(agency.Owner, agency.PartyId);

        using var clientOnStaff = await client.GetAsync("/api/v1/crm/parties", Cancel);
        using var clientOnSession = await client.GetAsync("/api/v1/identity/auth/session", Cancel);
        using var clientOnNotifications = await client.GetAsync("/api/v1/notifications", Cancel);
        using var staffOnPortal = await agency.Owner.GetAsync("/api/v1/portal/me", Cancel);
        using var staffOnClientJobs = await agency.Owner.GetAsync("/api/v1/jobs/portal/work-orders", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, clientOnStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, clientOnSession.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, clientOnNotifications.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, staffOnPortal.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, staffOnClientJobs.StatusCode);
    }

    [Fact]
    public async Task Client_SeesOnlyTheirOwnPartysWorkQuotesAndAccount()
    {
        var agency = await AgencyAsync();
        var otherParty = await PartyAsync(agency.Owner, "Mavi Kahve");
        var mine = await WorkOrderAsync(agency.Owner, agency.PartyId, "Web sitesi yenileme");
        var theirs = await WorkOrderAsync(agency.Owner, otherParty, "Menü tasarımı");
        var internalJob = await WorkOrderAsync(agency.Owner, null, "Ajans içi iş");
        var elsewhere = await AgencyAsync("Başka Ajansın Müşterisi");
        var foreign = await WorkOrderAsync(elsewhere.Owner, elsewhere.PartyId, "Başka ajansın işi");
        using var task = await agency.Owner.PostAsJsonAsync($"/api/v1/jobs/work-orders/{mine}/tasks", new { title = "Gizli iç görev" }, Cancel);
        using var draft = await agency.Owner.PostAsJsonAsync("/api/v1/sales/quotes", new { partyId = agency.PartyId, title = "Taslak teklif", lines = Array.Empty<object>() }, Cancel);
        using var charge = await agency.Owner.PostAsJsonAsync("/api/v1/finance/charges", new { partyId = agency.PartyId, description = "Web sitesi", amount = 30_000m, dueDate = "2026-01-10" }, Cancel);
        using var otherCharge = await agency.Owner.PostAsJsonAsync("/api/v1/finance/charges", new { partyId = otherParty, description = "Menü", amount = 9_000m }, Cancel);
        var (client, _, _) = await ClientOfAsync(agency.Owner, agency.PartyId);

        var workOrders = await client.GetFromJsonAsync<IReadOnlyList<ClientWorkOrder>>("/api/v1/jobs/portal/work-orders", Cancel);
        using var theirOrder = await client.GetAsync($"/api/v1/jobs/portal/work-orders/{theirs}", Cancel);
        using var internalOrder = await client.GetAsync($"/api/v1/jobs/portal/work-orders/{internalJob}", Cancel);
        using var foreignOrder = await client.GetAsync($"/api/v1/jobs/portal/work-orders/{foreign}", Cancel);
        using var foreignThread = await client.GetAsync($"/api/v1/timeline/portal/work_order/{foreign}", Cancel);
        var quotes = await client.GetFromJsonAsync<IReadOnlyList<ClientQuoteRow>>("/api/v1/sales/portal/quotes", Cancel);
        var account = await client.GetFromJsonAsync<ClientAccountResponse>("/api/v1/finance/portal/account", Cancel);

        Assert.Equal(HttpStatusCode.Created, task.StatusCode);
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (charge.StatusCode, otherCharge.StatusCode));
        var order = Assert.Single(workOrders!);
        Assert.Equal((mine, "Web sitesi yenileme", "open", 0, 1), (order.Id, order.Title, order.StageCategory, order.TasksDone, order.TasksTotal));
        Assert.Equal(HttpStatusCode.NotFound, theirOrder.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, internalOrder.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignOrder.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignThread.StatusCode);
        Assert.Empty(quotes!);
        Assert.Equal((30_000m, 30_000m), (Assert.Single(account!.Balances).Balance, Assert.Single(account.Balances).Overdue));
        Assert.Equal("Web sitesi", Assert.Single(account.OpenItems).Description);
    }

    [Fact]
    public async Task Disable_EndsTheSessionAtOnce_AndRefusesNewLinks()
    {
        var agency = await AgencyAsync();
        var (client, email, user) = await ClientOfAsync(agency.Owner, agency.PartyId);

        using var disable = await agency.Owner.PostAsync(new Uri($"/api/v1/portal/users/{user.Id}/disable", UriKind.Relative), null, Cancel);
        var disabled = await ReadAsync<PortalUserResponse>(disable, HttpStatusCode.OK);
        using var after = await client.GetAsync("/api/v1/portal/me", Cancel);
        var linkBefore = api.PortalTokenFor(email);
        using var request = await client.PostAsJsonAsync("/api/v1/portal/auth/request-link", new { email }, Cancel);
        using var resend = await agency.Owner.PostAsync(new Uri($"/api/v1/portal/users/{user.Id}/invite", UriKind.Relative), null, Cancel);
        using var enable = await agency.Owner.PostAsync(new Uri($"/api/v1/portal/users/{user.Id}/enable", UriKind.Relative), null, Cancel);
        var enabled = await ReadAsync<PortalUserResponse>(enable, HttpStatusCode.OK);

        Assert.Equal("disabled", disabled.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, request.StatusCode);
        Assert.Equal(linkBefore, api.PortalTokenFor(email));
        Assert.Equal("portal.user.disabled", await resend.ReadErrorCodeAsync());
        Assert.Equal("active", enabled.Status);
    }

    [Fact]
    public async Task Quote_IsAcceptedFromThePortal_UnderTheClientsName()
    {
        var agency = await AgencyAsync();
        using var created = await agency.Owner.PostAsJsonAsync("/api/v1/sales/quotes", new
        {
            partyId = agency.PartyId,
            title = "Kurumsal kimlik",
            lines = new[] { new { name = "Logo tasarımı", quantity = 1m, unitPrice = 20_000m, unit = "project", discountPercent = 0m, vatRate = 20, withholdingTenths = 0, priceIncludesVat = false } },
        }, Cancel);
        var quoteId = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Cancel)).GetProperty("id").GetGuid();
        using var send = await agency.Owner.PostAsJsonAsync($"/api/v1/sales/quotes/{quoteId}/send", new { email = false }, Cancel);
        var (client, _, _) = await ClientOfAsync(agency.Owner, agency.PartyId, "Can Demir");

        var quotes = await client.GetFromJsonAsync<IReadOnlyList<ClientQuoteRow>>("/api/v1/sales/portal/quotes", Cancel);
        var quote = await client.GetFromJsonAsync<PublicQuoteResponse>($"/api/v1/sales/portal/quotes/{quoteId}", Cancel);
        using var pdf = await client.GetAsync($"/api/v1/sales/portal/quotes/{quoteId}/pdf", Cancel);
        using var accept = await client.PostAsJsonAsync($"/api/v1/sales/portal/quotes/{quoteId}/accept", new { note = "Başlayalım" }, Cancel);
        var accepted = await ReadAsync<PublicQuoteResponse>(accept, HttpStatusCode.OK);
        using var twice = await client.PostAsJsonAsync($"/api/v1/sales/portal/quotes/{quoteId}/reject", new { }, Cancel);
        await api.DeliverOutboxAsync();
        using var inbox = await agency.Owner.GetAsync("/api/v1/notifications", Cancel);

        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        Assert.Equal(("sent", 24_000m), (Assert.Single(quotes!).Status, quotes![0].GrandTotal));
        Assert.Equal("Kurumsal kimlik", quote!.Title);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal(("accepted", "Can Demir"), (accepted.Status, accepted.DecidedByName));
        Assert.Equal("sales.quote.already_decided", await twice.ReadErrorCodeAsync());
        Assert.Contains("sales.quote.decided", await inbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Thread_CarriesMessagesBothWays_AndKeepsInternalNotesInternal()
    {
        var agency = await AgencyAsync();
        var workOrder = await WorkOrderAsync(agency.Owner, agency.PartyId, "Katalog tasarımı");
        var internalJob = await WorkOrderAsync(agency.Owner, null, "Ajans içi iş");
        var (client, email, _) = await ClientOfAsync(agency.Owner, agency.PartyId);

        using var secret = await agency.Owner.PostAsJsonAsync($"/api/v1/timeline/work_order/{workOrder}/notes", new { text = "Müşteri fiyatı pahalı buldu, indirim yapmayalım." }, Cancel);
        using var fromClient = await client.PostAsJsonAsync($"/api/v1/timeline/portal/work_order/{workOrder}/notes", new { text = "Kapak görselini değiştirebilir miyiz?" }, Cancel);
        using var reply = await agency.Owner.PostAsJsonAsync($"/api/v1/timeline/work_order/{workOrder}/notes", new { text = "Elbette, yarın yeni öneri gönderiyoruz.", visibleToClient = true }, Cancel);
        using var notShareable = await agency.Owner.PostAsJsonAsync($"/api/v1/timeline/work_order/{internalJob}/notes", new { text = "Bunu kim görecek?", visibleToClient = true }, Cancel);
        await api.DeliverOutboxAsync();
        var thread = await client.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/portal/work_order/{workOrder}", Cancel);
        var staffView = await agency.Owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/work_order/{workOrder}", Cancel);
        using var inbox = await agency.Owner.GetAsync("/api/v1/notifications", Cancel);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.Created), (secret.StatusCode, fromClient.StatusCode, reply.StatusCode));
        Assert.Equal("timeline.note.not_shareable", await notShareable.ReadErrorCodeAsync());
        Assert.Equal(["user", "portal_contact"], thread!.Items.Select(item => item.Actor.Kind));
        Assert.DoesNotContain(thread.Items, item => item.Payload.GetProperty("text").GetString()!.Contains("indirim", StringComparison.Ordinal));
        Assert.Equal("Ayşe Yılmaz", thread.Items[1].Actor.Name);
        Assert.Equal(3, staffView!.Items.Count(item => item.Type == "timeline.note"));
        Assert.Contains("portal.message.posted", await inbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Contains(api.EmailsTo(email), message => message.TextBody.Contains("yarın yeni öneri", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Files_AreSharedOneByOne_AndClientsSendTheirOwn()
    {
        var agency = await AgencyAsync();
        var workOrder = await WorkOrderAsync(agency.Owner, agency.PartyId, "Logo tasarımı");
        var otherParty = await PartyAsync(agency.Owner, "Mavi Kahve");
        var (client, _, _) = await ClientOfAsync(agency.Owner, agency.PartyId);
        var (otherClient, _, _) = await ClientOfAsync(agency.Owner, otherParty, "Mert Kaya");

        using var content = Upload("logo-v1.txt", "ilk öneri", "work_order", workOrder);
        using var uploaded = await agency.Owner.PostAsync(new Uri("/api/v1/files", UriKind.Relative), content, Cancel);
        var file = await ReadAsync<FileResponse>(uploaded, HttpStatusCode.Created);
        var beforeShare = await client.GetFromJsonAsync<IReadOnlyList<ClientFileResponse>>($"/api/v1/files/portal?subjectType=work_order&subjectId={workOrder}", Cancel);
        using var hidden = await client.GetAsync($"/api/v1/files/portal/{file.Id}/content", Cancel);
        using var share = await agency.Owner.PostAsJsonAsync($"/api/v1/files/{file.Id}/share", new { shared = true }, Cancel);
        var afterShare = await client.GetFromJsonAsync<IReadOnlyList<ClientFileResponse>>($"/api/v1/files/portal?subjectType=work_order&subjectId={workOrder}", Cancel);
        using var download = await client.GetAsync($"/api/v1/files/portal/{file.Id}/content", Cancel);
        using var otherDownload = await otherClient.GetAsync($"/api/v1/files/portal/{file.Id}/content", Cancel);
        using var otherList = await otherClient.GetAsync($"/api/v1/files/portal?subjectType=work_order&subjectId={workOrder}", Cancel);

        using var brief = Upload("brief.txt", "marka renkleri", "work_order", workOrder);
        using var fromClient = await client.PostAsync(new Uri("/api/v1/files/portal", UriKind.Relative), brief, Cancel);
        using var receipt = Upload("dekont.txt", "EFT dekontu", "party", agency.PartyId);
        using var receiptUpload = await client.PostAsync(new Uri("/api/v1/files/portal", UriKind.Relative), receipt, Cancel);
        using var intrusion = Upload("x.txt", "x", "party", otherParty);
        using var intrusionUpload = await client.PostAsync(new Uri("/api/v1/files/portal", UriKind.Relative), intrusion, Cancel);
        await api.DeliverOutboxAsync();
        var staffFiles = await agency.Owner.GetFromJsonAsync<IReadOnlyList<FileResponse>>($"/api/v1/files?subjectType=work_order&subjectId={workOrder}", Cancel);
        using var inbox = await agency.Owner.GetAsync("/api/v1/notifications", Cancel);

        Assert.Empty(beforeShare!);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
        Assert.Equal(("logo-v1.txt", false), (Assert.Single(afterShare!).FileName, afterShare![0].UploadedByClient));
        Assert.Equal("ilk öneri", await download.Content.ReadAsStringAsync(Cancel));
        Assert.Equal(HttpStatusCode.NotFound, otherDownload.StatusCode);
        Assert.Equal("files.subject.unknown", await otherList.ReadErrorCodeAsync());
        Assert.True((await ReadAsync<ClientFileResponse>(fromClient, HttpStatusCode.Created)).UploadedByClient);
        Assert.Equal(HttpStatusCode.Created, receiptUpload.StatusCode);
        Assert.Equal("files.subject.unknown", await intrusionUpload.ReadErrorCodeAsync());
        Assert.Contains(staffFiles!, staffFile => staffFile is { FileName: "brief.txt", ClientUploaderName: "Ayşe Yılmaz", SharedWithClient: true });
        Assert.Contains("portal.file.uploaded", await inbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Access_IsManagedByThoseWhoMay_AndOneAddressServesOneParty()
    {
        var agency = await AgencyAsync();
        var otherParty = await PartyAsync(agency.Owner, "Mavi Kahve");
        var (_, email, _) = await ClientOfAsync(agency.Owner, agency.PartyId);
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await agency.Owner.InviteAsync(memberEmail);
        var member = api.CreateClient();
        using var accepted = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));

        using var sameAddressElsewhere = await agency.Owner.PostAsJsonAsync("/api/v1/portal/users", new { partyId = otherParty, fullName = "Ayşe Yılmaz", email }, Cancel);
        using var memberInvite = await member.PostAsJsonAsync("/api/v1/portal/users", new { partyId = agency.PartyId, fullName = "X", email = ApiClientExtensions.UniqueEmail() }, Cancel);
        using var settings = await agency.Owner.PutAsJsonAsync("/api/v1/portal/settings", new { welcomeNote = "Hoş geldiniz", paymentInstructions = "IBAN TR00 0000 0000 0000 0000 0000 00" }, Cancel);
        var saved = await ReadAsync<PortalSettingsResponse>(settings, HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("portal.user.email_in_use", await sameAddressElsewhere.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, memberInvite.StatusCode);
        Assert.StartsWith("IBAN", saved.PaymentInstructions, StringComparison.Ordinal);
    }
}
