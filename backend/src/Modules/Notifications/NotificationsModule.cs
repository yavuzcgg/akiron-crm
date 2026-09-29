using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Modules.Notifications.Features;
using Akiron.Modules.Notifications.Persistence;
using Akiron.Modules.Notifications.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Notifications;

/// <summary>Per-person notifications with real-time delivery (SignalR); e-mail and WhatsApp channels plug in later.</summary>
public sealed class NotificationsModule : IModule
{
    public string Name => NotificationsDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => [];

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationsDbContext>(configuration, NotificationsDbContext.SchemaName);
        services.AddIntegrationEventConsumersFromAssembly(typeof(NotificationsModule).Assembly);
        services.AddScoped<NotificationSender>();
        services.AddSignalR();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        NotificationEndpoints.Map(endpoints);

        // Mapped from the root so the path is exactly NotificationHub.Path, under /api where the access cookie is sent.
        endpoints.MapHub<NotificationHub>("/hub");
    }
}
