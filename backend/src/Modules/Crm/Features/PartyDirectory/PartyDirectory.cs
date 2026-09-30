using Akiron.Contracts.Crm;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.PartyDirectory;

/// <summary>Implements <see cref="IPartyDirectory"/> on the tenant-filtered, archive-filtered parties.</summary>
internal sealed class PartyDirectory(CrmDbContext db) : IPartyDirectory
{
    public async Task<PartySummary?> FindAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var id = PartyId.From(partyId);
        return await db.Parties
            .Where(party => party.Id == id)
            .Select(party => new PartySummary(party.Id.Value, party.Code, party.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartySummary>> SearchCustomersAsync(string? search, int limit, CancellationToken cancellationToken)
    {
        var query = db.Parties.Where(party => party.IsCustomer);
        var folded = TurkishText.Fold(search);
        if (folded.Length > 0)
        {
            var pattern = TurkishText.ContainsPattern(folded);
            query = query.Where(party => EF.Functions.Like(party.SearchText, pattern, TurkishText.LikeEscape));
        }

        return await query
            .OrderBy(party => party.Name)
            .Take(limit)
            .Select(party => new PartySummary(party.Id.Value, party.Code, party.Name))
            .ToListAsync(cancellationToken);
    }
}
