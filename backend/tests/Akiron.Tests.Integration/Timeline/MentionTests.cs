using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Akiron.Modules.Identity.Features.Sessions;

namespace Akiron.Tests.Integration.Timeline;

public sealed class MentionTests(ApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Note_MentioningPeople_NotifiesThemButNotTheAuthorOrStrangers()
    {
        using var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync();
        var session = (await registered.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!;
        var email = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(email);
        using var member = api.CreateClient();
        using var accepted = await member.AcceptInvitationAsync(api.InvitationTokenFor(email));
        var memberId = (await accepted.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!.UserId;
        using var stranger = api.CreateClient();
        using var strangerSession = await stranger.RegisterAsync();
        var strangerId = (await strangerSession.Content.ReadFromJsonAsync<SessionResponse>(Cancel))!.UserId;

        var mentionable = await member.GetFromJsonAsync<JsonElement>("/api/v1/timeline/mentionable", Cancel);
        using var note = await owner.PostAsJsonAsync(
            $"/api/v1/timeline/workspace/{session.TenantId}/notes",
            new { text = "@Ayşe Yılmaz sunumu cumaya yetiştirebilir miyiz?", mentionedUserIds = new[] { memberId, session.UserId, strangerId } },
            Cancel);
        await api.DeliverOutboxAsync();
        var created = await note.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        var memberInbox = await member.GetStringAsync("/api/v1/notifications", Cancel);
        var ownerInbox = await owner.GetStringAsync("/api/v1/notifications", Cancel);
        var strangerInbox = await stranger.GetStringAsync("/api/v1/notifications", Cancel);

        Assert.Equal(2, mentionable.GetArrayLength());
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        Assert.Equal(2, created.GetProperty("payload").GetProperty("mentions").GetArrayLength());
        Assert.Contains("timeline.note.mentioned", memberInbox, StringComparison.Ordinal);
        Assert.DoesNotContain("timeline.note.mentioned", ownerInbox, StringComparison.Ordinal);
        Assert.DoesNotContain("timeline.note.mentioned", strangerInbox, StringComparison.Ordinal);
    }
}
