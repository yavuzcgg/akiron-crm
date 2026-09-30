using System.Globalization;
using System.Text;

namespace Akiron.Modules.Crm.Domain;

/// <summary>
/// Turkish-aware text helpers. Search is done on a folded form so that "IŞIK", "Işık" and "isik"
/// all match: Postgres ILIKE lower-cases "I" to "i", not to Turkish dotless "ı", and people type
/// without Turkish letters on foreign keyboards.
/// </summary>
public static class TurkishText
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Lower-case with Turkish rules, then fold ı ş ğ ü ö ç (and other accents) to plain ASCII.</summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lower = value.Trim().ToLower(Turkish).Replace('ı', 'i');
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
