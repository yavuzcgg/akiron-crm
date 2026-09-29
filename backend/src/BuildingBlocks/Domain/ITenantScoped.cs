namespace Akiron.BuildingBlocks.Domain;

/// <summary>
/// Rows that belong to exactly one tenant. <c>ModuleDbContext</c> filters every query by the
/// current tenant, and <c>TenantAuditInterceptor</c> stamps new rows and rejects writes that would
/// cross tenants. Handlers never set or filter <see cref="TenantId"/> themselves (ADR-0002).
/// </summary>
public interface ITenantScoped
{
    TenantId TenantId { get; }
}
