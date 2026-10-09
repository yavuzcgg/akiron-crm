using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Finance.Domain;

public readonly record struct ChargeId(Guid Value) : ITypedId<ChargeId>
{
    public static ChargeId New() => new(Guid.CreateVersion7());

    public static ChargeId From(Guid value) => new(value);
}

public readonly record struct OpenItemId(Guid Value) : ITypedId<OpenItemId>
{
    public static OpenItemId New() => new(Guid.CreateVersion7());

    public static OpenItemId From(Guid value) => new(value);
}

public enum ChargeKind
{
    /// <summary>A sale: work done or agreed (often from an accepted quote).</summary>
    Sale,

    /// <summary>What the client already owed when the agency started using Akiron.</summary>
    Opening,
}

public enum ChargeStatus
{
    Active,
    Cancelled,
}

public sealed record Instalment(DateOnly DueDate, decimal Amount);

/// <summary>
/// What a client owes us (borç kaydı), split into one or more instalments (ödeme planı) that
/// collections are allocated to. Until the Finance module issues e-invoices (Phase 5) this is also
/// the "to be invoiced" list for the accountant: the invoice number is noted when it is cut.
/// </summary>
public sealed class Charge : Entity<ChargeId>, ITenantScoped, IAuditable
{
    public const int DescriptionMaxLength = 300;
    public const int MaxInstalments = 36;

    private readonly List<OpenItem> _items = [];

    private Charge(ChargeId id, string number)
        : base(id)
    {
        Number = number;
        Description = string.Empty;
        PartyName = string.Empty;
        Currency = Currency.Try;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>BRC-2026-0001.</summary>
    public string Number { get; private set; }

    public ChargeKind Kind { get; private set; }

    public ChargeStatus Status { get; private set; }

    public Guid PartyId { get; private set; }

    public string PartyName { get; private set; }

    public DateOnly Date { get; private set; }

    public string Description { get; private set; }

    public Currency Currency { get; private set; }

    public decimal ExchangeRate { get; private set; }

    /// <summary>The total owed; the sum of the instalments.</summary>
    public decimal Amount { get; private set; }

    /// <summary>The accepted quote this charge bills, if any.</summary>
    public Guid? QuoteId { get; private set; }

    /// <summary>The contract whose month this charge bills (with <see cref="Period"/>), if any.</summary>
    public Guid? ContractId { get; private set; }

    /// <summary>First day of the contract month billed.</summary>
    public DateOnly? Period { get; private set; }

    public string? InvoiceNumber { get; private set; }

    public DateOnly? InvoiceDate { get; private set; }

    public string? CancellationReason { get; private set; }

    public IReadOnlyList<OpenItem> Items => _items;

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static Charge Record(
        string number, ChargeKind kind, Guid partyId, string partyName, DateOnly date, string description,
        Currency currency, decimal exchangeRate, IReadOnlyList<Instalment> instalments, Guid? quoteId,
        Guid? contractId = null, DateOnly? period = null)
    {
        var charge = new Charge(ChargeId.New(), number)
        {
            Kind = kind,
            PartyId = partyId,
            PartyName = partyName,
            Date = date,
            Description = description.Trim(),
            Currency = currency,
            ExchangeRate = currency == Currency.Try ? 1m : exchangeRate,
            QuoteId = quoteId,
            ContractId = contractId,
            Period = period,
            Amount = instalments.Sum(instalment => instalment.Amount),
        };
        charge._items.AddRange(instalments
            .OrderBy(instalment => instalment.DueDate)
            .Select((instalment, index) => OpenItem.Create(charge, index + 1, instalment)));
        return charge;
    }

    public void MarkInvoiced(string invoiceNumber, DateOnly invoiceDate)
    {
        InvoiceNumber = invoiceNumber.Trim();
        InvoiceDate = invoiceDate;
    }

    /// <summary>Only a charge nothing was collected against can be cancelled; reverse those collections first.</summary>
    public bool Cancel(string reason)
    {
        if (Status == ChargeStatus.Cancelled || _items.Any(item => item.Allocated > 0))
        {
            return false;
        }

        Status = ChargeStatus.Cancelled;
        CancellationReason = reason.Trim();
        _items.ForEach(item => item.Close());
        return true;
    }
}

/// <summary>An instalment still (partly) to be collected; allocations raise <see cref="Allocated"/>.</summary>
public sealed class OpenItem : Entity<OpenItemId>, ITenantScoped
{
    private OpenItem(OpenItemId id)
        : base(id)
    {
        Currency = Currency.Try;
    }

    public TenantId TenantId { get; private set; }

    public ChargeId ChargeId { get; private set; }

    public Guid PartyId { get; private set; }

    /// <summary>1-based instalment number within the charge.</summary>
    public int Sequence { get; private set; }

    public DateOnly DueDate { get; private set; }

    public decimal Amount { get; private set; }

    public decimal Allocated { get; private set; }

    public Currency Currency { get; private set; }

    /// <summary>Whether it can still take money: its charge is active and it is not fully paid.</summary>
    public bool IsOpen { get; private set; }

    public decimal Remaining => Amount - Allocated;

    public DateTimeOffset? LastRemindedAt { get; private set; }

    public int RemindersSent { get; private set; }

    internal static OpenItem Create(Charge charge, int sequence, Instalment instalment) =>
        new(OpenItemId.New())
        {
            ChargeId = charge.Id,
            PartyId = charge.PartyId,
            Sequence = sequence,
            DueDate = instalment.DueDate,
            Amount = instalment.Amount,
            Currency = charge.Currency,
            IsOpen = true,
        };

    /// <summary>Takes up to <paramref name="amount"/>; returns what it actually took.</summary>
    public decimal Allocate(decimal amount)
    {
        var taken = Math.Min(amount, Remaining);
        if (taken <= 0)
        {
            return 0;
        }

        Allocated += taken;
        IsOpen = Remaining > 0;
        return taken;
    }

    public void Release(decimal amount)
    {
        Allocated -= amount;
        IsOpen = true;
    }

    internal void Close() => IsOpen = false;

    public void Reminded(DateTimeOffset now)
    {
        LastRemindedAt = now;
        RemindersSent++;
    }
}
