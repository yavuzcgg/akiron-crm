namespace Akiron.BuildingBlocks.Tenancy;

/// <summary>
/// A write would have stored or changed a row outside the current tenant, or a tenant-scoped row
/// was saved with no tenant bound. Always a programming error, never a user error.
/// </summary>
public sealed class TenantMismatchException : Exception
{
    public TenantMismatchException()
    {
    }

    public TenantMismatchException(string message)
        : base(message)
    {
    }

    public TenantMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
