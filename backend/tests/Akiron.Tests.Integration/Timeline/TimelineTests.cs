using System.Net;
using System.Net.Http.Json;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Timeline.Features;
using Akiron.Modules.Timeline.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Tests.Integration.Timeline;

/// <summary>ADR-0009 end to end: module events → outbox → projection → stream.</summary>
public sealed class TimelineTests(ApiFixture api)
{
    [Fact]
    public async Task Workspace_AfterRegistrationAndAJoin_ShowsBothNewestFirst()
    {
        using var owner = api.CreateClient();
        var session = await RegisterAsync(owner);
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(memberEmail);
        using var member = api.CreateClient();
        using var __ = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));

        await api.DeliverOutboxAsync();
        var page = await WorkspaceTimelineAsync(owner, session.TenantId);

        Assert.Equal(["identity.member.joined", "identity.invitation.sent", "identity.workspace.created"], page.Items.Select(item => item.Type));
        var joined = page.Items[0];
        Assert.Equal("Ayşe Yılmaz", joined.Actor.Name);
        Assert.Equal("member", joined.Payload.GetProperty("role").GetString());
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Delivery_OfTheSameEventTwice_WritesOneEntry()
    {
        using var owner = api.CreateClient();
        var session = await RegisterAsync(owner);
        await api.DeliverOutboxAsync();

        // Simulate a crash after the projection ran but before the outbox row was marked done.
        await using (var scope = api.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await identity.Database.ExecuteSqlAsync(
                $"UPDATE identity.outbox_messages SET processed_at = NULL WHERE tenant_id = {session.TenantId}",
                TestContext.Current.CancellationToken);
        }

        await api.DeliverOutboxAsync();
        var page = await WorkspaceTimelineAsync(owner, session.TenantId);

        Assert.Single(page.Items, item => item.Type == "identity.workspace.created");
    }

    [Fact]
    public async Task Notes_PageNewestFirst_WithoutGapsOrRepeats()
    {
        using var owner = api.CreateClient();
        var session = await RegisterAsync(owner);
        await api.DeliverOutboxAsync();
        for (var number = 1; number <= 24; number++)
        {
            using var note = await owner.PostAsJsonAsync(NotesUri(session.TenantId), new { text = $"Not {number}" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        }

        var first = await WorkspaceTimelineAsync(owner, session.TenantId);
        var second = await WorkspaceTimelineAsync(owner, session.TenantId, first.NextCursor);

        Assert.Equal(20, first.Items.Count);
        Assert.Equal("Not 24", first.Items[0].Payload.GetProperty("text").GetString());
        Assert.Equal(5, second.Items.Count);
        Assert.Equal("identity.workspace.created", second.Items[^1].Type);
        Assert.Null(second.NextCursor);
        Assert.Equal(25, first.Items.Concat(second.Items).Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task Note_OnAnotherWorkspace_IsRefused()
    {
        using var mine = api.CreateClient();
        await RegisterAsync(mine);
        using var theirs = api.CreateClient();
        var other = await RegisterAsync(theirs);

        using var response = await mine.PostAsJsonAsync(NotesUri(other.TenantId), new { text = "sızma denemesi" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("timeline.subject.unknown", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Stream_HidesEntriesTheReaderMayNotSee()
    {
        using var owner = api.CreateClient();
        var session = await RegisterAsync(owner);
        await AddRoleAsync(session.TenantId, "viewer", ["timeline.read"]);

        var viewerEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(viewerEmail, role: "viewer");
        using var viewer = api.CreateClient();
        using var __ = await viewer.AcceptInvitationAsync(api.InvitationTokenFor(viewerEmail));
        await api.DeliverOutboxAsync();

        var ownerView = await WorkspaceTimelineAsync(owner, session.TenantId);
        var viewerView = await WorkspaceTimelineAsync(viewer, session.TenantId);

        Assert.Contains(ownerView.Items, item => item.Type == "identity.invitation.sent");
        Assert.DoesNotContain(viewerView.Items, item => item.Type == "identity.invitation.sent");
        Assert.Contains(viewerView.Items, item => item.Type == "identity.member.joined");
    }

    [Fact]
    public async Task Stream_WithoutTimelinePermission_IsForbidden()
    {
        using var owner = api.CreateClient();
        var session = await RegisterAsync(owner);
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(memberEmail);
        using var accepted = await api.CreateClient().AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));

        // Members read the timeline by default; take it away to see the check.
        await api.SetRolePermissionsAsync(session.TenantId, "member");
        using var member = api.CreateClient();
        using var __ = await member.LoginAsync(memberEmail);
        using var response = await member.GetAsync(StreamUri(session.TenantId), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Stream_OfAnotherTenant_IsEmptyEvenWithItsId()
    {
        using var victim = api.CreateClient();
        var victimSession = await RegisterAsync(victim);
        using var intruder = api.CreateClient();
        await RegisterAsync(intruder);
        await api.DeliverOutboxAsync();

        var page = await WorkspaceTimelineAsync(intruder, victimSession.TenantId);

        Assert.Empty(page.Items);
    }

    private static Uri StreamUri(Guid tenantId, string? before = null) =>
        new($"/api/v1/timeline/workspace/{tenantId}" + (before is null ? "" : $"?before={Uri.EscapeDataString(before)}"), UriKind.Relative);

    private static Uri NotesUri(Guid tenantId) => new($"/api/v1/timeline/workspace/{tenantId}/notes", UriKind.Relative);

    private static async Task<TimelinePageResponse> WorkspaceTimelineAsync(HttpClient client, Guid tenantId, string? before = null)
    {
        var page = await client.GetFromJsonAsync<TimelinePageResponse>(StreamUri(tenantId, before), TestContext.Current.CancellationToken);
        return page ?? throw new InvalidOperationException("Empty timeline response.");
    }

    private static async Task<SessionResponse> RegisterAsync(HttpClient client)
    {
        using var response = await client.RegisterAsync();
        return await response.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("Registration returned no session.");
    }

    private async Task AddRoleAsync(Guid tenantId, string name, string[] permissions)
    {
        await using var scope = api.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(TenantId.From(tenantId));
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        identity.Roles.Add(Role.CreateSystem(TenantId.From(tenantId), name, permissions));
        await identity.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
