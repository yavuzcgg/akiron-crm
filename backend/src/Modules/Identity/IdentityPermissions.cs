namespace Akiron.Modules.Identity;

public static class IdentityPermissions
{
    public const string MembersRead = "identity.members.read";
    public const string MembersManage = "identity.members.manage";
    public const string RolesManage = "identity.roles.manage";
    public const string TenantManage = "identity.tenant.manage";

    public static IReadOnlyCollection<string> All { get; } = [MembersRead, MembersManage, RolesManage, TenantManage];
}
