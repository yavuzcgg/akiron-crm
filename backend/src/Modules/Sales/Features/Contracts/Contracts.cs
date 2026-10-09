using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Sales;
using Akiron.Modules.Sales.Domain;
using Akiron.Modules.Sales.Features.Quotes;
using Akiron.Modules.Sales.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Sales.Features.Contracts;

/// <param name="FirstBillingDate">Months starting before this day are not billed (already invoiced outside Akiron).</param>
/// <param name="ExchangeRate">A fixed rate for a foreign-currency contract; empty takes TCMB of each billing day.</param>
internal sealed record CreateContractCommand(
    Guid? PartyId,
    string Title,
    decimal Amount,
    DateOnly StartDate,
    string Currency = "TRY",
    decimal? ExchangeRate = null,
    DateOnly? EndDate = null,
    bool AutoRenew = false,
    int NoticeDays = 30,
    int PaymentTermDays = 0,
    string? Scope = null,
    Guid? QuoteId = null,
    DateOnly? FirstBillingDate = null);

internal sealed record UpdateContractCommand(
    string Title,
    decimal Amount,
    DateOnly? EndDate = null,
    bool AutoRenew = false,
    int NoticeDays = 30,
    int PaymentTermDays = 0,
    string? Scope = null);

internal sealed record EndContractCommand(string Reason);

internal sealed class CreateContractValidator : AbstractValidator<CreateContractCommand>
{
    public CreateContractValidator()
    {
        RuleFor(command => command.PartyId).NotEmpty().When(command => command.QuoteId is null);
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Contract.TitleMaxLength);
        RuleFor(command => command.Scope).MaximumLength(Contract.ScopeMaxLength);
        RuleFor(command => command.Amount).GreaterThan(0m).LessThanOrEqualTo(1_000_000_000m);
        RuleFor(command => command.Currency).Must(Currency.IsSupported).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.ExchangeRate).GreaterThan(0m);
        RuleFor(command => command.EndDate).GreaterThan(command => command.StartDate);
        RuleFor(command => command.FirstBillingDate).GreaterThanOrEqualTo(command => command.StartDate);
        RuleFor(command => command.NoticeDays).InclusiveBetween(0, 365);
        RuleFor(command => command.PaymentTermDays).InclusiveBetween(0, 180);
    }
}

internal sealed class UpdateContractValidator : AbstractValidator<UpdateContractCommand>
{
    public UpdateContractValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Contract.TitleMaxLength);
        RuleFor(command => command.Scope).MaximumLength(Contract.ScopeMaxLength);
        RuleFor(command => command.Amount).GreaterThan(0m).LessThanOrEqualTo(1_000_000_000m);
        RuleFor(command => command.NoticeDays).InclusiveBetween(0, 365);
        RuleFor(command => command.PaymentTermDays).InclusiveBetween(0, 180);
    }
}

internal sealed class EndContractValidator : AbstractValidator<EndContractCommand>
{
    public EndContractValidator() => RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
}

internal sealed record ContractFilter(
    [FromQuery(Name = "status")] string? Status = null,
    [FromQuery(Name = "partyId")] Guid? PartyId = null,
    [FromQuery(Name = "search")] string? Search = null);

internal sealed class ContractFilterValidator : AbstractValidator<ContractFilter>
{
    public ContractFilterValidator()
    {
        RuleFor(filter => filter.Status).Must(status => status is null or "active" or "paused" or "ended").WithErrorCode("validation.invalid_value");
        RuleFor(filter => filter.Search).MaximumLength(100);
    }
}

internal sealed record ContractResponse(
    Guid Id,
    string Number,
    string Title,
    string? Scope,
    Guid PartyId,
    string PartyName,
    string Status,
    decimal Amount,
    string Currency,
    decimal? ExchangeRate,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool AutoRenew,
    int NoticeDays,
    int PaymentTermDays,
    DateOnly? NextBillingDate,
    int BilledPeriods,
    DateOnly? LastBilledOn,
    Guid? QuoteId,
    DateOnly? EndedOn,
    string? EndReason,
    DateTimeOffset CreatedAt);

internal static class ContractViews
{
    public static ContractResponse ToResponse(Contract contract) => new(
        contract.Id.Value,
        contract.Number,
        contract.Title,
        contract.Scope,
        contract.PartyId,
        contract.PartyName,
        contract.Status.ToString().ToLowerInvariant(),
        contract.Amount,
        contract.Currency.Code,
        contract.ExchangeRate,
        contract.StartDate,
        contract.EndDate,
        contract.AutoRenew,
        contract.NoticeDays,
        contract.PaymentTermDays,
        contract.NextBillingDate,
        contract.BilledPeriods,
        contract.LastBilledOn,
        contract.QuoteId,
        contract.EndedOn,
        contract.EndReason,
        contract.CreatedAt);

    /// <summary>Queues one charge per billed month for Finance (RetainerPeriodDue); call before SaveChanges.</summary>
    public static void PublishPeriods(SalesDbContext db, Akiron.BuildingBlocks.Domain.TenantId tenantId, Contract contract, IEnumerable<BillingPeriod> periods, DateTimeOffset now)
    {
        foreach (var period in periods)
        {
            db.Publish(new RetainerPeriodDue(
                tenantId, now, contract.Id.Value, contract.Number, contract.Title, contract.PartyId, contract.PartyName,
                period.Start, period.DueDate, contract.Amount, contract.Currency.Code, contract.ExchangeRate));
        }
    }
}

/// <summary>Contracts and retainers: start one (from scratch or an accepted quote), change its terms, pause, resume, end.</summary>
internal sealed class ContractsHandler(SalesDbContext db, IPartyDirectory parties, IAcceptedQuotes quotes, ITenantContext tenantContext, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public const string NumberSeries = "SZL";

    public async Task<PagedResult<ContractResponse>> ListAsync(PageRequest page, ContractFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Contracts.AsNoTracking();
        if (filter.Status is { } status)
        {
            var wanted = Enum.Parse<ContractStatus>(status, ignoreCase: true);
            query = query.Where(contract => contract.Status == wanted);
        }

        if (filter.PartyId is { } partyId)
        {
            query = query.Where(contract => contract.PartyId == partyId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = "%" + filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(contract => EF.Functions.ILike(contract.Title, pattern, "\\")
                || EF.Functions.ILike(contract.Number, pattern, "\\")
                || EF.Functions.ILike(contract.PartyName, pattern, "\\"));
        }

        var result = await query
            .OrderBy(contract => contract.Status)
            .ThenByDescending(contract => contract.CreatedAt)
            .ToPagedResultAsync(page, cancellationToken);
        return new PagedResult<ContractResponse>(result.Items.Select(ContractViews.ToResponse).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<Result<ContractResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var contract = await FindAsync(id, cancellationToken);
        return contract is null ? ContractErrors.NotFound : ContractViews.ToResponse(contract);
    }

    /// <summary>Starts a contract; months already due (a start date of today or earlier) are billed right away.</summary>
    public async Task<Result<ContractResponse>> CreateAsync(CreateContractCommand command, CancellationToken cancellationToken)
    {
        Guid partyId;
        string partyName;
        if (command.QuoteId is { } quoteId)
        {
            var quote = await quotes.FindAsync(quoteId, cancellationToken);
            if (quote is null)
            {
                return ContractErrors.QuoteNotAccepted;
            }

            (partyId, partyName) = (quote.PartyId, quote.PartyName);
        }
        else
        {
            var party = await parties.FindAsync(command.PartyId!.Value, cancellationToken);
            if (party is null)
            {
                return ContractErrors.PartyNotFound;
            }

            (partyId, partyName) = (party.PartyId, party.Name);
        }

        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        var today = SalesCalendar.Today(timeProvider);
        var now = timeProvider.GetUtcNow();
        var currency = Currency.From(command.Currency);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var sequence = await db.NextDocumentSequenceAsync(NumberSeries, command.StartDate.Year, cancellationToken);
        var contract = Contract.Create(
            DocumentNumber.Format(NumberSeries, command.StartDate.Year, sequence),
            partyId,
            partyName,
            command.StartDate,
            currency,
            command.ExchangeRate is { } rate ? decimal.Round(rate, 6) : null,
            new ContractTerms(command.Title, command.Scope, Round(command.Amount), command.PaymentTermDays, command.EndDate, command.AutoRenew, command.NoticeDays),
            command.QuoteId,
            command.FirstBillingDate);

        db.Contracts.Add(contract);
        db.Publish(new ContractStarted(
            tenantContext.TenantId, now, contract.Id.Value, contract.Number, contract.Title, partyId, partyName,
            contract.Amount, currency.Code, contract.StartDate, contract.EndDate, command.QuoteId, userId.Value, currentUser.DisplayName));
        ContractViews.PublishPeriods(db, tenantContext.TenantId, contract, contract.BillDue(today), now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ContractViews.ToResponse(contract);
    }

    public async Task<Result<ContractResponse>> UpdateAsync(Guid id, UpdateContractCommand command, CancellationToken cancellationToken)
    {
        var contract = await FindAsync(id, cancellationToken);
        if (contract is null)
        {
            return ContractErrors.NotFound;
        }

        if (contract.Status == ContractStatus.Ended)
        {
            return ContractErrors.Ended;
        }

        if (command.EndDate is { } end && end <= contract.StartDate)
        {
            return Error.Validation("sales.contract.end_before_start", "The end date must be after the start date.");
        }

        contract.Update(new ContractTerms(command.Title, command.Scope, Round(command.Amount), command.PaymentTermDays, command.EndDate, command.AutoRenew, command.NoticeDays));
        await db.SaveChangesAsync(cancellationToken);
        return ContractViews.ToResponse(contract);
    }

    public async Task<Result<ContractResponse>> PauseAsync(Guid id, CancellationToken cancellationToken)
    {
        var contract = await FindAsync(id, cancellationToken);
        if (contract is null)
        {
            return ContractErrors.NotFound;
        }

        if (!contract.Pause())
        {
            return ContractErrors.NotActive;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ContractViews.ToResponse(contract);
    }

    public async Task<Result<ContractResponse>> ResumeAsync(Guid id, CancellationToken cancellationToken)
    {
        var contract = await FindAsync(id, cancellationToken);
        if (contract is null)
        {
            return ContractErrors.NotFound;
        }

        var today = SalesCalendar.Today(timeProvider);
        if (!contract.Resume(today))
        {
            return ContractErrors.NotPaused;
        }

        ContractViews.PublishPeriods(db, tenantContext.TenantId, contract, contract.BillDue(today), timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return ContractViews.ToResponse(contract);
    }

    public async Task<Result<ContractResponse>> EndAsync(Guid id, EndContractCommand command, CancellationToken cancellationToken)
    {
        var contract = await FindAsync(id, cancellationToken);
        if (contract is null)
        {
            return ContractErrors.NotFound;
        }

        if (!contract.End(SalesCalendar.Today(timeProvider), command.Reason))
        {
            return ContractErrors.Ended;
        }

        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        db.Publish(new ContractEnded(
            contract.TenantId, timeProvider.GetUtcNow(), contract.Id.Value, contract.Number, contract.Title, contract.PartyId,
            contract.EndedOn!.Value, contract.EndReason, userId.Value, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        return ContractViews.ToResponse(contract);
    }

    private static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    private Task<Contract?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var contractId = ContractId.From(id);
        return db.Contracts.FirstOrDefaultAsync(contract => contract.Id == contractId, cancellationToken);
    }
}

internal static class ContractErrors
{
    public static readonly Error NotFound = Error.NotFound("sales.contract.not_found", "No such contract in this organisation.");

    public static readonly Error PartyNotFound = Error.Validation("sales.contract.party_not_found", "The client does not exist in this organisation.");

    public static readonly Error QuoteNotAccepted = Error.Rule("sales.contract.quote_not_accepted", "Only an accepted quote can become a contract.");

    public static readonly Error NotActive = Error.Rule("sales.contract.not_active", "Only an active contract can be paused.");

    public static readonly Error NotPaused = Error.Rule("sales.contract.not_paused", "Only a paused contract can be resumed.");

    public static readonly Error Ended = Error.Rule("sales.contract.ended", "The contract has ended.");
}
