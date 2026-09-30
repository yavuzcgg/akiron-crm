using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.Contracts.Identity;
using Akiron.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Identity.Features.MemberDirectory;

/// <summary>Implements <see cref="IMemberDirectory"/>; memberships are tenant-filtered, users are global.</summary>
internal sealed class MemberDirectory(IdentityDbContext db) : IMemberDirectory
{
    public async Task<IReadOnlyDictionary<Guid, MemberSummary>> FindAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().Select(UserId.From).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, MemberSummary>();
        }

        var members = await Members(ids).ToListAsync(cancellationToken);
        return members.ToDictionary(member => member.UserId.Value, member => new MemberSummary(member.UserId.Value, member.FullName));
    }

    public async Task<IReadOnlyList<MemberSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var members = await Members(null).ToListAsync(cancellationToken);
        return members
            .OrderBy(member => member.FullName, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true))
            .Select(member => new MemberSummary(member.UserId.Value, member.FullName))
            .ToList();
    }

    public async Task<IReadOnlyList<MemberSummary>> WithPermissionAsync(string permission, CancellationToken cancellationToken)
    {
        var members = await db.Memberships
            .Where(membership => membership.IsActive)
            .Join(db.Roles, membership => membership.RoleId, role => role.Id, (membership, role) => new { membership.UserId, role.Permissions })
            .Where(row => row.Permissions.Contains(permission) || row.Permissions.Contains(PermissionNames.All))
            .Join(db.Users, row => row.UserId, user => user.Id, (row, user) => new ActiveMember(user.Id, user.FullName))
            .ToListAsync(cancellationToken);
        return members.Select(member => new MemberSummary(member.UserId.Value, member.FullName)).ToList();
    }

    /// <summary>Filters before projecting: EF cannot translate a condition on a constructed record.</summary>
    private IQueryable<ActiveMember> Members(List<UserId>? only) =>
        db.Memberships
            .Where(membership => membership.IsActive && (only == null || only.Contains(membership.UserId)))
            .Join(db.Users, membership => membership.UserId, user => user.Id, (membership, user) => new ActiveMember(user.Id, user.FullName));

    private sealed record ActiveMember(UserId UserId, string FullName);
}
