using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Crm;

internal static class CrmErrors
{
    public const string TaxNumberInvalidCode = "crm.party.tax_number_invalid";
    public const string RoleRequiredCode = "crm.party.role_required";

    public static readonly Error PartyNotFound = Error.NotFound("crm.party.not_found", "No such party in this organisation.");

    public static readonly Error ContactNotFound = Error.NotFound("crm.contact.not_found", "No such contact on this party.");

    public static readonly Error CustomFieldNotFound = Error.NotFound("crm.custom_field.not_found", "No such custom field.");

    public static readonly Error CustomFieldOrderMismatch = Error.Validation("crm.custom_field.order_mismatch", "Send every custom field exactly once.");

    public static Error CustomFieldUnknown(string key) => WithField("crm.custom_field.unknown", "The party has no such custom field.", key);

    public static Error CustomFieldInvalid(string label) => WithField("crm.custom_field.invalid", "The value does not fit the field's type.", label);

    public static Error CustomFieldRequired(string label) => WithField("crm.custom_field.required", "The field is required.", label);

    private static Error WithField(string code, string detail, string field) =>
        new(code, detail, ErrorKind.Validation) { Parameters = new Dictionary<string, object?> { ["field"] = field } };

    public static readonly Error CodeTaken = Error.Conflict("crm.party.code_taken", "Another party already uses this code.");
}
