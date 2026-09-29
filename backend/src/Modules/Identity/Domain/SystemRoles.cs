using Akiron.BuildingBlocks.Domain;
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

    /// <param name="allPermissions">Every permission of every loaded module.</param>
    public static IReadOnlyList<Role> CreateFor(TenantId tenantId, IEnumerable<string> allPermissions) =>
    [
        Role.CreateSystem(tenantId, Owner, [PermissionNames.All]),
        Role.CreateSystem(tenantId, Admin, allPermissions.Where(permission => !OwnerOnly.Contains(permission))),

        // Deliberately empty: each tenant decides what staff may see (finance, for instance).
        Role.CreateSystem(tenantId, Member, []),
    ];
}
