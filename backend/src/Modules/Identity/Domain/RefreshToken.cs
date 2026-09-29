using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Identity.Domain;

/// <summary>
/// One link in a refresh-token chain (akiron-seo design). Only the SHA-256 hash is stored. Each use
/// revokes the token and issues a successor in the same family; presenting a revoked token again
/// means it was stolen, and the whole family is revoked. One family per signed-in device.
/// </summary>
/// <remarks>Global, not tenant-scoped: refresh happens before any tenant is bound.</remarks>
public sealed class RefreshToken : Entity<RefreshTokenId>
{
    private RefreshToken(
        RefreshTokenId id,
        UserId userId,
        TenantId tenantId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public UserId UserId { get; private set; }

    /// <summary>The tenant the session is signed into; refresh keeps it.</summary>
    public TenantId TenantId { get; private set; }

    public string TokenHash { get; private set; }

    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public static RefreshToken Create(
        UserId userId,
        TenantId tenantId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset now,
        TimeSpan lifetime) =>
        new(RefreshTokenId.New(), userId, tenantId, tokenHash, familyId, now, now + lifetime);
}
