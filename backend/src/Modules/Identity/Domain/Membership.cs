using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Identity.Domain;

/// <summary>Links a user to a tenant with one role.</summary>
public sealed class Membership : Entity<MembershipId>, ITenantScoped, IAuditable
{
    private Membership(MembershipId id, TenantId tenantId, UserId userId, RoleId roleId)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        RoleId = roleId;
        IsActive = true;
    }

    public TenantId TenantId { get; private set; }

    public UserId UserId { get; private set; }

    public RoleId RoleId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static Membership Create(TenantId tenantId, UserId userId, RoleId roleId) =>
        new(MembershipId.New(), tenantId, userId, roleId);
}
