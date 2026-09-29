using Akiron.BuildingBlocks.Domain;

namespace Akiron.BuildingBlocks.Events;

/// <summary>
/// An integration event waiting for delivery, stored in the emitting module's own schema so it
/// commits or rolls back with the change that raised it.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage(Guid id, TenantId tenantId, string type, string payload, DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
        NextAttemptAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public string Type { get; private set; }

    /// <summary>JSON (<c>jsonb</c>).</summary>
    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Set when retries are exhausted; the row stays for inspection and manual replay.</summary>
    public DateTimeOffset? FailedAt { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage From(IIntegrationEvent integrationEvent) => new(
        integrationEvent.EventId,
        integrationEvent.TenantId,
        IntegrationEventSerializer.NameOf(integrationEvent.GetType()),
        IntegrationEventSerializer.Serialize(integrationEvent),
        integrationEvent.OccurredAt);

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset now, int maxAttempts)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;

        if (Attempts >= maxAttempts)
        {
            FailedAt = now;
            return;
        }

        // 2, 4, 8 … seconds, capped at ten minutes.
        NextAttemptAt = now + TimeSpan.FromSeconds(Math.Min(600, Math.Pow(2, Attempts)));
    }
}
