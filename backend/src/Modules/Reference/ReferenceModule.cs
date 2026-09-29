using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Contracts.Reference;
using Akiron.Modules.Reference.Features;
using Akiron.Modules.Reference.Persistence;
using Akiron.Modules.Reference.Tcmb;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Reference;

/// <summary>Reference data shared by all tenants: TCMB exchange rates (tax code lists and provinces later).</summary>
public sealed class ReferenceModule : IModule
{
    public string Name => ReferenceDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => [];

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ReferenceDbContext>(configuration, ReferenceDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(ReferenceModule).Assembly);
        services.AddScoped<IExchangeRates, ExchangeRateQuery>();

        services.AddHttpClient<TcmbClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["ExchangeRates:TcmbBaseUrl"] ?? "https://www.tcmb.gov.tr/kurlar/");
            client.Timeout = TimeSpan.FromSeconds(20);
        })
        .AddStandardResilienceHandler();

        services.AddSingleton<ExchangeRateSync>();
        if (configuration.GetValue("ExchangeRates:SyncEnabled", true))
        {
            services.AddHostedService<ExchangeRateSyncService>();
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => ExchangeRateEndpoints.Map(endpoints);
}
