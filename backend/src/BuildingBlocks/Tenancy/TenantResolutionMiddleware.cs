using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Microsoft.AspNetCore.Authentication;
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
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var endpoint = context.GetEndpoint();

        // Public links name their own tenant; a signed-in visitor's workspace must not take over.
        if (endpoint?.Metadata.GetMetadata<PublicLinkMetadata>() is not null)
        {
            await next(context);
            return;
        }

        // Portal endpoints belong to the client's session; a staff session open in the same
        // browser must not decide the tenant (ADR-0011).
        var principal = context.User;
        if (endpoint?.Metadata.GetMetadata<PortalEndpointMetadata>() is not null)
        {
            var portal = await context.AuthenticateAsync(PortalAuth.Scheme);
            if (portal.Principal is null)
            {
                await next(context);
                return;
            }

            principal = portal.Principal;
        }

        var claim = principal.FindFirst(AkironClaimTypes.TenantId)?.Value;

        if (Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty)
        {
            tenantContext.Bind(TenantId.From(tenantId));
        }

        await next(context);
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
