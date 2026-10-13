using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Akiron.BuildingBlocks.Security;

/// <summary>
/// The client portal's own principal (ADR-0007, ADR-0011): a separate authentication scheme and
/// cookie, no staff permissions. A portal session never authenticates on a staff endpoint, and a
/// staff session never on a portal one.
/// </summary>
public static class PortalAuth
{
    public const string Scheme = "Portal";

    /// <summary>The authorization policy behind <see cref="PortalEndpointExtensions.RequirePortal{TBuilder}"/>.</summary>
    public const string Policy = "portal";

    /// <summary>
    /// Deliberately not <c>sub</c>: <see cref="ICurrentUser.UserId"/> stays null for portal requests,
    /// so a client is never mistaken for a member of staff in audit columns or handlers.
    /// </summary>
    public const string UserIdClaim = "portal_sub";

    /// <summary>The party (cari) the visitor belongs to; every portal query is scoped to it.</summary>
    public const string PartyIdClaim = "party_id";
}

/// <summary>Who is looking at the portal. Only valid on endpoints marked with <c>RequirePortal()</c>.</summary>
public interface IPortalVisitor
{
    Guid PortalUserId { get; }

    Guid PartyId { get; }

    string Name { get; }

    string Email { get; }
}

public sealed class HttpPortalVisitor(IHttpContextAccessor httpContextAccessor) : IPortalVisitor
{
    public Guid PortalUserId => Guid.Parse(Claim(PortalAuth.UserIdClaim));

    public Guid PartyId => Guid.Parse(Claim(PortalAuth.PartyIdClaim));

    public string Name => Claim(AkironClaimTypes.Name);

    public string Email => Claim(AkironClaimTypes.Email);

    private string Claim(string type) =>
        httpContextAccessor.HttpContext?.User.FindFirst(type)?.Value
        ?? throw new InvalidOperationException("This endpoint is not a portal endpoint; call RequirePortal() on it.");
}

/// <summary>Marks an endpoint of the client portal; the tenant is taken from the portal session.</summary>
public sealed class PortalEndpointMetadata
{
    public static readonly PortalEndpointMetadata Instance = new();
}

public static class PortalEndpointExtensions
{
    /// <summary>
    /// The endpoint serves a signed-in client: authenticated by the portal cookie only, scoped to
    /// their party through <see cref="IPortalVisitor"/>. Usage: <c>group.MapGet(...).RequirePortal();</c>
    /// </summary>
    public static TBuilder RequirePortal<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PortalAuth.Policy).WithMetadata(PortalEndpointMetadata.Instance);
}
