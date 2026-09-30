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
}
