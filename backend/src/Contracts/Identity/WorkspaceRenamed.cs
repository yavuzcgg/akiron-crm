using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Identity;

/// <summary>The organisation's display name changed.</summary>
[IntegrationEventName("identity.workspace.renamed")]
public sealed record WorkspaceRenamed(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    string OldName,
    string NewName,
    Guid RenamedByUserId,
    string? RenamedByName) : IntegrationEvent(TenantId, OccurredAt);
