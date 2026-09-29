using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Akiron.BuildingBlocks.Events;

/// <summary>
/// Delivers pending outbox messages of every module to their handlers. Rows are claimed with
/// <c>FOR UPDATE SKIP LOCKED</c>, so several API instances can run it side by side without
/// delivering the same message twice at the same moment. Delivery is still at-least-once: a crash
/// after a handler ran but before the row was marked processed re-delivers it.
/// </summary>
public sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IEnumerable<ModuleDatabase> databases,
    IntegrationEventRegistry registry,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 10;

    /// <summary>Processes one batch per module; returns how many messages were handled or failed.</summary>
    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        var handled = 0;
        foreach (var database in databases)
        {
            handled += await ProcessDatabaseAsync(database, cancellationToken);
        }

        return handled;
    }

    private async Task<int> ProcessDatabaseAsync(ModuleDatabase database, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(database.ContextType);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        // Only the schema name is interpolated, and it comes from code (ModuleDbContext.Schema),
        // never from input; identifiers cannot be passed as parameters. Values are parameters.
#pragma warning disable EF1002
        var batch = await db.OutboxMessages
            .FromSqlRaw(
                $$"""
                SELECT * FROM "{{database.Schema}}".outbox_messages
                WHERE processed_at IS NULL AND failed_at IS NULL AND next_attempt_at <= {0}
                ORDER BY occurred_at
                LIMIT {{BatchSize}}
                FOR UPDATE SKIP LOCKED
                """,
                now)
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002

        foreach (var message in batch)
        {
            try
            {
                await DeliverAsync(message, cancellationToken);
                message.MarkProcessed(timeProvider.GetUtcNow());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogDeliveryFailed(logger, exception, message.Type, message.Id);
                message.MarkFailed(exception.ToString(), timeProvider.GetUtcNow(), MaxAttempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return batch.Count;
    }

    private async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var integrationEvent = registry.Deserialize(message.Type, message.Payload)
            ?? throw new InvalidOperationException($"Unknown integration event type '{message.Type}'.");

        // Each delivery gets its own scope bound to the event's tenant, exactly like a request.
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(message.TenantId);

        var consumerType = typeof(IIntegrationEventConsumer<>).MakeGenericType(integrationEvent.GetType());
        foreach (var consumer in scope.ServiceProvider.GetServices(consumerType))
        {
            await (Task)consumerType.GetMethod(nameof(IIntegrationEventConsumer<IIntegrationEvent>.HandleAsync))!
                .Invoke(consumer, [integrationEvent, cancellationToken])!;
        }
    }

    [LoggerMessage(EventId = 3000, Level = LogLevel.Warning, Message = "Delivering outbox message {Type} {MessageId} failed")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, string type, Guid messageId);
}
