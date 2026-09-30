using System.Security.Claims;

namespace Akiron.BuildingBlocks.Security;

public static class PermissionClaims
{
    /// <summary>
    /// For handlers that show more to some callers (costs, reasons for leave): an exact grant or
    /// the owner's <c>*</c>. Endpoints still gate access with <c>RequirePermission</c>.
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        user.FindAll(AkironClaimTypes.Permission).Any(claim => claim.Value == permission || claim.Value == PermissionNames.All);
}
