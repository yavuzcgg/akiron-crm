using Akiron.BuildingBlocks.Domain;

namespace Akiron.BuildingBlocks.Tenancy;

public sealed class TenantContext : ITenantContext
{
    private TenantId? _tenantId;

    public bool HasTenant => _tenantId.HasValue;

    public TenantId TenantId => _tenantId
        ?? throw new InvalidOperationException("No tenant is bound to this scope.");

    public void Bind(TenantId tenantId)
    {
        if (tenantId == TenantId.Empty)
        {
            throw new ArgumentException("The empty tenant id cannot be bound.", nameof(tenantId));
        }

        // A scope that switched tenants halfway could read one tenant's rows and write them
        // under another's id; refuse rather than guess which one was meant.
        if (_tenantId.HasValue && _tenantId.Value != tenantId)
        {
            throw new InvalidOperationException("This scope is already bound to a different tenant.");
        }

        _tenantId = tenantId;
    }
}
