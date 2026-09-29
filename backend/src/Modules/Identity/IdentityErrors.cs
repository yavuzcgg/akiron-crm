using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Identity;

public static class IdentityErrors
{
    public static readonly Error EmailTaken =
        Error.Conflict("identity.user.email_taken", "An account with this e-mail address already exists.");

    /// <summary>Deliberately the same for unknown e-mail, wrong password and inactive user, so accounts cannot be probed.</summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("identity.auth.invalid_credentials", "The e-mail address or password is incorrect.");

    public static readonly Error SessionExpired =
        Error.Unauthorized("identity.auth.session_expired", "The session has expired or was revoked; sign in again.");

    public static readonly Error InvitationInvalid =
        Error.NotFound("identity.invitation.invalid", "The invitation does not exist, has expired, or was already used.");

    public static readonly Error InvitationNotFound =
        Error.NotFound("identity.invitation.not_found", "No such invitation in this organisation.");

    public static readonly Error AlreadyMember =
        Error.Conflict("identity.member.already_member", "This person is already a member of the organisation.");

    public static readonly Error RoleNotFound =
        Error.Validation("identity.role.not_found", "No role with this name exists in the organisation.");

    public static readonly Error OwnerRoleNotInvitable =
        Error.Rule("identity.invitation.owner_not_invitable", "Ownership cannot be granted by invitation.");

    public static readonly Error FullNameRequired =
        Error.Validation("identity.invitation.full_name_required", "A full name is needed to create the new account.");

    public static Error PasswordTooShort(int minLength) =>
        new("identity.password.too_short", $"The password must be at least {minLength} characters.", ErrorKind.Validation)
        {
            Parameters = new Dictionary<string, object?> { ["minLength"] = minLength },
        };
}
