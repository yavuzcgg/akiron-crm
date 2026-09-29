using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Akiron.BuildingBlocks.Tenancy;

/// <summary>
/// Binds the request scope to the tenant named in the access token. Runs after authentication;
/// anonymous requests stay unbound, so tenant-filtered queries return nothing for them.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var claim = context.User.FindFirst(AkironClaimTypes.TenantId)?.Value;

        if (Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty)
        {
            tenantContext.Bind(TenantId.From(tenantId));
        }

        return next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
