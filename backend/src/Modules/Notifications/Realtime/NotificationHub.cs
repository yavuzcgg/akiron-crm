using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Akiron.Modules.Notifications.Realtime;

/// <summary>
/// Pushes new notifications to open browser tabs. Clients only listen; everything they do goes
/// through the REST endpoints. One group per tenant and user, because a person signed into two
/// organisations must only get each organisation's notifications in its own session.
/// </summary>
[Authorize]
internal sealed class NotificationHub : Hub
{
    public const string Path = "/api/v1/notifications/hub";
    public const string NotificationMethod = "notification";

    public static string GroupOf(TenantId tenantId, Guid userId) => $"{tenantId.Value:N}:{userId:N}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (Guid.TryParse(user?.FindFirst(AkironClaimTypes.TenantId)?.Value, out var tenant)
            && Guid.TryParse(user?.FindFirst(AkironClaimTypes.Subject)?.Value, out var subject))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(TenantId.From(tenant), subject));
        }

        await base.OnConnectedAsync();
    }
}
