using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Sales.Domain;

public readonly record struct ContractId(Guid Value) : ITypedId<ContractId>
{
    public static ContractId New() => new(Guid.CreateVersion7());

    public static ContractId From(Guid value) => new(value);
}

public enum ContractStatus
{
    /// <summary>Bills every month.</summary>
    Active,

    /// <summary>Bills nothing until resumed; the months in between are skipped, not billed later.</summary>
    Paused,

    /// <summary>Ended by someone or by reaching its end date; history stays.</summary>
    Ended,
}

/// <summary>What can change on a running contract; amounts apply to periods not yet billed.</summary>
public sealed record ContractTerms(
    string Title,
    string? Scope,
    decimal Amount,
    int PaymentTermDays,
    DateOnly? EndDate,
    bool AutoRenew,
    int NoticeDays);

/// <summary>A month the scheduler billed (or must bill): its first day and when the client pays it.</summary>
public sealed record BillingPeriod(DateOnly Start, DateOnly DueDate);

/// <summary>
/// A retainer or service contract (sözleşme / abonelik): a client pays <see cref="Amount"/> every month
/// from <see cref="StartDate"/>, billed on the same day of each month (clamped to short months). The
/// scheduler bills due periods; Finance turns each into a charge (RetainerPeriodDue). An end date
/// with auto-renewal extends by the original term; without it the contract ends there.
/// </summary>
public sealed class Contract : Entity<ContractId>, ITenantScoped, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int ScopeMaxLength = 4000;

    private Contract(ContractId id, string number)
        : base(id)
    {
        Number = number;
        Title = string.Empty;
        PartyName = string.Empty;
        Currency = Currency.Try;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>SZL-2026-0001.</summary>
    public string Number { get; private set; }

    public Guid PartyId { get; private set; }

    public string PartyName { get; private set; }

    public string Title { get; private set; }

    /// <summary>What the client gets each month (deliverables, limits); free text.</summary>
    public string? Scope { get; private set; }

    public Guid? QuoteId { get; private set; }

    public Currency Currency { get; private set; }

    /// <summary>What the client pays per month, VAT included: the charge amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>A fixed rate for a foreign-currency contract; null takes TCMB of each billing day.</summary>
    public decimal? ExchangeRate { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool AutoRenew { get; private set; }

    /// <summary>How long one renewal adds: the original term in months.</summary>
    public int RenewalMonths { get; private set; }

    /// <summary>Days before the end date the owner is reminded.</summary>
    public int NoticeDays { get; private set; }

    /// <summary>The client pays a period this many days after it starts.</summary>
    public int PaymentTermDays { get; private set; }

    public ContractStatus Status { get; private set; }

    /// <summary>Index of the next month to bill, counted from <see cref="StartDate"/> (0 = the first month).</summary>
    public int NextPeriodIndex { get; private set; }

    public int BilledPeriods { get; private set; }

    public DateOnly? LastBilledOn { get; private set; }

    /// <summary>The end date the notice reminder was sent for; a renewal moves the end date and re-arms it.</summary>
    public DateOnly? NoticeSentFor { get; private set; }

    public DateOnly? EndedOn { get; private set; }

    public string? EndReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    /// <summary>The day billing next happens; null when the contract bills nothing more.</summary>
    public DateOnly? NextBillingDate =>
        Status == ContractStatus.Active && (EndDate is null || PeriodStart(NextPeriodIndex) < EndDate) ? PeriodStart(NextPeriodIndex) : null;

    /// <param name="firstBillingDate">Skip months before this day (they were billed outside Akiron); null bills from the start.</param>
    public static Contract Create(
        string number, Guid partyId, string partyName, DateOnly startDate, Currency currency, decimal? exchangeRate,
        ContractTerms terms, Guid? quoteId, DateOnly? firstBillingDate)
    {
        var contract = new Contract(ContractId.New(), number)
        {
            PartyId = partyId,
            PartyName = partyName,
            StartDate = startDate,
            Currency = currency,
            ExchangeRate = currency == Currency.Try ? null : exchangeRate,
            QuoteId = quoteId,
            Status = ContractStatus.Active,
        };
        contract.Apply(terms);
        contract.RenewalMonths = terms.EndDate is { } end ? Math.Max(1, MonthsBetween(startDate, end)) : 12;
        while (firstBillingDate is { } first && contract.PeriodStart(contract.NextPeriodIndex) < first)
        {
            contract.NextPeriodIndex++;
        }

        return contract;
    }

    public void Update(ContractTerms terms)
    {
        var endChanged = terms.EndDate != EndDate;
        Apply(terms);
        if (endChanged)
        {
            NoticeSentFor = null;
            RenewalMonths = terms.EndDate is { } end ? Math.Max(1, MonthsBetween(StartDate, end)) : RenewalMonths;
        }
    }

    public DateOnly PeriodStart(int index) => StartDate.AddMonths(index);

    /// <summary>
    /// The months due up to <paramref name="today"/> (several when the scheduler was down), marked billed.
    /// Renewal comes first, so a renewed contract's first new month is billed in the same run.
    /// </summary>
    public IReadOnlyList<BillingPeriod> BillDue(DateOnly today)
    {
        var periods = new List<BillingPeriod>();
        while (Status == ContractStatus.Active && PeriodStart(NextPeriodIndex) <= today && (EndDate is null || PeriodStart(NextPeriodIndex) < EndDate))
        {
            var start = PeriodStart(NextPeriodIndex);
            periods.Add(new BillingPeriod(start, start.AddDays(PaymentTermDays)));
            NextPeriodIndex++;
            BilledPeriods++;
            LastBilledOn = today;
        }

        return periods;
    }

    /// <summary>Extends an auto-renewing contract past <paramref name="today"/>; returns whether it did.</summary>
    public bool RenewIfDue(DateOnly today)
    {
        if (Status == ContractStatus.Ended || !AutoRenew || EndDate is not { } end || end > today)
        {
            return false;
        }

        while (EndDate <= today)
        {
            EndDate = EndDate.Value.AddMonths(RenewalMonths);
        }

        NoticeSentFor = null;
        return true;
    }

    /// <summary>Ends a contract that reached its end date without auto-renewal; returns whether it did.</summary>
    public bool ExpireIfDue(DateOnly today)
    {
        if (Status == ContractStatus.Ended || AutoRenew || EndDate is not { } end || end > today)
        {
            return false;
        }

        Status = ContractStatus.Ended;
        EndedOn = end;
        return true;
    }

    /// <summary>Whether the notice reminder is due now; marks it sent.</summary>
    public bool TakeNoticeIfDue(DateOnly today)
    {
        if (Status == ContractStatus.Ended || EndDate is not { } end || NoticeSentFor == end || end.AddDays(-NoticeDays) > today)
        {
            return false;
        }

        NoticeSentFor = end;
        return true;
    }

    public bool Pause()
    {
        if (Status != ContractStatus.Active)
        {
            return false;
        }

        Status = ContractStatus.Paused;
        return true;
    }

    /// <summary>Billing restarts with the first month starting today or later; paused months are not billed.</summary>
    public bool Resume(DateOnly today)
    {
        if (Status != ContractStatus.Paused)
        {
            return false;
        }

        Status = ContractStatus.Active;
        while (PeriodStart(NextPeriodIndex) < today)
        {
            NextPeriodIndex++;
        }

        return true;
    }

    /// <summary>Ends the contract today (or on its end date if that is earlier); nothing more is billed.</summary>
    public bool End(DateOnly today, string reason)
    {
        if (Status == ContractStatus.Ended)
        {
            return false;
        }

        Status = ContractStatus.Ended;
        EndedOn = EndDate is { } end && end < today ? end : today;
        EndReason = reason.Trim();
        return true;
    }

    private void Apply(ContractTerms terms)
    {
        Title = terms.Title.Trim();
        Scope = string.IsNullOrWhiteSpace(terms.Scope) ? null : terms.Scope.Trim();
        Amount = terms.Amount;
        PaymentTermDays = terms.PaymentTermDays;
        EndDate = terms.EndDate;
        AutoRenew = terms.EndDate is not null && terms.AutoRenew;
        NoticeDays = terms.NoticeDays;
    }

    private static int MonthsBetween(DateOnly from, DateOnly to) => ((to.Year - from.Year) * 12) + to.Month - from.Month;
}
