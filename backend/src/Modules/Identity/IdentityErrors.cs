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
}
