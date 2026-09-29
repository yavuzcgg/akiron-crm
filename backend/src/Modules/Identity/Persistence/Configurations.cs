using Akiron.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akiron.Modules.Identity.Persistence;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Id).ValueGeneratedNever();
        builder.Property(tenant => tenant.Name).HasMaxLength(Tenant.NameMaxLength);
        builder.Property(tenant => tenant.Slug).HasMaxLength(64);
        builder.HasIndex(tenant => tenant.Slug).IsUnique().HasDatabaseName(IdentityConstraints.TenantSlugUnique);
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.Email).HasMaxLength(User.EmailMaxLength);
        builder.Property(user => user.FullName).HasMaxLength(User.FullNameMaxLength);
        builder.Property(user => user.PasswordHash).HasMaxLength(256);
        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName(IdentityConstraints.UserEmailUnique);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).ValueGeneratedNever();
        builder.Property(role => role.Name).HasMaxLength(Role.NameMaxLength);
        builder.Property(role => role.Permissions).HasColumnType("text[]");
        builder.HasIndex(role => new { role.TenantId, role.Name }).IsUnique().HasDatabaseName(IdentityConstraints.RoleNameUnique);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(role => role.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).ValueGeneratedNever();
        builder.HasIndex(membership => new { membership.TenantId, membership.UserId })
            .IsUnique()
            .HasDatabaseName(IdentityConstraints.MembershipUnique);
        builder.HasIndex(membership => membership.UserId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(membership => membership.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(membership => membership.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(membership => membership.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasMaxLength(64);
        builder.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.FamilyId);
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal static class IdentityConstraints
{
    public const string TenantSlugUnique = "ix_tenants_slug";
    public const string UserEmailUnique = "ix_users_email";
    public const string RoleNameUnique = "ix_roles_tenant_id_name";
    public const string MembershipUnique = "ix_memberships_tenant_id_user_id";
}
