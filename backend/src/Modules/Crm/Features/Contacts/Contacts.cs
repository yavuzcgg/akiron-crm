using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.Contracts.Crm;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.Contacts;

internal sealed record ContactCommand(string FullName, string? Title = null, string? Email = null, string? Phone = null, bool IsPrimary = false);

internal sealed class ContactValidator : AbstractValidator<ContactCommand>
{
    public ContactValidator()
    {
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(PartyContact.FullNameMaxLength);
        RuleFor(command => command.Title).MaximumLength(100);
        RuleFor(command => command.Email!).EmailAddress().MaximumLength(254).When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Phone).MaximumLength(30);
    }
}

/// <summary>Adds, edits and removes a party's contacts. Marking one primary unmarks the others.</summary>
internal sealed class ContactsHandler(CrmDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<Result<PartyContactResponse>> AddAsync(Guid partyId, ContactCommand command, CancellationToken cancellationToken)
    {
        var id = PartyId.From(partyId);
        var party = await db.Parties.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (party is null)
        {
            return CrmErrors.PartyNotFound;
        }

        var contact = PartyContact.Create(party.Id, ToDetails(command));
        await ClearOtherPrimariesAsync(party.Id, contact, cancellationToken);

        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        db.Contacts.Add(contact);
        db.Publish(new PartyContactAdded(party.TenantId, timeProvider.GetUtcNow(), party.Id.Value, contact.Id.Value, party.Name, contact.FullName, userId.Value, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        return PartyResponse.ToResponse(contact);
    }

    public async Task<Result<PartyContactResponse>> UpdateAsync(Guid partyId, Guid contactId, ContactCommand command, CancellationToken cancellationToken)
    {
        var contact = await FindAsync(partyId, contactId, cancellationToken);
        if (contact is null)
        {
            return CrmErrors.ContactNotFound;
        }

        contact.Update(ToDetails(command));
        await ClearOtherPrimariesAsync(contact.PartyId, contact, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return PartyResponse.ToResponse(contact);
    }

    public async Task<Result<bool>> RemoveAsync(Guid partyId, Guid contactId, CancellationToken cancellationToken)
    {
        var contact = await FindAsync(partyId, contactId, cancellationToken);
        if (contact is null)
        {
            return CrmErrors.ContactNotFound;
        }

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<PartyContact?> FindAsync(Guid partyId, Guid contactId, CancellationToken cancellationToken)
    {
        var party = PartyId.From(partyId);
        var id = PartyContactId.From(contactId);
        return db.Contacts.FirstOrDefaultAsync(contact => contact.Id == id && contact.PartyId == party, cancellationToken);
    }

    private async Task ClearOtherPrimariesAsync(PartyId partyId, PartyContact contact, CancellationToken cancellationToken)
    {
        if (!contact.IsPrimary)
        {
            return;
        }

        var others = await db.Contacts
            .Where(other => other.PartyId == partyId && other.Id != contact.Id && other.IsPrimary)
            .ToListAsync(cancellationToken);
        others.ForEach(other => other.ClearPrimary());
    }

    private static PartyContactDetails ToDetails(ContactCommand command) =>
        new(command.FullName, command.Title, command.Email, command.Phone, command.IsPrimary);
}
