namespace Akiron.BuildingBlocks.Web;

public static class RateLimitPolicies
{
    /// <summary>Per client IP, for login, registration and token refresh. Limit set by <c>RateLimiting:Auth:PermitLimit</c>.</summary>
    public const string Auth = "auth";
}
