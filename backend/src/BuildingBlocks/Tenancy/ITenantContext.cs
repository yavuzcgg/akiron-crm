using Akiron.BuildingBlocks.Domain;

namespace Akiron.BuildingBlocks.Tenancy;

/// <summary>
/// The tenant the current scope works for. Set once per request from the access token, or
/// explicitly by flows that have no token yet (registration, public links, webhooks).
/// </summary>
public interface ITenantContext
{
    bool HasTenant { get; }

    /// <summary>The current tenant; throws when <see cref="HasTenant"/> is false.</summary>
    TenantId TenantId { get; }

    /// <summary>Binds the scope to a tenant. Rebinding to a different tenant throws.</summary>
    void Bind(TenantId tenantId);
}
