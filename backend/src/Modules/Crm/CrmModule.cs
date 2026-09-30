using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Crm;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Crm.Features.PartyDirectory;
using Akiron.Modules.Crm.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Crm;

public static class CrmPermissions
{
    public const string PartiesRead = "crm.parties.read";
    public const string PartiesWrite = "crm.parties.write";

    public static IReadOnlyCollection<string> All { get; } = [PartiesRead, PartiesWrite];
}

/// <summary>Counterparties (cari: customers and suppliers, ADR-0008) and their contacts.</summary>
public sealed class CrmModule : IModule
{
    public string Name => CrmDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => CrmPermissions.All;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CrmDbContext>(configuration, CrmDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(CrmModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<CrmModule>(includeInternalTypes: true);
        services.AddScoped<IPartyDirectory, PartyDirectory>();
        services.Configure<ConstraintErrorMap>(map => map.Add(CrmConstraints.PartyCodeUnique, CrmErrors.CodeTaken));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => PartyEndpoints.Map(endpoints);
}
