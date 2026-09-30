using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Jobs.Domain;

/// <summary>What a stage means for reporting, whatever the tenant calls it.</summary>
public enum StageCategory
{
    /// <summary>Not started.</summary>
    Open,

    /// <summary>Being worked on, or waiting on someone (client review counts here).</summary>
    Active,

    /// <summary>Finished; moving a work order here completes it.</summary>
    Done,
}

/// <summary>
/// A column of the work order board. Every tenant starts with four system stages; it can rename,
/// add, reorder and remove them (the per-tenant workflow in MODULES.md).
/// </summary>
public sealed class Stage : Entity<StageId>, ITenantScoped, IAuditable
{
    public const int NameMaxLength = 60;

    private Stage(StageId id, string? key, string? name, StageCategory category, int position)
        : base(id)
    {
        Key = key;
        Name = name;
        Category = category;
        Position = position;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>
    /// Stable key of a built-in stage (<c>todo</c>, <c>in_progress</c>, <c>review</c>, <c>done</c>).
    /// While <see cref="Name"/> is null the client shows the translated key, so a Turkish and an
    /// English user each see their own word.
    /// </summary>
    public string? Key { get; private set; }

    /// <summary>The tenant's own name; null for an untouched built-in stage.</summary>
    public string? Name { get; private set; }

    public StageCategory Category { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static IReadOnlyList<Stage> Defaults() =>
    [
        new(StageId.New(), "todo", null, StageCategory.Open, 0),
        new(StageId.New(), "in_progress", null, StageCategory.Active, 1),
        new(StageId.New(), "review", null, StageCategory.Active, 2),
        new(StageId.New(), "done", null, StageCategory.Done, 3),
    ];

    public static Stage Create(string name, StageCategory category, int position) =>
        new(StageId.New(), null, name.Trim(), category, position);

    public void Update(string name, StageCategory category)
    {
        Name = name.Trim();
        Category = category;
    }

    public void MoveTo(int position) => Position = position;
}
