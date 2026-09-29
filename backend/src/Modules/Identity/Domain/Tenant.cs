using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Identity.Domain;

/// <summary>A customer organisation. Global: it is the tenant, so it is not scoped to one.</summary>
public sealed partial class Tenant : Entity<TenantId>, IAuditable
{
    public const int NameMaxLength = 200;

    private Tenant(TenantId id, string name, string slug)
        : base(id)
    {
        Name = name;
        Slug = slug;
    }

    public string Name { get; private set; }

    /// <summary>URL-safe identifier, unique across tenants; later used for portal and inbound e-mail addresses.</summary>
    public string Slug { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static Tenant Create(string name)
    {
        var id = TenantId.New();
        var trimmed = name.Trim();

        return new Tenant(id, trimmed, CreateSlug(trimmed, id));
    }

    /// <summary>"Çelik Ajans" becomes "celik-ajans-0199a1b2": readable, and unique through the id suffix.</summary>
    private static string CreateSlug(string name, TenantId id)
    {
        // Dotless ı has no decomposition, so NFD alone would drop it; map it explicitly.
        var withoutDiacritics = string.Concat(
            name.Replace('ı', 'i').Replace('İ', 'I')
                .Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));

#pragma warning disable CA1308 // Slugs are lower-case by definition; the input is already ASCII here.
        var slug = NonAlphanumeric().Replace(withoutDiacritics.ToLowerInvariant(), "-").Trim('-');
#pragma warning restore CA1308

        if (slug.Length == 0)
        {
            slug = "tenant";
        }

        return $"{slug[..Math.Min(slug.Length, 48)].TrimEnd('-')}-{id.Value.ToString("N")[^8..]}";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
