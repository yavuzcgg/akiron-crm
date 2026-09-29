using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Akiron.Modules.Notifications.Features;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Akiron.Tests.Integration.Notifications;

public sealed class NotificationTests(ApiFixture api)
{
    private static readonly Uri InboxUri = new("/api/v1/notifications", UriKind.Relative);

    [Fact]
    public async Task InvitationAccepted_NotifiesOnlyTheInviter()
    {
        using var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var invite = await owner.InviteAsync(memberEmail);
        using var member = api.CreateClient();
        using var __ = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));

        await api.DeliverOutboxAsync();
        var ownerInbox = await owner.GetFromJsonAsync<NotificationListResponse>(InboxUri, TestContext.Current.CancellationToken);
        var memberInbox = await member.GetFromJsonAsync<NotificationListResponse>(InboxUri, TestContext.Current.CancellationToken);

        var notification = Assert.Single(ownerInbox!.Items);
        Assert.Equal("identity.invitation.accepted", notification.Type);
        Assert.Equal("Ayşe Yılmaz", notification.Payload.GetProperty("memberName").GetString());
        Assert.Equal(1, ownerInbox.UnreadCount);
        Assert.Empty(memberInbox!.Items);
    }

    [Fact]
    public async Task ReadAll_ClearsTheUnreadCount()
    {
        using var owner = await OwnerWithOneNotificationAsync();

        using var readAll = await owner.PostAsync(new Uri("/api/v1/notifications/read-all", UriKind.Relative), null, TestContext.Current.CancellationToken);
        var inbox = await owner.GetFromJsonAsync<NotificationListResponse>(InboxUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode);
        Assert.Equal(0, inbox!.UnreadCount);
        Assert.NotNull(inbox.Items[0].ReadAt);
    }

    [Fact]
    public async Task OpenTab_ReceivesTheNotificationThroughSignalR()
    {
        using var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync();
        var accessToken = registered.SetCookieValue("akiron_access");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "/api/v1/notifications/hub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = $"akiron_access={accessToken}";
            })
            .Build();

        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("notification", payload => received.TrySetResult(payload));
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(memberEmail);
        using var member = api.CreateClient();
        using var __ = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));
        await api.DeliverOutboxAsync();

        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal("identity.invitation.accepted", pushed.GetProperty("type").GetString());
    }

    private async Task<HttpClient> OwnerWithOneNotificationAsync()
    {
        var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var __ = await owner.InviteAsync(memberEmail);
        using var member = api.CreateClient();
        using var ___ = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));
        await api.DeliverOutboxAsync();
        return owner;
    }
}
