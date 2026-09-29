using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Identity.Domain;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
    Expired,
}

public static class InvitationStatusNames
{
    /// <summary>The value the API reports: <c>pending</c>, <c>accepted</c>, <c>revoked</c>, <c>expired</c>.</summary>
    public static string ToApiValue(this InvitationStatus status) => status switch
    {
        InvitationStatus.Pending => "pending",
        InvitationStatus.Accepted => "accepted",
        InvitationStatus.Revoked => "revoked",
        _ => "expired",
    };
}

/// <summary>
/// A link that lets one e-mail address join one tenant with one role. Only the token's hash is
/// stored; the raw token travels once, in the e-mail.
/// </summary>
public sealed class Invitation : Entity<InvitationId>, ITenantScoped, IAuditable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation(InvitationId id, TenantId tenantId, string email, RoleId roleId, string tokenHash, UserId invitedBy, DateTimeOffset expiresAt)
        : base(id)
    {
        TenantId = tenantId;
        Email = email;
        RoleId = roleId;
        TokenHash = tokenHash;
        InvitedBy = invitedBy;
        ExpiresAt = expiresAt;
    }

    public TenantId TenantId { get; private set; }

    /// <summary>Normalised like <see cref="User.Email"/>.</summary>
    public string Email { get; private set; }

    public RoleId RoleId { get; private set; }

    public string TokenHash { get; private set; }

    public UserId InvitedBy { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public UserId? AcceptedBy { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static Invitation Create(TenantId tenantId, string email, RoleId roleId, string tokenHash, UserId invitedBy, DateTimeOffset now) =>
        new(InvitationId.New(), tenantId, User.NormalizeEmail(email), roleId, tokenHash, invitedBy, now + Lifetime);

    public InvitationStatus StatusAt(DateTimeOffset now) =>
        AcceptedAt is not null ? InvitationStatus.Accepted
        : RevokedAt is not null ? InvitationStatus.Revoked
        : ExpiresAt <= now ? InvitationStatus.Expired
        : InvitationStatus.Pending;

    public void Accept(UserId userId, DateTimeOffset now)
    {
        if (StatusAt(now) != InvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be accepted.");
        }

        AcceptedAt = now;
        AcceptedBy = userId;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (AcceptedAt is null && RevokedAt is null)
        {
            RevokedAt = now;
        }
    }
}
