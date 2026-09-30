using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.Contracts.Crm;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.UpdateParty;

internal sealed record UpdatePartyCommand(
    string Kind,
    string Name,
    bool IsCustomer,
    bool IsSupplier,
    string? TaxNumber = null,
    string? TaxOffice = null,
    string? Email = null,
    string? Phone = null,
    string? Website = null,
    string? City = null,
    string? District = null,
    string? AddressLine = null) : IPartyInput;

internal sealed class UpdatePartyValidator : AbstractValidator<UpdatePartyCommand>
{
    public UpdatePartyValidator() => PartyInputRules.Apply(this);
}

/// <summary>Replaces the editable details; the timeline gets one line naming the fields that changed.</summary>
internal sealed class UpdatePartyHandler(CrmDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<Result<PartyResponse>> HandleAsync(Guid id, UpdatePartyCommand command, CancellationToken cancellationToken)
    {
        var partyId = PartyId.From(id);
        var party = await db.Parties.FirstOrDefaultAsync(candidate => candidate.Id == partyId, cancellationToken);
        if (party is null)
        {
            return CrmErrors.PartyNotFound;
        }

        var changed = party.Update(PartyInputRules.ToDetails(command));
        if (changed.Count > 0)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
            db.Publish(new PartyUpdated(party.TenantId, timeProvider.GetUtcNow(), party.Id.Value, party.Name, changed, userId.Value, currentUser.DisplayName));
            await db.SaveChangesAsync(cancellationToken);
        }

        var contacts = await db.Contacts.Where(contact => contact.PartyId == partyId).ToListAsync(cancellationToken);
        return PartyResponse.From(party, contacts);
    }
}
