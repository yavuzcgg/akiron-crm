using System.Security.Cryptography;
using System.Text;
using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Sales.Domain;

public readonly record struct QuoteId(Guid Value) : ITypedId<QuoteId>
{
    public static QuoteId New() => new(Guid.CreateVersion7());

    public static QuoteId From(Guid value) => new(value);
}

public readonly record struct QuoteLineId(Guid Value) : ITypedId<QuoteLineId>
{
    public static QuoteLineId New() => new(Guid.CreateVersion7());

    public static QuoteLineId From(Guid value) => new(value);
}

public enum QuoteStatus
{
    Draft,
    Sent,
    Accepted,
    Rejected,
}

/// <param name="UnitPrice">Net of VAT, four decimals.</param>
public sealed record QuoteLineInput(
    Guid? CatalogItemId,
    string Name,
    string? Description,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal DiscountPercent,
    int VatRate,
    int WithholdingTenths);

/// <param name="ExchangeRate">TRY per one unit of <paramref name="Currency"/> (TCMB döviz alış), null for TRY.</param>
public sealed record QuoteDetails(
    Guid PartyId,
    string PartyName,
    string Title,
    string? RecipientName,
    string? RecipientEmail,
    Currency Currency,
    decimal? ExchangeRate,
    DateOnly? RateDate,
    DateOnly IssueDate,
    DateOnly ValidUntil,
    string? Notes,
    IReadOnlyList<QuoteLineInput> Lines);

/// <summary>
/// An offer to a client (teklif). Editable while a draft; sending freezes it, gives it a public
/// link and keeps a copy of what was sent. A sent quote is revised by turning it back into a draft
/// with the next revision number (the old link stops working). Accepted is final.
/// </summary>
public sealed class Quote : Entity<QuoteId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int NotesMaxLength = 4000;
    public const int MaxLines = 100;

    private readonly List<QuoteLine> _lines = [];

    private Quote(QuoteId id, string number)
        : base(id)
    {
        Number = number;
        Revision = 1;
        Title = string.Empty;
        PartyName = string.Empty;
        Currency = Currency.Try;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>TKL-2026-0001; revisions keep the number and raise <see cref="Revision"/>.</summary>
    public string Number { get; private set; }

    public int Revision { get; private set; }

    public QuoteStatus Status { get; private set; }

    public Guid PartyId { get; private set; }

    /// <summary>The client's name when the quote was written; the document shows what was offered to whom.</summary>
    public string PartyName { get; private set; }

    public string Title { get; private set; }

    public string? RecipientName { get; private set; }

    public string? RecipientEmail { get; private set; }

    public Currency Currency { get; private set; }

    public decimal? ExchangeRate { get; private set; }

    public DateOnly? RateDate { get; private set; }

    public DateOnly IssueDate { get; private set; }

    public DateOnly ValidUntil { get; private set; }

    /// <summary>Terms, payment conditions, what is not included.</summary>
    public string? Notes { get; private set; }

    public IReadOnlyList<QuoteLine> Lines => _lines;

    public decimal GrossTotal { get; private set; }

    public decimal DiscountTotal { get; private set; }

    public decimal NetTotal { get; private set; }

    public decimal VatTotal { get; private set; }

    public decimal WithholdingTotal { get; private set; }

    public decimal GrandTotal { get; private set; }

    /// <summary>SHA-256 of the public link's secret; null while a draft. Only the hash is stored.</summary>
    public string? LinkTokenHash { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>The first time the client opened the link of this revision.</summary>
    public DateTimeOffset? ViewedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Who accepted or rejected, as the client typed it.</summary>
    public string? DecidedByName { get; private set; }

    public string? DecisionNote { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    /// <summary>The net total in TRY: what a work order's budget is measured in.</summary>
    public decimal NetTotalTry => ExchangeRate is { } rate ? QuoteMath.Round(NetTotal * rate) : NetTotal;

    public static Quote Draft(string number, QuoteDetails details)
    {
        var quote = new Quote(QuoteId.New(), number);
        quote.Edit(details);
        return quote;
    }

    /// <summary>Only drafts change; returns false otherwise.</summary>
    public bool Edit(QuoteDetails details)
    {
        if (Status != QuoteStatus.Draft)
        {
            return false;
        }

        PartyId = details.PartyId;
        PartyName = details.PartyName;
        Title = details.Title.Trim();
        RecipientName = Clean(details.RecipientName);
        RecipientEmail = Clean(details.RecipientEmail)?.ToLowerInvariant();
        Currency = details.Currency;
        ExchangeRate = details.Currency == Currency.Try ? null : details.ExchangeRate;
        RateDate = details.Currency == Currency.Try ? null : details.RateDate;
        IssueDate = details.IssueDate;
        ValidUntil = details.ValidUntil;
        Notes = Clean(details.Notes);

        _lines.Clear();
        _lines.AddRange(details.Lines.Select((line, position) => QuoteLine.Create(Id, position, line)));
        Recalculate();
        return true;
    }

    /// <summary>Freezes the draft and opens a public link; returns the raw link secret (shown once, never stored).</summary>
    public string? Send(DateTimeOffset now)
    {
        if (Status != QuoteStatus.Draft || _lines.Count == 0)
        {
            return null;
        }

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        LinkTokenHash = HashToken(secret);
        Status = QuoteStatus.Sent;
        SentAt = now;
        ViewedAt = null;
        DecidedAt = null;
        DecidedByName = null;
        DecisionNote = null;
        return secret;
    }

    /// <summary>Records the first look at this revision; returns whether it was the first.</summary>
    public bool MarkViewed(DateTimeOffset now)
    {
        if (ViewedAt is not null || Status != QuoteStatus.Sent)
        {
            return false;
        }

        ViewedAt = now;
        return true;
    }

    public bool IsExpiredOn(DateOnly today) => Status == QuoteStatus.Sent && ValidUntil < today;

    /// <summary>The client's answer through the link; only a sent, unexpired quote can be answered.</summary>
    public bool Decide(bool accept, string name, string? note, DateOnly today, DateTimeOffset now)
    {
        if (Status != QuoteStatus.Sent || IsExpiredOn(today))
        {
            return false;
        }

        Status = accept ? QuoteStatus.Accepted : QuoteStatus.Rejected;
        DecidedAt = now;
        DecidedByName = name.Trim();
        DecisionNote = Clean(note);
        ViewedAt ??= now;
        return true;
    }

    /// <summary>Back to a draft as the next revision; the old link stops working. Accepted quotes stay as agreed.</summary>
    public bool Revise()
    {
        if (Status is not (QuoteStatus.Sent or QuoteStatus.Rejected))
        {
            return false;
        }

        Status = QuoteStatus.Draft;
        Revision++;
        LinkTokenHash = null;
        SentAt = null;
        ViewedAt = null;
        DecidedAt = null;
        DecidedByName = null;
        DecisionNote = null;
        return true;
    }

    public static string HashToken(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private void Recalculate()
    {
        GrossTotal = _lines.Sum(line => line.Gross);
        DiscountTotal = _lines.Sum(line => line.Discount);
        NetTotal = _lines.Sum(line => line.Net);
        VatTotal = _lines.Sum(line => line.Vat);
        WithholdingTotal = _lines.Sum(line => line.Withholding);
        GrandTotal = _lines.Sum(line => line.Total);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>One row of a quote; its amounts are computed once, when the draft is saved.</summary>
public sealed class QuoteLine : Entity<QuoteLineId>, ITenantScoped
{
    public const int NameMaxLength = 300;

    private QuoteLine(QuoteLineId id, QuoteId quoteId)
        : base(id)
    {
        QuoteId = quoteId;
        Name = string.Empty;
        Unit = Units.Piece;
    }

    public TenantId TenantId { get; private set; }

    public QuoteId QuoteId { get; private set; }

    public int Position { get; private set; }

    /// <summary>The catalog item it was picked from, if any; the line keeps its own copy of every value.</summary>
    public Guid? CatalogItemId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public decimal Quantity { get; private set; }

    public string Unit { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal DiscountPercent { get; private set; }

    public int VatRate { get; private set; }

    public int WithholdingTenths { get; private set; }

    public decimal Gross { get; private set; }

    public decimal Discount { get; private set; }

    public decimal Net { get; private set; }

    public decimal Vat { get; private set; }

    public decimal Withholding { get; private set; }

    public decimal Total { get; private set; }

    internal static QuoteLine Create(QuoteId quoteId, int position, QuoteLineInput input)
    {
        var amounts = QuoteMath.Line(input.Quantity, input.UnitPrice, input.DiscountPercent, input.VatRate, input.WithholdingTenths);
        return new QuoteLine(QuoteLineId.New(), quoteId)
        {
            Position = position,
            CatalogItemId = input.CatalogItemId,
            Name = input.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            Quantity = input.Quantity,
            Unit = input.Unit,
            UnitPrice = decimal.Round(input.UnitPrice, 4),
            DiscountPercent = input.DiscountPercent,
            VatRate = input.VatRate,
            WithholdingTenths = input.WithholdingTenths,
            Gross = amounts.Gross,
            Discount = amounts.Discount,
            Net = amounts.Net,
            Vat = amounts.Vat,
            Withholding = amounts.Withholding,
            Total = amounts.Total,
        };
    }
}
