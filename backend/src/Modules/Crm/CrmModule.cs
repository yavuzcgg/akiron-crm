using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Scheduling;
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

    /// <summary>Defining the custom fields every party card shows.</summary>
    public const string CustomFieldsManage = "crm.custom_fields.manage";

    /// <summary>The lead funnel: seeing the board.</summary>
    public const string LeadsRead = "crm.leads.read";

    /// <summary>Adding, moving and converting leads, and the web form settings.</summary>
    public const string LeadsWrite = "crm.leads.write";

    public static IReadOnlyCollection<string> All { get; } = [PartiesRead, PartiesWrite, CustomFieldsManage, LeadsRead, LeadsWrite];

    /// <summary>Selling is everyday work: members run the funnel; clients' details stay with admins.</summary>
    public static IReadOnlyCollection<string> Member { get; } = [LeadsRead, LeadsWrite];
}

/// <summary>Counterparties (cari: customers and suppliers, ADR-0008), their contacts, and the lead funnel.</summary>
public sealed class CrmModule : IModule
{
    public string Name => CrmDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => CrmPermissions.All;

    public IReadOnlyCollection<string> MemberPermissions => CrmPermissions.Member;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CrmDbContext>(configuration, CrmDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(CrmModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<CrmModule>(includeInternalTypes: true);
        services.AddScoped<IPartyDirectory, PartyDirectory>();
        services.AddScoped<Features.CustomFields.CustomValuesCheck>();
        services.AddIntegrationEventConsumersFromAssembly(typeof(CrmModule).Assembly);
        services.AddScheduledJob<Features.Leads.LeadFollowUpJob>(configuration);
        services.Configure<ConstraintErrorMap>(map => map.Add(CrmConstraints.PartyCodeUnique, CrmErrors.CodeTaken));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        PartyEndpoints.Map(endpoints);
        Features.CustomFields.CustomFieldEndpoints.Map(endpoints);
        Features.Leads.LeadEndpoints.Map(endpoints);
    }
}
