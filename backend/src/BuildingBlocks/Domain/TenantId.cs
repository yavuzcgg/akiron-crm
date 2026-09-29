namespace Akiron.BuildingBlocks.Domain;

public readonly record struct TenantId(Guid Value) : ITypedId<TenantId>
{
    public static readonly TenantId Empty = new(Guid.Empty);

    public static TenantId New() => new(Guid.CreateVersion7());

    public static TenantId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
