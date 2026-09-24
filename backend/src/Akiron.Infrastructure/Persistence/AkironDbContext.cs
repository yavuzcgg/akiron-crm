using Microsoft.EntityFrameworkCore;

namespace Akiron.Infrastructure.Persistence;

public class AkironDbContext(DbContextOptions<AkironDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AkironDbContext).Assembly);
    }
}
