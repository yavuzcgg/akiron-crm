using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.BuildingBlocks.Events;

/// <summary>Maps stored event names to CLR types and back, and serialises payloads.</summary>
public static class IntegrationEventSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static readonly ConcurrentDictionary<Type, string> Names = new();

    public static string NameOf(Type eventType) => Names.GetOrAdd(eventType, type =>
        type.GetCustomAttribute<IntegrationEventNameAttribute>()?.Name
        ?? throw new InvalidOperationException($"{type.Name} has no [IntegrationEventName]."));

    public static string Serialize(IIntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Options);
}

/// <summary>Every event type the host knows, found by scanning the contract assemblies once at startup.</summary>
public sealed class IntegrationEventRegistry
{
    private readonly Dictionary<string, Type> _types;

    public IntegrationEventRegistry(IEnumerable<Assembly> assemblies)
    {
        _types = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(type))
            .ToDictionary(IntegrationEventSerializer.NameOf, StringComparer.Ordinal);
    }

    public Type? Find(string name) => _types.GetValueOrDefault(name);

    public IIntegrationEvent? Deserialize(string name, string payload) =>
        Find(name) is { } type
            ? (IIntegrationEvent?)JsonSerializer.Deserialize(payload, type, IntegrationEventSerializer.Options)
            : null;
}

public static class IntegrationEventRegistration
{
    /// <summary>Registers every <see cref="IIntegrationEventConsumer{TEvent}"/> in the assembly as scoped.</summary>
    public static IServiceCollection AddIntegrationEventConsumersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var registrations = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IIntegrationEventConsumer<>))
                .Select(contract => (contract, type)));

        foreach (var (contract, implementation) in registrations)
        {
            services.AddScoped(contract, implementation);
        }

        return services;
    }
}

public static class OutboxRegistration
{
    /// <summary>
    /// The outbox machinery. <paramref name="contractAssemblies"/> hold the event types; the
    /// background dispatcher is skipped when <c>Outbox:Enabled</c> is false (tests drive
    /// <see cref="OutboxProcessor"/> themselves so they are deterministic).
    /// </summary>
    public static IServiceCollection AddOutbox(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        params Assembly[] contractAssemblies)
    {
        services.AddSingleton(new IntegrationEventRegistry(contractAssemblies));
        services.AddSingleton<OutboxProcessor>();

        if (Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue(configuration, "Outbox:Enabled", true))
        {
            services.AddHostedService<OutboxDispatcherService>();
        }

        return services;
    }
}
