using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Identity;
using Akiron.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Features.WorkspaceDirectory;

internal sealed class WorkspaceDirectory(IdentityDbContext db, ITenantContext tenantContext) : IWorkspaceDirectory
{
    public async Task<string> CurrentNameAsync(CancellationToken cancellationToken) =>
        await db.Tenants.Where(tenant => tenant.Id == tenantContext.TenantId).Select(tenant => tenant.Name).SingleAsync(cancellationToken);
}
