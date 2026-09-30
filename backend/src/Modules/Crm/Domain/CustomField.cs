using System.Globalization;
using System.Text;
using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Crm.Domain;

public readonly record struct CustomFieldId(Guid Value) : ITypedId<CustomFieldId>
{
    public static CustomFieldId New() => new(Guid.CreateVersion7());

    public static CustomFieldId From(Guid value) => new(value);
}

public enum CustomFieldType
{
    Text,
    Number,
    Date,
    Select,
    Checkbox,
}

/// <summary>
/// A field a tenant adds to its party cards ("Sektör", "Sözleşme bitişi", "Instagram hesabı").
/// The key is fixed at creation so stored values survive a rename of the label.
/// </summary>
public sealed class CustomField : Entity<CustomFieldId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int LabelMaxLength = 60;
    public const int ValueMaxLength = 500;
    public const int MaxOptions = 50;

    private CustomField(CustomFieldId id, string key, CustomFieldType type)
        : base(id)
    {
        Key = key;
        Type = type;
        Label = string.Empty;
        Options = [];
    }

    public TenantId TenantId { get; private set; }

    public string Key { get; private set; }

    public string Label { get; private set; }

    public CustomFieldType Type { get; private set; }

    /// <summary>The choices of a <see cref="CustomFieldType.Select"/> field.</summary>
    public IReadOnlyList<string> Options { get; private set; }

    public bool IsRequired { get; private set; }

    public int Position { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static CustomField Create(string key, CustomFieldType type, string label, IReadOnlyList<string> options, bool isRequired, int position)
    {
        var field = new CustomField(CustomFieldId.New(), key, type) { Position = position };
        field.Update(label, options, isRequired);
        return field;
    }

    public void Update(string label, IReadOnlyList<string> options, bool isRequired)
    {
        Label = label.Trim();
        Options = Type == CustomFieldType.Select
            ? options.Select(option => option.Trim()).Where(option => option.Length > 0).Distinct(StringComparer.Ordinal).ToList()
            : [];
        IsRequired = isRequired;
    }

    public void MoveTo(int position) => Position = position;

    /// <summary>"Instagram hesabı" → "instagram_hesabi": stable, ASCII, readable in exports.</summary>
    public static string KeyFrom(string label)
    {
        var folded = TurkishText.Fold(label);
        var builder = new StringBuilder();
        foreach (var character in folded)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) ? character : '_');
        }

        var key = string.Join('_', builder.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries));
        return key.Length == 0 ? "field" : key.Length > 40 ? key[..40] : key;
    }

    /// <summary>
    /// Checks a submitted value and returns it in its stored form (numbers invariant, dates
    /// yyyy-MM-dd, checkboxes true/false); null when empty; false when it does not fit the type.
    /// </summary>
    public bool TryNormalize(string? value, out string? normalized)
    {
        normalized = null;
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        switch (Type)
        {
            case CustomFieldType.Text:
                normalized = text;
                return text.Length <= ValueMaxLength;
            case CustomFieldType.Number:
                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    normalized = number.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                return false;
            case CustomFieldType.Date:
                if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    normalized = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    return true;
                }

                return false;
            case CustomFieldType.Select:
                normalized = text;
                return Options.Contains(text, StringComparer.Ordinal);
            case CustomFieldType.Checkbox:
                if (bool.TryParse(text, out var flag))
                {
                    // An unticked box is no value, so "required" means "must be ticked".
                    normalized = flag ? "true" : null;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }
}
