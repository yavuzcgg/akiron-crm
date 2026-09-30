using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.BuildingBlocks.Modules;

/// <summary>
/// A sellable unit of the product (ADR-0001). The host lists modules explicitly and calls these
/// in order; a module never references another module's project.
/// </summary>
public interface IModule
{
    /// <summary>Lower-case name: the Postgres schema, the route segment (<c>/api/v1/{name}</c>) and the permission prefix.</summary>
    string Name { get; }

    /// <summary>Every permission the module's endpoints can demand.</summary>
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>
    /// What the built-in Member role gets from this module: the day-to-day work every staff
    /// member does. Finance, clients and team management stay with owners and admins.
    /// </summary>
    IReadOnlyCollection<string> MemberPermissions => [];

    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps endpoints on a group already rooted at <c>/api/v1/{name}</c>.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
