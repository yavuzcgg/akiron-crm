using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Jobs.Features;
using Akiron.Modules.Jobs.Features.ListWorkOrders;
using Akiron.Modules.Jobs.Features.TimeTracking;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Jobs;

public sealed class WorkOrderTests(ApiFixture api)
{
    private const string WorkOrders = "/api/v1/jobs/work-orders";

    private static readonly string[] LogoTasks = ["Brief toplantısı", "3 eskiz", "Sunum", "Final teslim"];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

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
        var session = await accepted.Content.ReadFromJsonAsync<SessionResponse>(Cancel);
        return (member, session!.UserId);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private static async Task<WorkOrderResponse> CreateAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(WorkOrders, body, Cancel);
        return await ReadAsync<WorkOrderResponse>(response, HttpStatusCode.Created);
    }

    private static async Task<BoardResponse> BoardAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<BoardResponse>($"{WorkOrders}/board", Cancel))!;

    [Fact]
    public async Task Board_OnFirstUse_HasTheFourBuiltInStages()
    {
        var (owner, _) = await OwnerAsync();

        var board = await BoardAsync(owner);

        Assert.Equal(["todo", "in_progress", "review", "done"], board.Stages.Select(stage => stage.Key));
        Assert.Equal(["open", "active", "active", "done"], board.Stages.Select(stage => stage.Category));
        Assert.Empty(board.WorkOrders);
    }

    [Fact]
    public async Task Create_ForAClient_NumbersItNotifiesTheAssigneeAndWritesBothTimelines()
    {
        var (owner, session) = await OwnerAsync();
        var (member, memberId) = await MemberOfAsync(owner);
        using var partyResponse = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "ABC Mobilya", isCustomer = true, isSupplier = false }, Cancel);
        var party = await ReadAsync<PartyResponse>(partyResponse, HttpStatusCode.Created);

        var first = await CreateAsync(owner, new { title = "Logo tasarımı", partyId = party.Id, assigneeIds = new[] { memberId, session.UserId }, priority = "high", dueDate = "2026-10-15" });
        var second = await CreateAsync(owner, new { title = "Kurumsal kimlik" });
        await api.DeliverOutboxAsync();

        var partyTimeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/party/{party.Id}", Cancel);
        var workOrderTimeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/work_order/{first.Id}", Cancel);
        using var memberInbox = await member.GetAsync("/api/v1/notifications", Cancel);
        using var ownerInbox = await owner.GetAsync("/api/v1/notifications", Cancel);

        var year = DateTimeOffset.UtcNow.Year;
        Assert.Equal($"IS-{year}-0001", first.Number);
        Assert.Equal($"IS-{year}-0002", second.Number);
        Assert.Equal("todo", first.Stage.Key);
        Assert.Equal("ABC Mobilya", first.PartyName);
        Assert.Equal("high", first.Priority);
        Assert.Equal(new DateOnly(2026, 10, 15), first.DueDate);
        Assert.Equal(2, first.Assignees.Count);
        Assert.Contains(partyTimeline!.Items, item => item.Type == "jobs.work_order.created");
        Assert.Equal("jobs.work_order.created", Assert.Single(workOrderTimeline!.Items).Type);
        Assert.Contains("jobs.work_order.assigned", await memberInbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.DoesNotContain("jobs.work_order.assigned", await ownerInbox.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_WithAClientOrPersonFromAnotherTenant_IsRefused()
    {
        var (owner, _) = await OwnerAsync();
        var (stranger, strangerSession) = await OwnerAsync();
        using var foreignParty = await stranger.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "Yabancı", isCustomer = true, isSupplier = false }, Cancel);
        var party = await ReadAsync<PartyResponse>(foreignParty, HttpStatusCode.Created);

        using var withParty = await owner.PostAsJsonAsync(WorkOrders, new { title = "Sızma", partyId = party.Id }, Cancel);
        using var withPerson = await owner.PostAsJsonAsync(WorkOrders, new { title = "Sızma", assigneeIds = new[] { strangerSession.UserId } }, Cancel);

        Assert.Equal("jobs.work_order.party_not_found", await withParty.ReadErrorCodeAsync());
        Assert.Equal("jobs.work_order.assignee_not_member", await withPerson.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Move_ToADoneStage_CompletesItAndBackReopensIt()
    {
        var (owner, _) = await OwnerAsync();
        var workOrder = await CreateAsync(owner, new { title = "Çekim" });
        var stages = (await BoardAsync(owner)).Stages;
        var done = stages.Single(stage => stage.Key == "done");
        var inProgress = stages.Single(stage => stage.Key == "in_progress");

        using var toDone = await owner.PostAsJsonAsync($"{WorkOrders}/{workOrder.Id}/move", new { stageId = done.Id }, Cancel);
        var completed = await ReadAsync<WorkOrderCard>(toDone, HttpStatusCode.OK);
        using var back = await owner.PostAsJsonAsync($"{WorkOrders}/{workOrder.Id}/move", new { stageId = inProgress.Id }, Cancel);
        var reopened = await ReadAsync<WorkOrderCard>(back, HttpStatusCode.OK);
        await api.DeliverOutboxAsync();
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/work_order/{workOrder.Id}", Cancel);

        Assert.NotNull(completed.CompletedAt);
        Assert.Null(reopened.CompletedAt);
        var moved = timeline!.Items.Where(item => item.Type == "jobs.work_order.moved").ToList();
        Assert.Equal(2, moved.Count);
        Assert.Equal("done", moved[1].Payload.GetProperty("toKey").GetString());
        Assert.True(moved[1].Payload.GetProperty("completed").GetBoolean());
    }

    [Fact]
    public async Task Move_WithinAStage_PutsTheCardAtTheGivenPosition()
    {
        var (owner, _) = await OwnerAsync();
        var first = await CreateAsync(owner, new { title = "Bir" });
        await CreateAsync(owner, new { title = "İki" });
        var third = await CreateAsync(owner, new { title = "Üç" });

        using var move = await owner.PostAsJsonAsync($"{WorkOrders}/{third.Id}/move", new { stageId = first.Stage.Id, index = 0 }, Cancel);
        var board = await BoardAsync(owner);

        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        Assert.Equal(["Üç", "Bir", "İki"], board.WorkOrders.Select(card => card.Title));
    }

    [Fact]
    public async Task Tasks_AreCountedOnTheCard()
    {
        var (owner, _) = await OwnerAsync();
        var workOrder = await CreateAsync(owner, new { title = "Web sitesi" });

        using var draft = await owner.PostAsJsonAsync($"{WorkOrders}/{workOrder.Id}/tasks", new { title = "Taslak" }, Cancel);
        using var _ = await owner.PostAsJsonAsync($"{WorkOrders}/{workOrder.Id}/tasks", new { title = "Revize" }, Cancel);
        var task = await ReadAsync<WorkOrderTaskResponse>(draft, HttpStatusCode.Created);
        using var tick = await owner.PutAsJsonAsync($"{WorkOrders}/{workOrder.Id}/tasks/{task.Id}", new { title = "Taslak", isDone = true }, Cancel);
        var card = Assert.Single((await BoardAsync(owner)).WorkOrders);
        var detail = await owner.GetFromJsonAsync<WorkOrderResponse>($"{WorkOrders}/{workOrder.Id}", Cancel);

        Assert.Equal(HttpStatusCode.OK, tick.StatusCode);
        Assert.Equal((1, 2), (card.TasksDone, card.TasksTotal));
        Assert.Equal(["Taslak", "Revize"], detail!.Tasks.Select(item => item.Title));
        Assert.True(detail.Tasks[0].IsDone);
    }

    [Fact]
    public async Task Timer_StartingASecondOne_StopsTheFirstAndTimeAddsUp()
    {
        var (owner, _) = await OwnerAsync();
        var logo = await CreateAsync(owner, new { title = "Logo" });
        var web = await CreateAsync(owner, new { title = "Web" });

        using var startLogo = await owner.PostAsJsonAsync("/api/v1/jobs/time/timer/start", new { workOrderId = logo.Id }, Cancel);
        using var startWeb = await owner.PostAsJsonAsync("/api/v1/jobs/time/timer/start", new { workOrderId = web.Id, note = "Ana sayfa" }, Cancel);
        using var running = await owner.GetAsync("/api/v1/jobs/time/timer", Cancel);
        var runningEntry = await ReadAsync<TimeEntryResponse>(running, HttpStatusCode.OK);
        using var stop = await owner.PostAsync(new Uri("/api/v1/jobs/time/timer/stop", UriKind.Relative), null, Cancel);
        using var stopAgain = await owner.PostAsync(new Uri("/api/v1/jobs/time/timer/stop", UriKind.Relative), null, Cancel);
        using var nothingRunning = await owner.GetAsync("/api/v1/jobs/time/timer", Cancel);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var logged = await owner.PostAsJsonAsync("/api/v1/jobs/time/entries", new { workOrderId = logo.Id, date = today, minutes = 90 }, Cancel);
        var entries = await owner.GetFromJsonAsync<List<TimeEntryResponse>>($"/api/v1/jobs/time/entries?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}", Cancel);
        var logoDetail = await owner.GetFromJsonAsync<WorkOrderResponse>($"{WorkOrders}/{logo.Id}", Cancel);

        Assert.Equal(HttpStatusCode.OK, startLogo.StatusCode);
        Assert.Equal(web.Id, runningEntry.WorkOrderId);
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        Assert.Equal("jobs.timer.not_running", await stopAgain.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, nothingRunning.StatusCode);
        Assert.Equal(HttpStatusCode.Created, logged.StatusCode);
        Assert.Equal(3, entries!.Count);
        Assert.All(entries, entry => Assert.NotNull(entry.EndedAt));
        Assert.Equal(91, logoDetail!.MinutesLogged);
    }

    [Fact]
    public async Task TimeEntries_OfSomeoneElse_NeedTheTimesheetPermission()
    {
        var (owner, session) = await OwnerAsync();
        var (member, memberId) = await MemberOfAsync(owner);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var range = $"from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}";

        using var memberReadsOwner = await member.GetAsync($"/api/v1/jobs/time/entries?{range}&userId={session.UserId}", Cancel);
        using var ownerReadsMember = await owner.GetAsync($"/api/v1/jobs/time/entries?{range}&userId={memberId}", Cancel);
        using var tooLong = await owner.GetAsync($"/api/v1/jobs/time/entries?from={today.AddDays(-90):yyyy-MM-dd}&to={today:yyyy-MM-dd}", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, memberReadsOwner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerReadsMember.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Stages_CanBeAddedRenamedButNotRemovedWhileUsedOrLast()
    {
        var (owner, _) = await OwnerAsync();
        var workOrder = await CreateAsync(owner, new { title = "Kampanya" });

        using var added = await owner.PostAsJsonAsync("/api/v1/jobs/stages", new { name = "Müşteri onayı", category = "active" }, Cancel);
        var stage = await ReadAsync<StageResponse>(added, HttpStatusCode.Created);
        using var renamed = await owner.PutAsJsonAsync($"/api/v1/jobs/stages/{stage.Id}", new { name = "Onayda", category = "active" }, Cancel);
        var stages = (await BoardAsync(owner)).Stages;
        using var removeUsed = await owner.DeleteAsync($"/api/v1/jobs/stages/{workOrder.Stage.Id}", Cancel);
        using var removeLastDone = await owner.DeleteAsync($"/api/v1/jobs/stages/{stages.Single(candidate => candidate.Key == "done").Id}", Cancel);
        using var removeEmpty = await owner.DeleteAsync($"/api/v1/jobs/stages/{stage.Id}", Cancel);

        Assert.Equal(["todo", "in_progress", "review", null, "done"], stages.Select(candidate => candidate.Key));
        Assert.Equal("Onayda", stages[3].Name);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("jobs.stage.not_empty", await removeUsed.ReadErrorCodeAsync());
        Assert.Equal("jobs.stage.last_of_its_kind", await removeLastDone.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, removeEmpty.StatusCode);
    }

    [Fact]
    public async Task RenamingTheClient_UpdatesTheNameOnItsWorkOrders()
    {
        var (owner, _) = await OwnerAsync();
        using var partyResponse = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "ABC", isCustomer = true, isSupplier = false }, Cancel);
        var party = await ReadAsync<PartyResponse>(partyResponse, HttpStatusCode.Created);
        var workOrder = await CreateAsync(owner, new { title = "Broşür", partyId = party.Id });

        using var _ = await owner.PutAsJsonAsync($"/api/v1/crm/parties/{party.Id}", new { kind = "company", name = "ABC Mobilya A.Ş.", isCustomer = true, isSupplier = false }, Cancel);
        await api.DeliverOutboxAsync();
        var detail = await owner.GetFromJsonAsync<WorkOrderResponse>($"{WorkOrders}/{workOrder.Id}", Cancel);
        var list = await owner.GetFromJsonAsync<PagedResult<WorkOrderCard>>($"{WorkOrders}?partyId={party.Id}", Cancel);

        Assert.Equal("ABC Mobilya A.Ş.", detail!.PartyName);
        Assert.Equal(workOrder.Id, Assert.Single(list!.Items).Id);
    }

    [Fact]
    public async Task WorkOrders_AreInvisibleAndUntouchableFromAnotherTenant()
    {
        var (owner, _) = await OwnerAsync();
        var (intruder, _) = await OwnerAsync();
        var workOrder = await CreateAsync(owner, new { title = "Gizli iş" });
        var intruderStage = (await BoardAsync(intruder)).Stages[0];

        using var read = await intruder.GetAsync($"{WorkOrders}/{workOrder.Id}", Cancel);
        using var update = await intruder.PutAsJsonAsync($"{WorkOrders}/{workOrder.Id}", new { title = "Ele geçirildi" }, Cancel);
        using var move = await intruder.PostAsJsonAsync($"{WorkOrders}/{workOrder.Id}/move", new { stageId = intruderStage.Id }, Cancel);
        using var task = await intruder.PostAsJsonAsync($"{WorkOrders}/{workOrder.Id}/tasks", new { title = "Sızma" }, Cancel);
        using var timer = await intruder.PostAsJsonAsync("/api/v1/jobs/time/timer/start", new { workOrderId = workOrder.Id }, Cancel);
        using var archive = await intruder.DeleteAsync($"{WorkOrders}/{workOrder.Id}", Cancel);
        var intruderBoard = await BoardAsync(intruder);
        var stillThere = await owner.GetFromJsonAsync<WorkOrderResponse>($"{WorkOrders}/{workOrder.Id}", Cancel);

        Assert.All([read, update, move, task, timer, archive], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.Empty(intruderBoard.WorkOrders);
        Assert.Equal("Gizli iş", stillThere!.Title);
        Assert.Empty(stillThere.Tasks);
    }

    [Fact]
    public async Task Member_ByDefault_CanRunWorkOrdersButNotChangeTheBoard()
    {
        var (owner, _) = await OwnerAsync();
        var (member, memberId) = await MemberOfAsync(owner);
        using var client = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "IŞIK Tekstil", isCustomer = true, isSupplier = false }, Cancel);
        using var supplier = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "Işık Kâğıt", isCustomer = false, isSupplier = true }, Cancel);

        var picker = await member.GetFromJsonAsync<JsonElement>("/api/v1/jobs/clients?search=isik", Cancel);
        var clientId = picker.EnumerateArray().Single().GetProperty("partyId").GetGuid();
        var workOrder = await CreateAsync(member, new { title = "Üyenin işi", partyId = clientId, assigneeIds = new[] { memberId } });
        using var stage = await member.PostAsJsonAsync("/api/v1/jobs/stages", new { name = "Yeni", category = "active" }, Cancel);
        using var people = await member.GetAsync("/api/v1/jobs/assignable-members", Cancel);
        using var mine = await member.GetAsync($"{WorkOrders}/board?mine=true", Cancel);

        Assert.Equal("IŞIK Tekstil", workOrder.PartyName);
        Assert.Equal(HttpStatusCode.Forbidden, stage.StatusCode);
        Assert.Equal(2, (await people.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetArrayLength());
        Assert.Single((await mine.Content.ReadFromJsonAsync<BoardResponse>(Cancel))!.WorkOrders);
    }

    [Fact]
    public async Task Template_FillsTheChecklistAndDueDateOfANewWorkOrder()
    {
        var (owner, _) = await OwnerAsync();
        var (member, _) = await MemberOfAsync(owner);

        using var created = await owner.PostAsJsonAsync("/api/v1/jobs/templates", new
        {
            name = "Logo tasarımı",
            title = "Logo tasarımı",
            priority = "high",
            dueInDays = 10,
            tasks = LogoTasks,
        }, Cancel);
        var template = await ReadAsync<JsonElement>(created, HttpStatusCode.Created);
        using var memberCreates = await member.PostAsJsonAsync("/api/v1/jobs/templates", new { name = "Yetkisiz" }, Cancel);
        var workOrder = await CreateAsync(member, new { title = "Logo tasarımı", priority = "high", templateId = template.GetProperty("id").GetGuid() });
        using var unknown = await owner.PostAsJsonAsync(WorkOrders, new { title = "X", templateId = Guid.NewGuid() }, Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, memberCreates.StatusCode);
        Assert.Equal(LogoTasks, workOrder.Tasks.Select(task => task.Title));
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), workOrder.DueDate);
        Assert.Equal("jobs.template.not_found", await unknown.ReadErrorCodeAsync());
    }
}
