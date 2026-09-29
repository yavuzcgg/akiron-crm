namespace Akiron.BuildingBlocks.Domain;

/// <summary>
/// Base for aggregate roots and child entities. State changes go through methods on the derived
/// type; setters stay private so EF Core can materialise rows but callers cannot bypass rules.
/// </summary>
public abstract class Entity<TId>
    where TId : struct
{
    protected Entity(TId id) => Id = id;

    public TId Id { get; private set; }
}
