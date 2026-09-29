using System.Globalization;
using System.Net;
using Akiron.Modules.Reference.Domain;
using Akiron.Modules.Reference.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Akiron.Modules.Reference.Tcmb;

/// <summary>Downloads one bulletin; null when TCMB has none for that day (weekend, holiday, not yet published).</summary>
internal sealed class TcmbClient(HttpClient http)
{
    public async Task<TcmbBulletin?> GetBulletinAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"{date:yyyyMM}/{date:ddMMyyyy}.xml");
        using var response = await http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return TcmbBulletinParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }
}

/// <summary>
/// Keeps the last week of TCMB bulletins stored. Safe to run on several instances at once: a
/// bulletin already stored is skipped, and a concurrent insert of the same one is ignored.
/// </summary>
internal sealed partial class ExchangeRateSync(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ExchangeRateSync> logger)
{
    /// <summary>Turkey has used UTC+3 all year since 2016.</summary>
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    /// <summary>TCMB publishes at 15:30 Turkish time; allow a few minutes.</summary>
    private static readonly TimeSpan PublishedAfter = new(15, 40, 0);

    private const int DaysToKeepComplete = 7;

    /// <returns>How many bulletins were stored.</returns>
    public async Task<int> SyncAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReferenceDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<TcmbClient>();

        var turkeyNow = timeProvider.GetUtcNow().ToOffset(TurkeyOffset);
        var today = DateOnly.FromDateTime(turkeyNow.DateTime);
        var from = today.AddDays(-DaysToKeepComplete);

        var stored = await db.ExchangeRates
            .Where(rate => rate.BulletinDate >= from)
            .Select(rate => rate.BulletinDate)
            .Distinct()
            .ToListAsync(cancellationToken);

        var added = 0;
        for (var day = today; day > from; day = day.AddDays(-1))
        {
            var publishedYet = day < today || turkeyNow.TimeOfDay >= PublishedAfter;
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || !publishedYet || stored.Contains(day))
            {
                continue;
            }

            var bulletin = await client.GetBulletinAsync(day, cancellationToken);
            if (bulletin is null || stored.Contains(bulletin.Date))
            {
                continue;
            }

            if (await StoreAsync(db, bulletin, cancellationToken))
            {
                stored.Add(bulletin.Date);
                added++;
                LogStored(logger, bulletin.Date, bulletin.Rates.Count);
            }
        }

        return added;
    }

    private async Task<bool> StoreAsync(ReferenceDbContext db, TcmbBulletin bulletin, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        db.ExchangeRates.AddRange(bulletin.Rates.Select(rate => ExchangeRate.Create(
            bulletin.Date, rate.CurrencyCode, rate.ForexBuying, rate.ForexSelling, rate.BanknoteBuying, rate.BanknoteSelling, now)));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another instance stored the same bulletin a moment earlier.
            db.ChangeTracker.Clear();
            return false;
        }
    }

    [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Stored TCMB bulletin {BulletinDate} with {Count} rates")]
    private static partial void LogStored(ILogger logger, DateOnly bulletinDate, int count);
}

/// <summary>Runs <see cref="ExchangeRateSync"/> at start-up and then hourly.</summary>
internal sealed partial class ExchangeRateSyncService(ExchangeRateSync sync, ILogger<ExchangeRateSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await sync.SyncAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // TCMB or the network may be down; the next hour tries again.
                LogSyncFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Warning, Message = "Exchange rate sync failed")]
    private static partial void LogSyncFailed(ILogger logger, Exception exception);
}
