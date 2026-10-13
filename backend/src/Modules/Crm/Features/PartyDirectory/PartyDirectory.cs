using Akiron.Contracts.Crm;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.PartyDirectory;

/// <summary>A party is itself something its client may write on or send files to (a payment receipt, say).</summary>
internal sealed class PartyPortalSubject(CrmDbContext db) : Akiron.Contracts.Portal.IPortalSubject
{
    public string SubjectType => Akiron.Contracts.Subjects.Party;

    public async Task<Akiron.Contracts.Portal.PortalSubjectInfo?> FindAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var id = PartyId.From(subjectId);
        var name = await db.Parties.Where(party => party.Id == id).Select(party => party.Name).FirstOrDefaultAsync(cancellationToken);

        // No particular member of staff: whoever handles the kind of thing the client sent is told.
        return name is null ? null : new Akiron.Contracts.Portal.PortalSubjectInfo(subjectId, name, []);
    }
}

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

    public async Task<string?> BillingEmailAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var id = PartyId.From(partyId);
        var contact = await db.Contacts
            .Where(candidate => candidate.PartyId == id && candidate.Email != null)
            .OrderByDescending(candidate => candidate.IsPrimary).ThenBy(candidate => candidate.CreatedAt)
            .Select(candidate => candidate.Email)
            .FirstOrDefaultAsync(cancellationToken);
        return contact ?? await db.Parties.Where(party => party.Id == id).Select(party => party.Email).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> BillingPhoneAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var id = PartyId.From(partyId);
        var contact = await db.Contacts
            .Where(candidate => candidate.PartyId == id && candidate.Phone != null)
            .OrderByDescending(candidate => candidate.IsPrimary).ThenBy(candidate => candidate.CreatedAt)
            .Select(candidate => candidate.Phone)
            .FirstOrDefaultAsync(cancellationToken);
        return contact ?? await db.Parties.Where(party => party.Id == id).Select(party => party.Phone).FirstOrDefaultAsync(cancellationToken);
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
