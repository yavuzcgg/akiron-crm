using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Modules.Timeline.Features;
using Akiron.Modules.Timeline.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Timeline;

public static class TimelinePermissions
{
    public const string Read = "timeline.read";
    public const string NotesWrite = "timeline.notes.write";

    public static IReadOnlyCollection<string> All { get; } = [Read, NotesWrite];
}

/// <summary>The activity timeline (ADR-0009): projects other modules' events into per-record streams.</summary>
public sealed class TimelineModule : IModule
{
    public string Name => TimelineDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => TimelinePermissions.All;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TimelineDbContext>(configuration, TimelineDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(TimelineModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<TimelineModule>(includeInternalTypes: true);
        services.AddIntegrationEventConsumersFromAssembly(typeof(TimelineModule).Assembly);
        services.AddScoped<TimelineWriter>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => TimelineEndpoints.Map(endpoints);
}
