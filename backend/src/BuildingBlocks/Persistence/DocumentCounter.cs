using Akiron.BuildingBlocks.Domain;

namespace Akiron.BuildingBlocks.Persistence;

/// <summary>Row behind <see cref="ModuleDbContext.NextDocumentSequenceAsync"/>; only ever written by that SQL.</summary>
public sealed class DocumentCounter
{
    private DocumentCounter()
    {
    }

    public TenantId TenantId { get; private set; }

    public string Series { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public long LastValue { get; private set; }
}

public static class DocumentNumber
{
    /// <summary><c>Format("TKL", 2026, 7)</c> → <c>TKL-2026-0007</c>. Four digits minimum, more when needed.</summary>
    public static string Format(string series, int year, long sequence) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{series}-{year}-{sequence:0000}");
}
