using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Jobs.Domain;

public readonly record struct StageId(Guid Value) : ITypedId<StageId>
{
    public static StageId New() => new(Guid.CreateVersion7());

    public static StageId From(Guid value) => new(value);
}

public readonly record struct WorkOrderId(Guid Value) : ITypedId<WorkOrderId>
{
    public static WorkOrderId New() => new(Guid.CreateVersion7());

    public static WorkOrderId From(Guid value) => new(value);
}

public readonly record struct WorkOrderTaskId(Guid Value) : ITypedId<WorkOrderTaskId>
{
    public static WorkOrderTaskId New() => new(Guid.CreateVersion7());

    public static WorkOrderTaskId From(Guid value) => new(value);
}

public readonly record struct TimeEntryId(Guid Value) : ITypedId<TimeEntryId>
{
    public static TimeEntryId New() => new(Guid.CreateVersion7());

    public static TimeEntryId From(Guid value) => new(value);
}
