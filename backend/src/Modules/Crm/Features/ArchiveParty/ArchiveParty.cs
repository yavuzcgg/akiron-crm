using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.Contracts.Crm;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.ArchiveParty;

/// <summary>
/// Hides a party from lists (soft delete); its timeline, files and audit trail stay. Once invoices
/// exist, a party with an open balance will refuse to be archived (Finance, Phase 4).
/// </summary>
internal sealed class ArchivePartyHandler(CrmDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<Result<bool>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var partyId = PartyId.From(id);
        var party = await db.Parties.FirstOrDefaultAsync(candidate => candidate.Id == partyId, cancellationToken);
        if (party is null)
        {
            return CrmErrors.PartyNotFound;
        }

        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        db.Parties.Remove(party);
        db.Publish(new PartyArchived(party.TenantId, timeProvider.GetUtcNow(), party.Id.Value, party.Name, userId.Value, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
