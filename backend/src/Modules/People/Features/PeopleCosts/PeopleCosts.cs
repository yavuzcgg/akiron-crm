using Akiron.Contracts.People;
using Akiron.Modules.People.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.People.Features;

/// <summary>Implements <see cref="IPeopleCosts"/> from the tenant's employee profiles.</summary>
internal sealed class PeopleCosts(PeopleDbContext db) : IPeopleCosts
{
    public async Task<IReadOnlyDictionary<Guid, decimal>> HourlyCostsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        return await db.Employees
            .Where(employee => ids.Contains(employee.UserId) && employee.HourlyCost != null)
            .ToDictionaryAsync(employee => employee.UserId, employee => employee.HourlyCost!.Value, cancellationToken);
    }
}
