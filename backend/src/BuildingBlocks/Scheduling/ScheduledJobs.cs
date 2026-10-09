using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akiron.BuildingBlocks.Scheduling;

/// <summary>
/// Work that happens because a day came, not because someone clicked: billing a retainer, reminding
/// about a due payment. Runs every hour; an implementation must be safe to run repeatedly on the same
/// day and on several instances at once (it records what it did, or keys its events deterministically).
/// </summary>
public interface IScheduledJob
{
    /// <param name="today">The business day in Türkiye; tests pass the day they want.</param>
    Task RunAsync(DateOnly today, CancellationToken cancellationToken);
}

/// <summary>The calendar the product runs on: Türkiye, UTC+3 all year since 2016.</summary>
public static class BusinessCalendar
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Istanbul).DateTime);
}

/// <summary>
/// A stable id derived from a key, for events that must exist at most once ("the digest of 9 Oct for
/// tenant X"): publishing it twice collides on the outbox's primary key instead of notifying twice.
/// </summary>
public static class DeterministicId
{
    public static Guid From(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, 16));
    }
}

public static class SchedulingRegistration
{
    /// <summary>Registers a job; the hourly runner starts unless <c>Scheduler:Enabled</c> is false (tests run jobs themselves).</summary>
    public static IServiceCollection AddScheduledJob<TJob>(this IServiceCollection services, IConfiguration configuration)
        where TJob : class, IScheduledJob
    {
        services.AddSingleton<IScheduledJob, TJob>();
        if (configuration.GetValue("Scheduler:Enabled", true))
        {
            services.AddHostedService<ScheduledJobsService>();
        }

        return services;
    }
}

/// <summary>Runs every <see cref="IScheduledJob"/> shortly after start-up and then hourly.</summary>
public sealed partial class ScheduledJobsService(
    IEnumerable<IScheduledJob> jobs,
    TimeProvider timeProvider,
    ILogger<ScheduledJobsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let migrations and role sync finish first.
        await Task.Delay(TimeSpan.FromSeconds(30), timeProvider, stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), timeProvider);
        do
        {
            var today = BusinessCalendar.Today(timeProvider);
            foreach (var job in jobs)
            {
                try
                {
                    await job.RunAsync(today, stoppingToken);
                }
                // A database timeout also surfaces as a cancellation; only the host shutting down may end the loop.
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    LogJobFailed(logger, exception, job.GetType().Name);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 3100, Level = LogLevel.Warning, Message = "Scheduled job {Job} failed; it runs again next hour")]
    private static partial void LogJobFailed(ILogger logger, Exception exception, string job);
}
