using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Identity.Domain;

public readonly record struct MembershipId(Guid Value) : ITypedId<MembershipId>
{
    public static MembershipId New() => new(Guid.CreateVersion7());

    public static MembershipId From(Guid value) => new(value);
}

public readonly record struct RoleId(Guid Value) : ITypedId<RoleId>
{
    public static RoleId New() => new(Guid.CreateVersion7());

    public static RoleId From(Guid value) => new(value);
}

public readonly record struct RefreshTokenId(Guid Value) : ITypedId<RefreshTokenId>
{
    public static RefreshTokenId New() => new(Guid.CreateVersion7());

    public static RefreshTokenId From(Guid value) => new(value);
}

public readonly record struct PasswordResetTokenId(Guid Value) : ITypedId<PasswordResetTokenId>
{
    public static PasswordResetTokenId New() => new(Guid.CreateVersion7());

    public static PasswordResetTokenId From(Guid value) => new(value);
}

public readonly record struct InvitationId(Guid Value) : ITypedId<InvitationId>
{
    public static InvitationId New() => new(Guid.CreateVersion7());

    public static InvitationId From(Guid value) => new(value);
}
