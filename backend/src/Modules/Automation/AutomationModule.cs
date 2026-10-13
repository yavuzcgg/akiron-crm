using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Modules.Automation.Features;
using Akiron.Modules.Automation.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Automation;

public static class AutomationPermissions
{
    /// <summary>Setting up rules that write to clients and the team on the organisation's behalf.</summary>
    public const string Manage = "automation.manage";

    public static IReadOnlyCollection<string> All { get; } = [Manage];
}

/// <summary>
/// Automation v1 (Phase 4): flat "when this happens, do that" rules over the business events the
/// other modules already publish. It adds no events to them and reads nothing of theirs but names
/// and contact details through Contracts.
/// </summary>
public sealed class AutomationModule : IModule
{
    public string Name => AutomationDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => AutomationPermissions.All;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AutomationDbContext>(configuration, AutomationDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(AutomationModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<AutomationModule>(includeInternalTypes: true);
        services.AddIntegrationEventConsumersFromAssembly(typeof(AutomationModule).Assembly);
        services.AddScoped<AutomationEngine>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => AutomationEndpoints.Map(endpoints);
}
