namespace Akiron.BuildingBlocks.Web;

/// <summary>Error codes that do not belong to one module. Module codes live in each module's <c>*Errors</c> class.</summary>
public static class CommonErrorCodes
{
    public const string ValidationFailed = "common.validation.failed";
    public const string MalformedRequest = "common.request.malformed";
    public const string Unauthenticated = "common.auth.unauthenticated";
    public const string Forbidden = "common.auth.forbidden";
    public const string Conflict = "common.conflict";
    public const string RateLimited = "common.rate_limited";
    public const string Unexpected = "common.unexpected";
}
