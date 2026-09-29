using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Persistence;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "identity";

    public override string Schema => SchemaName;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
}
