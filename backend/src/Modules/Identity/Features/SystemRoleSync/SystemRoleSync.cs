using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Modules;
using Akiron.Modules.Identity.Domain;
using Akiron.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akiron.Modules.Identity.Features.SystemRoleSync;

/// <summary>
/// Brings every tenant's system roles (owner, admin, member) up to the templates of the modules
/// loaded now. Without it a module shipped after a tenant registered stays invisible to that
/// tenant's admins. Signed-in people get the new permissions at their next token refresh.
/// </summary>
internal sealed partial class SystemRoleSync(IdentityDbContext db, PermissionCatalog catalog, ILogger<SystemRoleSync> logger)
{
    /// <param name="onlyTenant">Limits the run to one tenant (tests); null means every tenant.</param>
    public async Task<int> SyncAsync(CancellationToken cancellationToken, TenantId? onlyTenant = null)
    {
        var templates = SystemRoles.Templates(catalog);

        // Across every tenant on purpose: this runs at start-up, before any request binds one.
        // Three small rows per tenant; read-only here, so nothing is tracked.
        var roles = await db.Roles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(role => role.IsSystem && (onlyTenant == null || role.TenantId == onlyTenant))
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var role in roles)
        {
            if (templates.TryGetValue(role.Name, out var permissions) && role.ReplacePermissions(permissions))
            {
                await db.Roles
                    .IgnoreQueryFilters()
                    .Where(candidate => candidate.Id == role.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.Permissions, role.Permissions), cancellationToken);
                updated++;
            }
        }

        if (updated > 0)
        {
            LogUpdated(logger, updated);
        }

        return updated;
    }

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Updated {Count} system roles to the current permission templates")]
    private static partial void LogUpdated(ILogger logger, int count);
}

/// <summary>Runs <see cref="SystemRoleSync"/> once when the host starts (after migrations).</summary>
internal sealed class SystemRoleSyncService(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SystemRoleSync>().SyncAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
