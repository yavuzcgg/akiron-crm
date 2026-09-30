using Akiron.BuildingBlocks.Domain;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.GetParty;

internal sealed class GetPartyHandler(CrmDbContext db)
{
    public async Task<Result<PartyResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var partyId = PartyId.From(id);
        var party = await db.Parties.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == partyId, cancellationToken);
        if (party is null)
        {
            return CrmErrors.PartyNotFound;
        }

        var contacts = await db.Contacts.AsNoTracking().Where(contact => contact.PartyId == partyId).ToListAsync(cancellationToken);
        return PartyResponse.From(party, contacts);
    }
}
