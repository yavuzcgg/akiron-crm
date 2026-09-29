namespace Akiron.BuildingBlocks.Domain;

/// <summary>
/// Removing the entity through EF Core marks it deleted instead of deleting the row, and queries
/// hide it. Financial records do not use this: they are cancelled or reversed (ADR-0004).
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }
}
