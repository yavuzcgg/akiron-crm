using System.Security.Cryptography;
using System.Text;
using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Portal.Domain;

public readonly record struct PortalUserId(Guid Value) : ITypedId<PortalUserId>
{
    public static PortalUserId New() => new(Guid.CreateVersion7());

    public static PortalUserId From(Guid value) => new(value);
}

public enum PortalUserStatus
{
    /// <summary>Invited, never signed in.</summary>
    Invited,

    Active,

    /// <summary>Access switched off; links and sessions stop working, history stays.</summary>
    Disabled,
}

/// <summary>
/// A client's person who may open the portal for one party (ADR-0011). Not a member of staff: no
/// password, no permissions; they sign in with a link sent to <see cref="Email"/> and see only
/// their party's records.
/// </summary>
public sealed class PortalUser : Entity<PortalUserId>, ITenantScoped, IAuditable
{
    public const int NameMaxLength = 200;

    private PortalUser(PortalUserId id)
        : base(id)
    {
        FullName = string.Empty;
        Email = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid PartyId { get; private set; }

    public string FullName { get; private set; }

    /// <summary>Lower-cased; unique per tenant.</summary>
    public string Email { get; private set; }

    public PortalUserStatus Status { get; private set; }

    public DateTimeOffset? LastSignInAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static PortalUser Invite(Guid partyId, string fullName, string email) =>
        new(PortalUserId.New()) { PartyId = partyId, FullName = fullName.Trim(), Email = NormalizeEmail(email), Status = PortalUserStatus.Invited };

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void SignedIn(DateTimeOffset now)
    {
        Status = PortalUserStatus.Active;
        LastSignInAt = now;
    }

    public bool Disable()
    {
        if (Status == PortalUserStatus.Disabled)
        {
            return false;
        }

        Status = PortalUserStatus.Disabled;
        return true;
    }

    public bool Enable()
    {
        if (Status != PortalUserStatus.Disabled)
        {
            return false;
        }

        Status = LastSignInAt is null ? PortalUserStatus.Invited : PortalUserStatus.Active;
        return true;
    }
}

/// <summary>Secrets travel in links and cookies; only their SHA-256 is stored.</summary>
public static class PortalSecret
{
    public static string New() => RandomNumberGenerator.GetHexString(48);

    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}

/// <summary>A single-use sign-in link: the invitation (days) or a requested link (minutes).</summary>
public sealed class PortalSignInLink : ITenantScoped
{
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan RequestedLifetime = TimeSpan.FromMinutes(20);

    private PortalSignInLink()
    {
        TokenHash = string.Empty;
    }

    public long Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public PortalUserId PortalUserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static PortalSignInLink Issue(PortalUserId userId, string secret, DateTimeOffset expiresAt) =>
        new() { PortalUserId = userId, TokenHash = PortalSecret.Hash(secret), ExpiresAt = expiresAt };

    public bool IsUsableAt(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;

    public void Use(DateTimeOffset now) => UsedAt = now;
}

/// <summary>A signed-in browser; revocable, so switching access off works at once.</summary>
public sealed class PortalSession : ITenantScoped
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private PortalSession()
    {
        TokenHash = string.Empty;
    }

    public long Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public PortalUserId PortalUserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static PortalSession Start(PortalUserId userId, string secret, DateTimeOffset now) =>
        new() { PortalUserId = userId, TokenHash = PortalSecret.Hash(secret), CreatedAt = now, ExpiresAt = now + Lifetime };

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}

public readonly record struct PortalSettingsId(Guid Value) : ITypedId<PortalSettingsId>
{
    public static PortalSettingsId New() => new(Guid.CreateVersion7());

    public static PortalSettingsId From(Guid value) => new(value);
}

/// <summary>What the agency tells every client in the portal: a greeting and how to pay (IBAN, account name).</summary>
public sealed class PortalSettings : Entity<PortalSettingsId>, ITenantScoped, IAuditable
{
    public const int TextMaxLength = 2000;

    private PortalSettings(PortalSettingsId id)
        : base(id)
    {
    }

    public TenantId TenantId { get; private set; }

    public string? WelcomeNote { get; private set; }

    public string? PaymentInstructions { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static PortalSettings Create() => new(PortalSettingsId.New());

    public void Update(string? welcomeNote, string? paymentInstructions)
    {
        WelcomeNote = string.IsNullOrWhiteSpace(welcomeNote) ? null : welcomeNote.Trim();
        PaymentInstructions = string.IsNullOrWhiteSpace(paymentInstructions) ? null : paymentInstructions.Trim();
    }
}
