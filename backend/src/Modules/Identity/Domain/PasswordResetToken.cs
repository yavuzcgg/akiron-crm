using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Identity.Domain;

/// <summary>
/// A one-time, one-hour link for choosing a new password. Only the hash is stored; asking again
/// does not invalidate earlier links, but using any one of them revokes the rest.
/// </summary>
public sealed class PasswordResetToken : Entity<PasswordResetTokenId>
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private PasswordResetToken(PasswordResetTokenId id, UserId userId, string tokenHash, DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = createdAt + Lifetime;
    }

    public UserId UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static PasswordResetToken Create(UserId userId, string tokenHash, DateTimeOffset now) =>
        new(PasswordResetTokenId.New(), userId, tokenHash, now);

    public bool IsUsableAt(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;

    public void MarkUsed(DateTimeOffset now) => UsedAt = now;
}
