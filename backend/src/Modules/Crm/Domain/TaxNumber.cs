namespace Akiron.Modules.Crm.Domain;

/// <summary>
/// Turkish tax identity numbers: VKN (vergi kimlik numarası, 10 digits, companies) and TCKN
/// (T.C. kimlik numarası, 11 digits, persons and sole traders). Both carry check digits; a number
/// that fails them is a typo, and an e-invoice to it would be rejected by GİB.
/// </summary>
public static class TaxNumber
{
    public static bool IsValidVkn(string? value)
    {
        if (value is not { Length: 10 } || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var index = 0; index < 9; index++)
        {
            var digit = value[index] - '0';
            var shifted = (digit + 9 - index) % 10;
            var weighted = shifted * (1 << (9 - index)) % 9;
            if (shifted != 0 && weighted == 0)
            {
                weighted = 9;
            }

            sum += weighted;
        }

        return (10 - (sum % 10)) % 10 == value[9] - '0';
    }

    public static bool IsValidTckn(string? value)
    {
        if (value is not { Length: 11 } || !value.All(char.IsAsciiDigit) || value[0] == '0')
        {
            return false;
        }

        var digits = value.Select(character => character - '0').ToArray();
        var odd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var even = digits[1] + digits[3] + digits[5] + digits[7];

        var tenth = ((odd * 7) - even) % 10;
        if (tenth < 0)
        {
            tenth += 10;
        }

        return tenth == digits[9] && digits.Take(10).Sum() % 10 == digits[10];
    }

    /// <summary>Companies are identified by VKN; persons by TCKN (a sole trader's VKN is also accepted).</summary>
    public static bool IsValidFor(PartyKind kind, string value) =>
        kind == PartyKind.Company ? IsValidVkn(value) : IsValidTckn(value) || IsValidVkn(value);
}
