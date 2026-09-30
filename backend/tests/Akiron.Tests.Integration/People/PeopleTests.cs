using System.Net;
using System.Net.Http.Json;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Jobs.Features;
using Akiron.Modules.People.Features.Employees;
using Akiron.Modules.People.Features.Leave;

namespace Akiron.Tests.Integration.People;

public sealed class PeopleTests(ApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Client, SessionResponse Session)> OwnerAsync()
    {
        var client = api.CreateClient();
        using var registered = await client.RegisterAsync();
        return (client, (await registered.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!);
    }

    private async Task<(HttpClient Client, Guid UserId)> JoinAsync(HttpClient owner, string role = "member")
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(email, role);
        var client = api.CreateClient();
        using var accepted = await client.AcceptInvitationAsync(api.InvitationTokenFor(email));
        return (client, (await accepted.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!.UserId);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private static Task<HttpResponseMessage> RequestLeaveAsync(HttpClient client, string start, string end, string type = "annual", bool halfDay = false) =>
        client.PostAsJsonAsync("/api/v1/people/leave", new { type, startDate = start, endDate = end, halfDay }, Cancel);

    [Fact]
    public async Task Employees_ShowEveryMemberAndHourlyCostsOnlyToCostReaders()
    {
        var (owner, _) = await OwnerAsync();
        var (member, memberId) = await JoinAsync(owner);

        using var update = await owner.PutAsJsonAsync($"/api/v1/people/employees/{memberId}", new { jobTitle = "Grafik tasarımcı", department = "Kreatif", hourlyCost = 450.5m, annualLeaveDays = 20 }, Cancel);
        var asOwner = await owner.GetFromJsonAsync<List<EmployeeResponse>>("/api/v1/people/employees", Cancel);
        var asMember = await member.GetFromJsonAsync<List<EmployeeResponse>>("/api/v1/people/employees", Cancel);
        using var memberEdits = await member.PutAsJsonAsync($"/api/v1/people/employees/{memberId}", new { hourlyCost = 9999m }, Cancel);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(2, asOwner!.Count);
        Assert.Equal(450.5m, asOwner.Single(person => person.UserId == memberId).HourlyCost);
        var seenByMember = asMember!.Single(person => person.UserId == memberId);
        Assert.Equal("Grafik tasarımcı", seenByMember.JobTitle);
        Assert.Null(seenByMember.HourlyCost);
        Assert.Equal(HttpStatusCode.Forbidden, memberEdits.StatusCode);
    }

    [Fact]
    public async Task Leave_ApprovedByTheOwner_UsesTheAllowanceAndNotifiesBothSides()
    {
        var (owner, _) = await OwnerAsync();
        var (member, _) = await JoinAsync(owner);

        using var submitted = await RequestLeaveAsync(member, "2027-03-01", "2027-03-05");
        var request = await ReadAsync<LeaveResponse>(submitted, HttpStatusCode.Created);
        await api.DeliverOutboxAsync();
        var pending = await owner.GetFromJsonAsync<List<LeaveResponse>>("/api/v1/people/leave/pending", Cancel);
        using var ownerInbox = await owner.GetAsync("/api/v1/notifications", Cancel);

        using var decided = await owner.PostAsJsonAsync($"/api/v1/people/leave/{request.Id}/decide", new { approve = true, note = "İyi tatiller" }, Cancel);
        using var again = await owner.PostAsJsonAsync($"/api/v1/people/leave/{request.Id}/decide", new { approve = false }, Cancel);
        await api.DeliverOutboxAsync();
        var mine = await member.GetFromJsonAsync<MyLeaveResponse>("/api/v1/people/leave/mine?year=2027", Cancel);
        using var memberInbox = await member.GetAsync("/api/v1/notifications", Cancel);

        Assert.Equal(5m, request.Days);
        Assert.Equal("pending", request.Status);
        Assert.Equal(request.Id, Assert.Single(pending!).Id);
        Assert.Contains("people.leave.requested", await ownerInbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        Assert.Equal("people.leave.already_decided", await again.ReadErrorCodeAsync());
        Assert.Equal((14, 5m, 0m, 9m), (mine!.Balance.Allowance, mine.Balance.Used, mine.Balance.Pending, mine.Balance.Remaining));
        Assert.Equal("approved", Assert.Single(mine.Requests).Status);
        Assert.Contains("people.leave.decided", await memberInbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leave_CountsWorkingDaysOnlyAndRefusesImpossibleRequests()
    {
        var (owner, _) = await OwnerAsync();
        var (member, _) = await JoinAsync(owner);

        // 26–30 October 2026: Monday to Friday, with Republic Day (29th) in between.
        using var withHoliday = await RequestLeaveAsync(member, "2026-10-26", "2026-10-30");
        using var overlapping = await RequestLeaveAsync(member, "2026-10-30", "2026-11-02");
        using var weekend = await RequestLeaveAsync(member, "2026-11-07", "2026-11-08");
        using var tooMuch = await RequestLeaveAsync(member, "2026-11-09", "2026-11-27");
        using var halfDay = await RequestLeaveAsync(member, "2026-12-04", "2026-12-04", halfDay: true);
        using var sick = await RequestLeaveAsync(member, "2026-11-09", "2026-11-27", type: "sick");

        Assert.Equal(4m, (await ReadAsync<LeaveResponse>(withHoliday, HttpStatusCode.Created)).Days);
        Assert.Equal("people.leave.overlaps", await overlapping.ReadErrorCodeAsync());
        Assert.Equal("people.leave.no_working_days", await weekend.ReadErrorCodeAsync());
        Assert.Equal("people.leave.over_allowance", await tooMuch.ReadErrorCodeAsync());
        Assert.Equal(0.5m, (await ReadAsync<LeaveResponse>(halfDay, HttpStatusCode.Created)).Days);
        Assert.Equal(HttpStatusCode.Created, sick.StatusCode);
    }

    [Fact]
    public async Task Leave_OwnRequest_NeedsSomeoneElseUnlessYouAreTheOwner()
    {
        var (owner, _) = await OwnerAsync();
        var (admin, _) = await JoinAsync(owner, "admin");

        var adminRequest = await ReadAsync<LeaveResponse>(await RequestLeaveAsync(admin, "2027-04-05", "2027-04-06"), HttpStatusCode.Created);
        var ownerRequest = await ReadAsync<LeaveResponse>(await RequestLeaveAsync(owner, "2027-04-07", "2027-04-08"), HttpStatusCode.Created);
        using var adminSelf = await admin.PostAsJsonAsync($"/api/v1/people/leave/{adminRequest.Id}/decide", new { approve = true }, Cancel);
        using var ownerSelf = await owner.PostAsJsonAsync($"/api/v1/people/leave/{ownerRequest.Id}/decide", new { approve = true }, Cancel);
        using var adminForOwner = await admin.PostAsync(new Uri($"/api/v1/people/leave/{adminRequest.Id}/cancel", UriKind.Relative), null, Cancel);

        Assert.Equal("people.leave.own_request", await adminSelf.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, ownerSelf.StatusCode);
        Assert.Equal("cancelled", (await ReadAsync<LeaveResponse>(adminForOwner, HttpStatusCode.OK)).Status);
    }

    [Fact]
    public async Task Calendar_ShowsWhoIsOffButTheReasonOnlyToApprovers()
    {
        var (owner, _) = await OwnerAsync();
        var (member, _) = await JoinAsync(owner);
        var (colleague, _) = await JoinAsync(owner);
        var request = await ReadAsync<LeaveResponse>(await RequestLeaveAsync(member, "2027-05-03", "2027-05-04", type: "sick"), HttpStatusCode.Created);
        using var _ = await owner.PostAsJsonAsync($"/api/v1/people/leave/{request.Id}/decide", new { approve = true }, Cancel);

        const string calendar = "/api/v1/people/leave/calendar?from=2027-05-01&to=2027-05-31";
        var seenByColleague = await colleague.GetFromJsonAsync<List<LeaveResponse>>(calendar, Cancel);
        var seenByOwner = await owner.GetFromJsonAsync<List<LeaveResponse>>(calendar, Cancel);
        var seenBySelf = await member.GetFromJsonAsync<List<LeaveResponse>>(calendar, Cancel);

        Assert.Null(Assert.Single(seenByColleague!).Type);
        Assert.Equal("sick", Assert.Single(seenByOwner!).Type);
        Assert.Equal("sick", Assert.Single(seenBySelf!).Type);
    }

    [Fact]
    public async Task Leave_IsInvisibleAndUntouchableFromAnotherTenant()
    {
        var (owner, _) = await OwnerAsync();
        var (member, _) = await JoinAsync(owner);
        var (intruder, _) = await OwnerAsync();
        var request = await ReadAsync<LeaveResponse>(await RequestLeaveAsync(member, "2027-06-07", "2027-06-08"), HttpStatusCode.Created);

        var pending = await intruder.GetFromJsonAsync<List<LeaveResponse>>("/api/v1/people/leave/pending", Cancel);
        using var decide = await intruder.PostAsJsonAsync($"/api/v1/people/leave/{request.Id}/decide", new { approve = false }, Cancel);
        using var profile = await intruder.GetAsync($"/api/v1/people/employees/{request.UserId}", Cancel);
        var stillPending = await owner.GetFromJsonAsync<List<LeaveResponse>>("/api/v1/people/leave/pending", Cancel);

        Assert.Empty(pending!);
        Assert.Equal(HttpStatusCode.NotFound, decide.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, profile.StatusCode);
        Assert.Equal("pending", Assert.Single(stillPending!).Status);
    }

    [Fact]
    public async Task WorkOrderProfit_UsesTheHourlyCostWhenTheTimeWasLogged()
    {
        var (owner, _) = await OwnerAsync();
        var (member, memberId) = await JoinAsync(owner);
        using var workOrderResponse = await owner.PostAsJsonAsync("/api/v1/jobs/work-orders", new { title = "Logo" }, Cancel);
        var workOrder = await ReadAsync<WorkOrderResponse>(workOrderResponse, HttpStatusCode.Created);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var _ = await owner.PutAsJsonAsync($"/api/v1/people/employees/{memberId}", new { hourlyCost = 600m, annualLeaveDays = 14 }, Cancel);
        using var __ = await member.PostAsJsonAsync("/api/v1/jobs/time/entries", new { workOrderId = workOrder.Id, date = today, minutes = 90 }, Cancel);
        using var ___ = await owner.PutAsJsonAsync($"/api/v1/people/employees/{memberId}", new { hourlyCost = 800m, annualLeaveDays = 14 }, Cancel);
        using var ____ = await owner.PostAsJsonAsync("/api/v1/jobs/time/entries", new { workOrderId = workOrder.Id, date = today, minutes = 30 }, Cancel);
        using var budget = await owner.PutAsJsonAsync($"/api/v1/jobs/work-orders/{workOrder.Id}/budget", new { budget = 10_000m }, Cancel);
        var ownerView = await ReadAsync<WorkOrderResponse>(budget, HttpStatusCode.OK);
        var memberView = await member.GetFromJsonAsync<WorkOrderResponse>($"/api/v1/jobs/work-orders/{workOrder.Id}", Cancel);
        using var memberBudget = await member.PutAsJsonAsync($"/api/v1/jobs/work-orders/{workOrder.Id}/budget", new { budget = 1m }, Cancel);

        var financials = ownerView.Financials!;
        Assert.Equal(900m, financials.Cost);
        Assert.Equal(30, financials.UncostedMinutes);
        Assert.Equal(9_100m, financials.Profit);
        Assert.Equal(91.0m, financials.MarginPercent);
        Assert.Null(memberView!.Financials);
        Assert.Equal(HttpStatusCode.Forbidden, memberBudget.StatusCode);
    }
}
