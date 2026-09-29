using Akiron.Modules.Timeline.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Akiron.Modules.Timeline.Persistence;

/// <summary>
/// Writes an entry at most once per idempotency key, so re-delivered outbox messages are harmless.
/// </summary>
internal sealed class TimelineWriter(TimelineDbContext db)
{
    public async Task WriteAsync(TimelineEntry entry, CancellationToken cancellationToken)
    {
        if (await db.Entries.AnyAsync(existing => existing.IdempotencyKey == entry.IdempotencyKey, cancellationToken))
        {
            return;
        }

        db.Entries.Add(entry);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: TimelineEntryConfiguration.IdempotencyUnique,
        })
        {
            // A concurrent delivery of the same event won the race; the entry exists.
            db.ChangeTracker.Clear();
        }
    }
}
