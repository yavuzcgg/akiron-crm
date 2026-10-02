using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Akiron.BuildingBlocks.Tenancy;

/// <summary>
/// Binds the request scope to the tenant named in the access token. Runs after authentication;
/// anonymous requests stay unbound, so tenant-filtered queries return nothing for them.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Public links name their own tenant; a signed-in visitor's workspace must not take over.
        if (context.GetEndpoint()?.Metadata.GetMetadata<PublicLinkMetadata>() is not null)
        {
            return next(context);
        }

        var claim = context.User.FindFirst(AkironClaimTypes.TenantId)?.Value;

        if (Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty)
        {
            tenantContext.Bind(TenantId.From(tenantId));
        }

        return next(context);
    }
}

/// <summary>Marks an endpoint reached by a public link secret (quote approval, later payment pages).</summary>
public sealed class PublicLinkMetadata
{
    public static readonly PublicLinkMetadata Instance = new();
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();

    /// <summary>
    /// An anonymous endpoint whose link secret decides the tenant: the caller's own session (if
    /// any) is not bound, and the handler binds the tenant it finds behind the secret.
    /// </summary>
    public static TBuilder AsPublicLink<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AllowAnonymous().WithMetadata(PublicLinkMetadata.Instance);
}
