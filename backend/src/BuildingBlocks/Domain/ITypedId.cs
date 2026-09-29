namespace Akiron.BuildingBlocks.Domain;

/// <summary>
/// A Guid identifier wrapped in its own type, so a <c>CustomerId</c> can never be passed where an
/// <c>InvoiceId</c> is expected. Typed ids live in the domain and persistence layers only; API
/// contracts expose plain <see cref="Guid"/> values.
/// </summary>
/// <remarks>
/// Every implementation is picked up by <c>ModuleDbContext</c> and stored as a <c>uuid</c> column
/// without per-property configuration.
/// </remarks>
public interface ITypedId<TSelf>
    where TSelf : struct, ITypedId<TSelf>
{
    Guid Value { get; }

    static abstract TSelf From(Guid value);
}
