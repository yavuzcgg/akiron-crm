using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Crm;

internal static class CrmErrors
{
    public const string TaxNumberInvalidCode = "crm.party.tax_number_invalid";
    public const string RoleRequiredCode = "crm.party.role_required";

    public static readonly Error PartyNotFound = Error.NotFound("crm.party.not_found", "No such party in this organisation.");

    public static readonly Error ContactNotFound = Error.NotFound("crm.contact.not_found", "No such contact on this party.");

    public static readonly Error CodeTaken = Error.Conflict("crm.party.code_taken", "Another party already uses this code.");
}
