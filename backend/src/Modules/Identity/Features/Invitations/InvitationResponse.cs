namespace Akiron.Modules.Identity.Features.Invitations;

/// <param name="Status">pending, accepted, revoked or expired.</param>
internal sealed record InvitationResponse(
    Guid Id,
    string Email,
    string Role,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string InvitedByName);

/// <summary>What the accept page shows before the visitor has an account or session.</summary>
/// <param name="AccountExists">True when the address already has an account: the page asks for its password instead of a new one.</param>
internal sealed record InvitationPreviewResponse(
    string WorkspaceName,
    string Email,
    string Role,
    string InvitedByName,
    bool AccountExists,
    DateTimeOffset ExpiresAt);
