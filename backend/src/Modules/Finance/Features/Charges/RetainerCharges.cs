using System.Globalization;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Contracts.Finance;
using Akiron.Contracts.Reference;
using Akiron.Contracts.Sales;
using Akiron.Modules.Finance.Domain;
using Akiron.Modules.Finance.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Akiron.Modules.Finance.Features.Charges;

/// <summary>
/// Turns a retainer month into a charge (BRC) with one instalment, posted to the party ledger.
/// Idempotent: a contract's month is charged once (unique index on contract and period), so a
/// redelivered event changes nothing.
/// </summary>
internal sealed class RetainerChargeConsumer(FinanceDbContext db, IExchangeRates exchangeRates, TimeProvider timeProvider)
    : IIntegrationEventConsumer<RetainerPeriodDue>
{
    public async Task HandleAsync(RetainerPeriodDue integrationEvent, CancellationToken cancellationToken)
    {
        if (await db.Charges.AnyAsync(charge => charge.ContractId == integrationEvent.ContractId && charge.Period == integrationEvent.PeriodStart, cancellationToken))
        {
            return;
        }

        var currency = Currency.From(integrationEvent.Currency);
        var rate = currency == Currency.Try
            ? 1m
            : integrationEvent.ExchangeRate
                ?? (await exchangeRates.FindAsync(currency, integrationEvent.PeriodStart.AddDays(-1), cancellationToken))?.ForexBuying
                ?? throw new InvalidOperationException($"No TCMB {currency.Code} rate before {integrationEvent.PeriodStart}; the delivery is retried.");

        var date = integrationEvent.PeriodStart;
        var description = string.Create(CultureInfo.InvariantCulture, $"{integrationEvent.Number} {integrationEvent.Title} · {date:yyyy-MM}");
        if (description.Length > Charge.DescriptionMaxLength)
        {
            description = description[..Charge.DescriptionMaxLength];
        }

        var now = timeProvider.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var sequence = await db.NextDocumentSequenceAsync(RecordChargeHandler.NumberSeries, date.Year, cancellationToken);
        var charge = Charge.Record(
            DocumentNumber.Format(RecordChargeHandler.NumberSeries, date.Year, sequence),
            ChargeKind.Sale,
            integrationEvent.PartyId,
            integrationEvent.PartyName,
            date,
            description,
            currency,
            rate,
            [new Instalment(integrationEvent.DueDate, integrationEvent.Amount)],
            quoteId: null,
            contractId: integrationEvent.ContractId,
            period: integrationEvent.PeriodStart);

        db.Charges.Add(charge);
        db.Ledger.Add(LedgerEntry.Post(charge.PartyId, date, LedgerSource.Charge, charge.Id.Value, charge.Number, charge.Description, charge.Amount, 0m, currency, charge.ExchangeRate, now));
        db.Publish(new ChargeRecorded(
            integrationEvent.TenantId, date, now, charge.Id.Value, charge.Number, charge.PartyId, charge.PartyName, charge.Description,
            charge.Amount, currency.Code, 1, null, null, null, integrationEvent.ContractId));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: FinanceConstraints.ContractPeriodUnique })
        {
            // A parallel delivery charged this month a moment ago.
        }
    }
}
