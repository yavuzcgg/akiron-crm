using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akiron.BuildingBlocks.Events;

/// <summary>
/// Runs <see cref="OutboxProcessor"/> continuously: straight again while there is work, otherwise
/// after <c>Outbox:PollingIntervalMs</c>. Hangfire is not needed for this; it arrives with the
/// first scheduled job (TCMB exchange rates).
/// </summary>
public sealed partial class OutboxDispatcherService(
    OutboxProcessor processor,
    IConfiguration configuration,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromMilliseconds(configuration.GetValue("Outbox:PollingIntervalMs", 500));

        while (!stoppingToken.IsCancellationRequested)
        {
            int handled;
            try
            {
                handled = await processor.ProcessAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The database may be briefly unavailable; keep the loop alive.
                LogCycleFailed(logger, exception);
                handled = 0;
            }

            if (handled == 0)
            {
                await Task.Delay(idleDelay, stoppingToken);
            }
        }
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Outbox dispatch cycle failed")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);
}
