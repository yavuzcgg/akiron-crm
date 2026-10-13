using System.Net;
using System.Net.Http.Json;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Jobs.Features;
using Akiron.Modules.Jobs.Features.ClientPortal;
using Akiron.Modules.Jobs.Features.Tickets;
using Akiron.Modules.Portal.Features;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Jobs;

/// <summary>Support tickets: a client asks in the portal, the conversation moves the ticket, the team resolves or turns it into work.</summary>
public sealed class TicketTests(ApiFixture api)
{
    private const string Tickets = "/api/v1/jobs/tickets";
    private const string ClientTickets = "/api/v1/jobs/portal/tickets";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

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

    private async Task<TicketResponse> TicketAsync(HttpClient owner, Guid id)
    {
        await api.DeliverOutboxAsync();
        return (await owner.GetFromJsonAsync<TicketResponse>($"{Tickets}/{id}", Cancel))!;
    }

    [Fact]
    public async Task Ticket_FromThePortal_IsMovedByTheConversation()
    {
        var (owner, _, partyId) = await AgencyAsync();
        var (client, email) = await ClientOfAsync(owner, partyId);

        using var open = await client.PostAsJsonAsync(ClientTickets, new { subject = "Sitede iletişim formu çalışmıyor", description = "Gönder'e basınca hata veriyor.", urgent = true }, Cancel);
        var opened = await ReadAsync<ClientTicket>(open, HttpStatusCode.Created);
        var fresh = await TicketAsync(owner, opened.Id);
        using var inbox = await owner.GetAsync("/api/v1/notifications", Cancel);

        using var reply = await owner.PostAsJsonAsync($"/api/v1/timeline/ticket/{opened.Id}/notes", new { text = "Bakıyoruz, hangi tarayıcıda oluyor?", visibleToClient = true }, Cancel);
        var answered = await TicketAsync(owner, opened.Id);
        using var wait = await owner.PostAsJsonAsync($"{Tickets}/{opened.Id}/status", new { status = "waiting_client" }, Cancel);
        using var fromClient = await client.PostAsJsonAsync($"/api/v1/timeline/portal/ticket/{opened.Id}/notes", new { text = "Chrome'da." }, Cancel);
        var back = await TicketAsync(owner, opened.Id);
        using var resolve = await owner.PostAsJsonAsync($"{Tickets}/{opened.Id}/status", new { status = "resolved" }, Cancel);
        var resolved = await TicketAsync(owner, opened.Id);
        using var again = await client.PostAsJsonAsync($"/api/v1/timeline/portal/ticket/{opened.Id}/notes", new { text = "Hâlâ aynı hata." }, Cancel);
        var reopened = await TicketAsync(owner, opened.Id);
        using var close = await client.PostAsync(new Uri($"{ClientTickets}/{opened.Id}/close", UriKind.Relative), null, Cancel);
        var closed = await ReadAsync<ClientTicket>(close, HttpStatusCode.OK);
        await api.DeliverOutboxAsync();
        var thread = await client.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/portal/ticket/{opened.Id}", Cancel);
        using var after = await owner.GetAsync("/api/v1/notifications", Cancel);

        Assert.Equal(($"DST-{DateTime.UtcNow.Year}-0001", "open", true, "Deniz Aksoy"), (opened.Number, opened.Status, opened.Urgent, opened.RequesterName));
        Assert.Equal(("urgent", true, false), (fresh.Priority, fresh.FromClient, fresh.ResponseOverdue));
        Assert.Null(fresh.FirstResponseAt);
        Assert.Contains("jobs.ticket.opened", await inbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        Assert.Equal("in_progress", answered.Status);
        Assert.NotNull(answered.FirstResponseAt);
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Created), (wait.StatusCode, fromClient.StatusCode));
        Assert.Equal("open", back.Status);
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        Assert.Equal("resolved", resolved.Status);
        Assert.Contains(api.EmailsTo(email), mail => mail.Subject.Contains("talebiniz çözüldü", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal("open", reopened.Status);
        Assert.Equal("closed", closed.Status);
        Assert.Equal("jobs.ticket.opened", thread!.Items[^1].Type);
        Assert.Contains(thread.Items, item => item.Type == "jobs.ticket.status_changed" && item.Payload.GetProperty("to").GetString() == "closed");
        Assert.Contains("jobs.ticket.closed_by_client", await after.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ticket_OpenedByStaff_IsAssignedAndBecomesAWorkOrder()
    {
        var (owner, _, partyId) = await AgencyAsync();
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(memberEmail);
        var member = api.CreateClient();
        using var accepted = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));
        var memberId = (await accepted.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!.UserId;

        using var open = await owner.PostAsJsonAsync(Tickets, new { partyId, subject = "Menü fiyatları güncellensin", description = "Telefonla istedi.", priority = "high", assigneeUserId = memberId, requesterName = "Mert Kaya" }, Cancel);
        var ticket = await ReadAsync<TicketResponse>(open, HttpStatusCode.Created);
        using var convert = await member.PostAsync(new Uri($"{Tickets}/{ticket.Id}/convert", UriKind.Relative), null, Cancel);
        var converted = await ReadAsync<TicketResponse>(convert, HttpStatusCode.OK);
        using var twice = await member.PostAsync(new Uri($"{Tickets}/{ticket.Id}/convert", UriKind.Relative), null, Cancel);
        var workOrder = await owner.GetFromJsonAsync<WorkOrderResponse>($"/api/v1/jobs/work-orders/{converted.WorkOrderId}", Cancel);
        var queue = await member.GetFromJsonAsync<IReadOnlyList<TicketResponse>>($"{Tickets}?view=open&assigneeId={memberId}", Cancel);
        await api.DeliverOutboxAsync();
        using var memberInbox = await member.GetAsync("/api/v1/notifications", Cancel);

        Assert.Equal(("open", "high", false, "Mert Kaya", "Ayşe Yılmaz"), (ticket.Status, ticket.Priority, ticket.FromClient, ticket.RequesterName, ticket.AssigneeName));
        Assert.NotNull(ticket.FirstResponseAt);
        Assert.Equal("in_progress", converted.Status);
        Assert.Equal("jobs.ticket.already_converted", await twice.ReadErrorCodeAsync());
        Assert.Equal(("Menü fiyatları güncellensin", "Mavi Kahve", "high"), (workOrder!.Title, workOrder.PartyName, workOrder.Priority));
        Assert.Equal(ticket.Id, Assert.Single(queue!).Id);
        Assert.Contains("jobs.ticket.opened", await memberInbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tickets_StayWithTheirPartyAndTenant()
    {
        var (owner, _, partyId) = await AgencyAsync();
        var otherParty = await PartyAsync(owner, "Işık Tekstil");
        var (client, _) = await ClientOfAsync(owner, partyId);
        var (otherClient, _) = await ClientOfAsync(owner, otherParty, "Ece Tan");
        var (stranger, _, _) = await AgencyAsync();
        using var open = await client.PostAsJsonAsync(ClientTickets, new { subject = "Gizli talep" }, Cancel);
        var ticket = await ReadAsync<ClientTicket>(open, HttpStatusCode.Created);

        var mine = await client.GetFromJsonAsync<IReadOnlyList<ClientTicket>>(ClientTickets, Cancel);
        var theirs = await otherClient.GetFromJsonAsync<IReadOnlyList<ClientTicket>>(ClientTickets, Cancel);
        using var otherGet = await otherClient.GetAsync($"{ClientTickets}/{ticket.Id}", Cancel);
        using var otherThread = await otherClient.GetAsync($"/api/v1/timeline/portal/ticket/{ticket.Id}", Cancel);
        using var otherWrite = await otherClient.PostAsJsonAsync($"/api/v1/timeline/portal/ticket/{ticket.Id}/notes", new { text = "ben de" }, Cancel);
        using var strangerGet = await stranger.GetAsync($"{Tickets}/{ticket.Id}", Cancel);
        using var strangerOpen = await stranger.PostAsJsonAsync(Tickets, new { partyId, subject = "X" }, Cancel);

        Assert.Single(mine!);
        Assert.Empty(theirs!);
        Assert.Equal(HttpStatusCode.NotFound, otherGet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherThread.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherWrite.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerGet.StatusCode);
        Assert.Equal("jobs.work_order.party_not_found", await strangerOpen.ReadErrorCodeAsync());
    }
}
