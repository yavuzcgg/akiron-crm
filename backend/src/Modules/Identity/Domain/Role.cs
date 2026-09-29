using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Identity.Domain;

/// <summary>A named set of permissions inside one tenant (ADR-0007). System roles are created at registration and cannot be deleted.</summary>
public sealed class Role : Entity<RoleId>, ITenantScoped, IAuditable
{
    public const int NameMaxLength = 100;

    private Role(RoleId id, TenantId tenantId, string name, IReadOnlyList<string> permissions, bool isSystem)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Permissions = permissions;
        IsSystem = isSystem;
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public IReadOnlyList<string> Permissions { get; private set; }

    public bool IsSystem { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static Role CreateSystem(TenantId tenantId, string name, IEnumerable<string> permissions) =>
        new(RoleId.New(), tenantId, name, permissions.Distinct(StringComparer.Ordinal).ToList(), isSystem: true);
}
