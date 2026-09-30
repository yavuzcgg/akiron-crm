using Akiron.Modules.Crm.Domain;
using FluentValidation;

namespace Akiron.Modules.Crm.Features;

/// <summary>The fields a party form sends, shared by create and update.</summary>
internal interface IPartyInput
{
    string Kind { get; }

    string Name { get; }

    bool IsCustomer { get; }

    bool IsSupplier { get; }

    string? TaxNumber { get; }

    string? TaxOffice { get; }

    string? Email { get; }

    string? Phone { get; }

    string? Website { get; }

    string? City { get; }

    string? District { get; }

    string? AddressLine { get; }

    /// <summary>Custom field values by key; null leaves them as they are.</summary>
    IReadOnlyDictionary<string, string?>? CustomFields { get; }
}

internal static class PartyKinds
{
    public const string Company = "company";
    public const string Person = "person";

    public static PartyKind Parse(string value) => value == Person ? PartyKind.Person : PartyKind.Company;

    public static string ToApi(PartyKind kind) => kind == PartyKind.Person ? Person : Company;
}

internal static class PartyInputRules
{
    public static void Apply<T>(AbstractValidator<T> validator)
        where T : IPartyInput
    {
        validator.RuleFor(input => input.Kind).Must(kind => kind is PartyKinds.Company or PartyKinds.Person)
            .WithErrorCode("validation.invalid_value");
        validator.RuleFor(input => input.Name).NotEmpty().MaximumLength(Party.NameMaxLength);
        validator.RuleFor(input => input.IsCustomer).Must((input, _) => input.IsCustomer || input.IsSupplier)
            .WithErrorCode(CrmErrors.RoleRequiredCode)
            .WithMessage("A party is a customer, a supplier, or both.");
        validator.RuleFor(input => input.TaxNumber!)
            .Must((input, taxNumber) => TaxNumber.IsValidFor(PartyKinds.Parse(input.Kind), taxNumber.Trim()))
            .When(input => !string.IsNullOrWhiteSpace(input.TaxNumber))
            .WithErrorCode(CrmErrors.TaxNumberInvalidCode)
            .WithMessage("The VKN (10 digits) or TCKN (11 digits) fails its check digits.");
        validator.RuleFor(input => input.TaxOffice).MaximumLength(100);
        validator.RuleFor(input => input.Email!).EmailAddress().MaximumLength(254).When(input => !string.IsNullOrWhiteSpace(input.Email));
        validator.RuleFor(input => input.Phone).MaximumLength(30);
        validator.RuleFor(input => input.Website).MaximumLength(200);
        validator.RuleFor(input => input.City).MaximumLength(100);
        validator.RuleFor(input => input.District).MaximumLength(100);
        validator.RuleFor(input => input.AddressLine).MaximumLength(500);
    }

    public static PartyDetails ToDetails(IPartyInput input) => new(
        PartyKinds.Parse(input.Kind),
        input.Name,
        input.IsCustomer,
        input.IsSupplier,
        input.TaxNumber?.Trim(),
        input.TaxOffice,
        input.Email,
        input.Phone,
        input.Website,
        input.City,
        input.District,
        input.AddressLine);
}

internal sealed record PartyContactResponse(Guid Id, string FullName, string? Title, string? Email, string? Phone, bool IsPrimary);

internal sealed record PartyResponse(
    Guid Id,
    string Code,
    string Kind,
    string Name,
    bool IsCustomer,
    bool IsSupplier,
    string? TaxNumber,
    string? TaxOffice,
    string? Email,
    string? Phone,
    string? Website,
    string? City,
    string? District,
    string? AddressLine,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string> CustomFields,
    IReadOnlyList<PartyContactResponse> Contacts)
{
    public static PartyResponse From(Party party, IEnumerable<PartyContact> contacts) => new(
        party.Id.Value,
        party.Code,
        PartyKinds.ToApi(party.Kind),
        party.Name,
        party.IsCustomer,
        party.IsSupplier,
        party.TaxNumber,
        party.TaxOffice,
        party.Email,
        party.Phone,
        party.Website,
        party.City,
        party.District,
        party.AddressLine,
        party.CreatedAt,
        party.CustomValues,
        contacts
            .OrderByDescending(contact => contact.IsPrimary)
            .ThenBy(contact => contact.CreatedAt)
            .Select(ToResponse)
            .ToList());

    public static PartyContactResponse ToResponse(PartyContact contact) =>
        new(contact.Id.Value, contact.FullName, contact.Title, contact.Email, contact.Phone, contact.IsPrimary);
}
