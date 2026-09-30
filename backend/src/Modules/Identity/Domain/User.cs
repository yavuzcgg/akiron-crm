using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Identity.Domain;

/// <summary>A person who can sign in. Global: one user can belong to several tenants through memberships.</summary>
public sealed class User : Entity<UserId>, IAuditable
{
    public const int EmailMaxLength = 254;
    public const int FullNameMaxLength = 200;

    private User(UserId id, string email, string fullName, string passwordHash)
        : base(id)
    {
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        IsActive = true;
    }

    /// <summary>Always stored normalised (see <see cref="NormalizeEmail"/>).</summary>
    public string Email { get; private set; }

    public string FullName { get; private set; }

    [AuditIgnore]
    public string PasswordHash { get; private set; }

    public bool IsActive { get; private set; }

    [AuditIgnore]
    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static User Create(string email, string fullName, string passwordHash) =>
        new(UserId.New(), NormalizeEmail(email), fullName.Trim(), passwordHash);

    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;

    public void Rename(string fullName) => FullName = fullName.Trim();

    public void ChangePassword(string passwordHash) => PasswordHash = passwordHash;

    /// <summary>
    /// Invariant lower-casing on purpose: the Turkish culture would turn "I" into dotless "ı" and
    /// make INFO@FIRMA.COM a different address from info@firma.com.
    /// </summary>
#pragma warning disable CA1308 // E-mail addresses are compared in lower case by convention.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
#pragma warning restore CA1308
}
