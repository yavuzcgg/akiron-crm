namespace Akiron.BuildingBlocks.Security;

public static class PermissionNames
{
    /// <summary>Granted to the tenant owner: every permission, including ones added later.</summary>
    public const string All = "*";

    internal const string PolicyPrefix = "perm:";
}
