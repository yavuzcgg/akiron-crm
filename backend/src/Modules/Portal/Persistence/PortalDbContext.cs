using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Modules.Portal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Portal.Persistence;

internal sealed class PortalDbContext(DbContextOptions<PortalDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "portal";

    public override string Schema => SchemaName;

    public DbSet<PortalUser> Users => Set<PortalUser>();

    public DbSet<PortalSignInLink> SignInLinks => Set<PortalSignInLink>();

    public DbSet<PortalSession> Sessions => Set<PortalSession>();

    public DbSet<PortalSettings> Settings => Set<PortalSettings>();
}

internal static class PortalConstraints
{
    public const string UserEmailUnique = "ix_users_tenant_id_email";
}

internal sealed class PortalUserConfiguration : IEntityTypeConfiguration<PortalUser>
{
    public void Configure(EntityTypeBuilder<PortalUser> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.FullName).HasMaxLength(PortalUser.NameMaxLength);
        builder.Property(user => user.Email).HasMaxLength(254);
        builder.Property(user => user.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(user => new { user.TenantId, user.Email }).IsUnique().HasDatabaseName(PortalConstraints.UserEmailUnique);
        builder.HasIndex(user => new { user.TenantId, user.PartyId });

        // "Send me a link" looks an address up before any tenant is known.
        builder.HasIndex(user => user.Email);
    }
}

internal sealed class PortalSignInLinkConfiguration : IEntityTypeConfiguration<PortalSignInLink>
{
    public void Configure(EntityTypeBuilder<PortalSignInLink> builder)
    {
        builder.ToTable("sign_in_links");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.TokenHash).HasMaxLength(64);
        builder.HasIndex(link => link.TokenHash).IsUnique();
        builder.HasOne<PortalUser>().WithMany().HasForeignKey(link => link.PortalUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PortalSessionConfiguration : IEntityTypeConfiguration<PortalSession>
{
    public void Configure(EntityTypeBuilder<PortalSession> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(64);
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasOne<PortalUser>().WithMany().HasForeignKey(session => session.PortalUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PortalSettingsConfiguration : IEntityTypeConfiguration<PortalSettings>
{
    public void Configure(EntityTypeBuilder<PortalSettings> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.WelcomeNote).HasMaxLength(PortalSettings.TextMaxLength);
        builder.Property(settings => settings.PaymentInstructions).HasMaxLength(PortalSettings.TextMaxLength);
        builder.HasIndex(settings => settings.TenantId).IsUnique();
    }
}

/// <summary>For <c>dotnet ef</c> only; see IdentityDesignTimeDbContextFactory.</summary>
internal sealed class PortalDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5434;Database=akiron;Username=akiron;Password=akiron_dev",
                npgsql => npgsql.MigrationsHistoryTable(ModuleDatabaseRegistration.MigrationsHistoryTable, PortalDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PortalDbContext(options, new TenantContext());
    }
}
