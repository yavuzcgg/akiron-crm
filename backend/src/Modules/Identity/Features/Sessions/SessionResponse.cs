namespace Akiron.Modules.Identity.Features.Sessions;

/// <summary>Who is signed in, into which organisation, and what they may do. Tokens are in cookies, never here.</summary>
/// <param name="Role">A stable key (<c>owner</c>, <c>admin</c>, <c>member</c> or a custom role name); the client translates system roles.</param>
/// <param name="Permissions">Granted permissions; <c>*</c> means all.</param>
public sealed record SessionResponse(
    Guid UserId,
    string Email,
    string FullName,
    Guid TenantId,
    string TenantName,
    string Role,
    IReadOnlyList<string> Permissions);
