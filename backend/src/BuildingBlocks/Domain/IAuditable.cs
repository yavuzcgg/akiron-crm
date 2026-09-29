namespace Akiron.BuildingBlocks.Domain;

/// <summary>Creation and last-change stamps, written by <c>TenantAuditInterceptor</c>.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    UserId? CreatedBy { get; }

    DateTimeOffset? UpdatedAt { get; }

    UserId? UpdatedBy { get; }
}
