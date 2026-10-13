using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Portal.Features;
using Akiron.Modules.Portal.Persistence;
using Akiron.Modules.Portal.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Portal;

public static class PortalPermissions
{
    /// <summary>Seeing which client contacts have portal access.</summary>
    public const string Read = "portal.read";

    /// <summary>Inviting contacts, switching access off, and the portal's greeting and payment text.</summary>
    public const string Manage = "portal.manage";

    public static IReadOnlyCollection<string> All { get; } = [Read, Manage];
}

/// <summary>
/// The client portal's accounts and sessions (ADR-0011): who of a client's people may sign in, the
/// password-less links, and the portal authentication scheme. What clients see is served by the
/// module that owns it (jobs, sales, finance, timeline, files) on endpoints marked RequirePortal().
/// </summary>
public sealed class PortalModule : IModule
{
    public string Name => PortalDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => PortalPermissions.All;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PortalDbContext>(configuration, PortalDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(PortalModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<PortalModule>(includeInternalTypes: true);
        services.AddIntegrationEventConsumersFromAssembly(typeof(PortalModule).Assembly);
        services.AddSingleton<PortalCookies>();
        services.AddScoped<PortalLinks>();
        services.Configure<ConstraintErrorMap>(map => map.Add(PortalConstraints.UserEmailUnique, PortalErrors.EmailInUse));

        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, PortalAuthenticationHandler>(PortalAuth.Scheme, null);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => PortalEndpoints.Map(endpoints);
}
