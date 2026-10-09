using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Finance.Domain;

public readonly record struct LedgerEntryId(Guid Value) : ITypedId<LedgerEntryId>
{
    public static LedgerEntryId New() => new(Guid.CreateVersion7());

    public static LedgerEntryId From(Guid value) => new(value);
}

public enum LedgerSource
{
    /// <summary>A charge: what the client owes (a sale, or an opening balance).</summary>
    Charge,

    /// <summary>A collection received from the client.</summary>
    Receipt,
}

/// <summary>
/// One line of the party ledger (cari hareket, ADR-0004): append-only. Debit means the party owes
/// us more, credit means less (money received). Nothing is updated or deleted; a cancellation is a
/// new line in the opposite direction pointing at the one it reverses. Balances are sums of these.
/// </summary>
public sealed class LedgerEntry : Entity<LedgerEntryId>, ITenantScoped
{
    public const int DescriptionMaxLength = 300;

    private LedgerEntry(LedgerEntryId id)
        : base(id)
    {
        Description = string.Empty;
        Currency = Currency.Try;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>Insertion order; ties on the same date keep the order they were written in.</summary>
    public long Sequence { get; private set; }

    public Guid PartyId { get; private set; }

    public DateOnly Date { get; private set; }

    public LedgerSource Source { get; private set; }

    public Guid SourceId { get; private set; }

    /// <summary>The document number shown on the statement (BRC-2026-0001, TAH-2026-0001).</summary>
    public string? DocumentNumber { get; private set; }

    public string Description { get; private set; }

    public decimal Debit { get; private set; }

    public decimal Credit { get; private set; }

    public Currency Currency { get; private set; }

    /// <summary>TRY per unit of <see cref="Currency"/> on the document; 1 for TRY.</summary>
    public decimal ExchangeRate { get; private set; }

    /// <summary>The line this one cancels, if it is a reversal.</summary>
    public LedgerEntryId? ReversesId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static LedgerEntry Post(
        Guid partyId, DateOnly date, LedgerSource source, Guid sourceId, string? documentNumber, string description,
        decimal debit, decimal credit, Currency currency, decimal exchangeRate, DateTimeOffset now) =>
        new(LedgerEntryId.New())
        {
            PartyId = partyId,
            Date = date,
            Source = source,
            SourceId = sourceId,
            DocumentNumber = documentNumber,
            Description = description.Length <= DescriptionMaxLength ? description : description[..DescriptionMaxLength],
            Debit = debit,
            Credit = credit,
            Currency = currency,
            ExchangeRate = exchangeRate,
            CreatedAt = now,
        };

    /// <summary>The mirror line: same amounts the other way round, dated when the cancellation happened.</summary>
    public LedgerEntry Reverse(DateOnly date, string description, DateTimeOffset now)
    {
        var reversal = Post(PartyId, date, Source, SourceId, DocumentNumber, description, Credit, Debit, Currency, ExchangeRate, now);
        reversal.ReversesId = Id;
        return reversal;
    }
}
