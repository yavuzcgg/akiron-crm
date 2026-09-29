using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Files.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Files.Persistence;

internal sealed class FilesDbContext(DbContextOptions<FilesDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "files";

    public override string Schema => SchemaName;

    public DbSet<StoredFile> Files => Set<StoredFile>();
}

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("stored_files");
        builder.HasKey(file => file.Id);
        builder.Property(file => file.Id).ValueGeneratedNever();
        builder.Property(file => file.SubjectType).HasMaxLength(50);
        builder.Property(file => file.FileName).HasMaxLength(StoredFile.FileNameMaxLength);
        builder.Property(file => file.ContentType).HasMaxLength(200);
        builder.Property(file => file.StorageKey).HasMaxLength(200);
        builder.HasIndex(file => new { file.TenantId, file.SubjectType, file.SubjectId, file.CreatedAt });
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class FilesDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FilesDbContext>
{
    public FilesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, FilesDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new FilesDbContext(options, new TenantContext());
    }
}
