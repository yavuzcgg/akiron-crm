using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Security;

namespace Akiron.Modules.Identity.Domain;

/// <summary>The roles every tenant starts with. Names are stable keys; the client translates them.</summary>
public static class SystemRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";

    /// <summary>Permissions only the owner holds: closing or transferring the organisation.</summary>
    private static readonly HashSet<string> OwnerOnly = new(StringComparer.Ordinal)
    {
        IdentityPermissions.TenantManage,
    };

    /// <summary>
    /// What each system role holds with the modules loaded now. Recomputed at every start
    /// (SystemRoleSync), so a module added later reaches tenants created before it.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Templates(PermissionCatalog catalog) =>
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Owner] = [PermissionNames.All],
            [Admin] = catalog.All.Where(permission => !OwnerOnly.Contains(permission)).ToList(),
            [Member] = catalog.MemberDefaults,
        };

    public static IReadOnlyList<Role> CreateFor(TenantId tenantId, PermissionCatalog catalog) =>
        Templates(catalog).Select(template => Role.CreateSystem(tenantId, template.Key, template.Value)).ToList();
}
