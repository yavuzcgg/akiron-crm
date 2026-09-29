using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.BuildingBlocks.Modules;

public static class HandlerRegistration
{
    /// <summary>
    /// Registers every concrete class whose name ends in <c>Handler</c> as scoped, so endpoints can
    /// inject use cases directly (ADR-0003: no mediator).
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var handlers = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                           && type.Name.EndsWith("Handler", StringComparison.Ordinal));

        foreach (var handler in handlers)
        {
            services.AddScoped(handler);
        }

        return services;
    }
}
