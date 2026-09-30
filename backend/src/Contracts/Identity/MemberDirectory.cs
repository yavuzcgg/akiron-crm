namespace Akiron.Contracts.Identity;

public sealed record MemberSummary(Guid UserId, string FullName);

/// <summary>
/// Read side of Identity for other modules: who belongs to the current tenant. Used to check
/// assignees and to show names without copying them into every module.
/// </summary>
public interface IMemberDirectory
{
    /// <summary>Active members of the current tenant among <paramref name="userIds"/>; unknown ids are left out.</summary>
    Task<IReadOnlyDictionary<Guid, MemberSummary>> FindAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Every active member of the current tenant, by name.</summary>
    Task<IReadOnlyList<MemberSummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Active members whose role grants <paramref name="permission"/> (owners always do): who approves, who gets told.</summary>
    Task<IReadOnlyList<MemberSummary>> WithPermissionAsync(string permission, CancellationToken cancellationToken);
}
