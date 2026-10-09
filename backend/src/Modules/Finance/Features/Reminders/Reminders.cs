using System.Globalization;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Email;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Scheduling;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Finance;
using Akiron.Contracts.Identity;
using Akiron.Modules.Finance.Domain;
using Akiron.Modules.Finance.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Akiron.Modules.Finance.Features.Reminders;

/// <param name="Email">Where to send it; empty uses the party's primary contact (or the party's own address).</param>
internal sealed record RemindCommand(string? Email = null);

internal sealed class RemindValidator : AbstractValidator<RemindCommand>
{
    public RemindValidator() =>
        RuleFor(command => command.Email!).EmailAddress().MaximumLength(254).When(command => !string.IsNullOrWhiteSpace(command.Email));
}

internal sealed record ReminderResponse(string Email, DateTimeOffset SentAt, int RemindersSent);

/// <summary>E-mails a client a polite reminder about one open instalment (what, how much, since when).</summary>
internal sealed class RemindersHandler(
    FinanceDbContext db,
    FinanceContext context,
    IPartyDirectory parties,
    IWorkspaceDirectory workspace,
    IEmailSender emailSender)
{
    public async Task<Result<ReminderResponse>> SendAsync(Guid openItemId, RemindCommand command, CancellationToken cancellationToken)
    {
        var id = OpenItemId.From(openItemId);
        var item = await db.OpenItems.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.IsOpen, cancellationToken);
        if (item is null)
        {
            return FinanceErrors.OpenItemNotFound;
        }

        var charge = await db.Charges.AsNoTracking().FirstAsync(candidate => candidate.Id == item.ChargeId, cancellationToken);
        var email = string.IsNullOrWhiteSpace(command.Email) ? await parties.BillingEmailAsync(item.PartyId, cancellationToken) : command.Email.Trim();
        if (email is null)
        {
            return Error.Validation("finance.reminder.no_email", "The party has no e-mail address; type one.");
        }

        var agency = await workspace.CurrentNameAsync(cancellationToken);
        var amount = item.Remaining.ToString("N2", CultureInfo.GetCultureInfo("tr-TR")) + " " + item.Currency.Code;
        var due = item.DueDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var overdue = item.DueDate < context.Today;
        await emailSender.SendAsync(
            new EmailMessage(
                email,
                overdue ? $"Ödeme hatırlatması: {charge.Number}" : $"Yaklaşan ödeme: {charge.Number}",
                $"""
                Merhaba,

                {charge.Description} kapsamındaki {amount} tutarındaki ödemenin vadesi {due}{(overdue ? " tarihinde doldu" : " tarihinde doluyor")}.
                Ödemeyi yaptıysanız bu e-postayı dikkate almayın; teşekkür ederiz.

                {agency}

                ---
                A friendly reminder: {amount} for "{charge.Description}" {(overdue ? "was" : "is")} due on {due}.
                If you have already paid, please ignore this e-mail. Thank you.
                """),
            cancellationToken);

        var now = context.Now;
        item.Reminded(now);
        db.Publish(new PaymentReminderSent(item.TenantId, now, item.PartyId, charge.Number, item.Remaining, item.Currency.Code, item.DueDate, email, context.UserId, context.UserName));
        await db.SaveChangesAsync(cancellationToken);
        return new ReminderResponse(email, now, item.RemindersSent);
    }
}

/// <summary>
/// Once a day, per tenant with money due: a digest for the people who handle collections (what falls
/// due today, what is late, what comes in the next three days). The event id is derived from the
/// tenant and the day, so hourly runs and several instances still notify once.
/// </summary>
internal sealed class PaymentDigestJob(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IScheduledJob
{
    public const int UpcomingDays = 3;

    public async Task RunAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(UpcomingDays);
        List<Guid> tenants;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            tenants = await db.OpenItems.IgnoreQueryFilters([QueryFilters.Tenant])
                .Where(item => item.IsOpen && item.DueDate <= horizon)
                .Select(item => item.TenantId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        foreach (var tenant in tenants)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var tenantId = TenantId.From(tenant);
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();

            var items = await db.OpenItems.AsNoTracking()
                .Where(item => item.IsOpen && item.DueDate <= horizon)
                .Select(item => new { item.DueDate, Remaining = item.Amount - item.Allocated, item.Currency })
                .ToListAsync(cancellationToken);

            static IReadOnlyList<DueAmount> Totals(IEnumerable<(Currency Currency, decimal Remaining)> rows) =>
                rows.GroupBy(row => row.Currency.Code).Select(group => new DueAmount(group.Key, group.Sum(row => row.Remaining))).ToList();

            var eventId = DeterministicId.From(string.Create(CultureInfo.InvariantCulture, $"finance.payments.digest:{tenant}:{today:yyyy-MM-dd}"));
            if (await db.OutboxMessages.AnyAsync(message => message.Id == eventId, cancellationToken))
            {
                continue;
            }

            var overdue = items.Where(item => item.DueDate < today).ToList();
            var soon = items.Where(item => item.DueDate >= today).ToList();
            db.Publish(new PaymentsDueDigest(
                tenantId,
                timeProvider.GetUtcNow(),
                today,
                items.Count(item => item.DueDate == today),
                overdue.Count,
                items.Count(item => item.DueDate > today),
                Totals(overdue.Select(item => (item.Currency, item.Remaining))),
                Totals(soon.Select(item => (item.Currency, item.Remaining))))
            {
                EventId = eventId,
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Today's digest for this tenant is already queued.
            }
        }
    }
}
