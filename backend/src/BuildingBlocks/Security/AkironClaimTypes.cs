namespace Akiron.BuildingBlocks.Security;

/// <summary>
/// Claim names in the access token. Inbound claim mapping is off, so these arrive unchanged.
/// </summary>
public static class AkironClaimTypes
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string TenantId = "tenant_id";
    public const string Role = "role";

    /// <summary>One claim per granted permission; <see cref="PermissionNames.All"/> grants everything.</summary>
    public const string Permission = "perm";
}
