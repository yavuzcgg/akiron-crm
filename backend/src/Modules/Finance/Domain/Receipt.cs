using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Finance.Domain;

public readonly record struct ReceiptId(Guid Value) : ITypedId<ReceiptId>
{
    public static ReceiptId New() => new(Guid.CreateVersion7());

    public static ReceiptId From(Guid value) => new(value);
}

public readonly record struct AllocationId(Guid Value) : ITypedId<AllocationId>
{
    public static AllocationId New() => new(Guid.CreateVersion7());

    public static AllocationId From(Guid value) => new(value);
}

public enum PaymentMethod
{
    Transfer,
    Cash,
    Card,
    Check,
    Note,
    Other,
}

public enum ReceiptStatus
{
    Recorded,
    Reversed,
}

/// <summary>
/// Money received from a client (tahsilat). It is recorded against the party, not after an
/// invoice (ADR-0004), and allocated to open items; what is not allocated is an advance on the
/// party's account until it is matched later. A mistake is undone by reversing, never by deleting.
/// </summary>
public sealed class Receipt : Entity<ReceiptId>, ITenantScoped, IAuditable
{
    public const int NoteMaxLength = 500;

    private readonly List<Allocation> _allocations = [];

    private Receipt(ReceiptId id, string number)
        : base(id)
    {
        Number = number;
        PartyName = string.Empty;
        Currency = Currency.Try;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>TAH-2026-0001.</summary>
    public string Number { get; private set; }

    public ReceiptStatus Status { get; private set; }

    public Guid PartyId { get; private set; }

    public string PartyName { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal Amount { get; private set; }

    public Currency Currency { get; private set; }

    public decimal ExchangeRate { get; private set; }

    public PaymentMethod Method { get; private set; }

    /// <summary>Where the money went ("Garanti TL hesabı", "Kasa"); a label until Cash &amp; Bank (Phase 5).</summary>
    public string? Account { get; private set; }

    /// <summary>Bank reference, cheque number…</summary>
    public string? Reference { get; private set; }

    public string? Note { get; private set; }

    public string? ReversalReason { get; private set; }

    public IReadOnlyList<Allocation> Allocations => _allocations;

    public decimal Allocated => _allocations.Where(allocation => !allocation.IsReversed).Sum(allocation => allocation.Amount);

    public decimal Unallocated => Status == ReceiptStatus.Reversed ? 0 : Amount - Allocated;

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static Receipt Record(
        string number, Guid partyId, string partyName, DateOnly date, decimal amount, Currency currency, decimal exchangeRate,
        PaymentMethod method, string? account, string? reference, string? note) =>
        new(ReceiptId.New(), number)
        {
            PartyId = partyId,
            PartyName = partyName,
            Date = date,
            Amount = amount,
            Currency = currency,
            ExchangeRate = currency == Currency.Try ? 1m : exchangeRate,
            Method = method,
            Account = Clean(account),
            Reference = Clean(reference),
            Note = Clean(note),
        };

    /// <summary>Puts up to <paramref name="amount"/> of the unallocated money on <paramref name="item"/>; returns what was placed.</summary>
    public decimal AllocateTo(OpenItem item, decimal amount, DateTimeOffset now)
    {
        if (Status != ReceiptStatus.Recorded || item.Currency != Currency || item.PartyId != PartyId)
        {
            return 0;
        }

        var placed = item.Allocate(Math.Min(amount, Unallocated));
        if (placed > 0)
        {
            _allocations.Add(new Allocation(AllocationId.New(), Id, item.Id, placed, now));
        }

        return placed;
    }

    /// <summary>Undoes the receipt and every allocation it made; the open items reopen.</summary>
    public bool Reverse(string reason, IReadOnlyDictionary<OpenItemId, OpenItem> items)
    {
        if (Status == ReceiptStatus.Reversed)
        {
            return false;
        }

        foreach (var allocation in _allocations.Where(allocation => !allocation.IsReversed))
        {
            allocation.MarkReversed();
            if (items.TryGetValue(allocation.OpenItemId, out var item))
            {
                item.Release(allocation.Amount);
            }
        }

        Status = ReceiptStatus.Reversed;
        ReversalReason = reason.Trim();
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Which money closed which item (ADR-0004); same currency only in Phase 3.</summary>
public sealed class Allocation : Entity<AllocationId>, ITenantScoped
{
    internal Allocation(AllocationId id, ReceiptId receiptId, OpenItemId openItemId, decimal amount, DateTimeOffset createdAt)
        : base(id)
    {
        ReceiptId = receiptId;
        OpenItemId = openItemId;
        Amount = amount;
        CreatedAt = createdAt;
    }

    public TenantId TenantId { get; private set; }

    public ReceiptId ReceiptId { get; private set; }

    public OpenItemId OpenItemId { get; private set; }

    public decimal Amount { get; private set; }

    public bool IsReversed { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal void MarkReversed() => IsReversed = true;
}
