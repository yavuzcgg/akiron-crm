namespace Akiron.BuildingBlocks.Domain;

public readonly record struct UserId(Guid Value) : ITypedId<UserId>
{
    public static UserId New() => new(Guid.CreateVersion7());

    public static UserId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
