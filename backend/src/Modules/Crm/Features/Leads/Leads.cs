using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Identity;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Features.CreateParty;
using Akiron.Modules.Crm.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.Leads;

internal static class LeadApi
{
    public static readonly string[] Stages = ["new", "contacted", "meeting", "proposal", "won", "lost"];

    public static readonly string[] Sources = ["website", "web_form", "referral", "instagram", "whatsapp", "phone", "email", "event", "other"];

    public static string Stage(LeadStage stage) => Stages[(int)stage];

    public static LeadStage ParseStage(string value) => (LeadStage)Array.IndexOf(Stages, value);

    public static string Source(LeadSource source) => Sources[(int)source];

    public static LeadSource ParseSource(string value) => (LeadSource)Array.IndexOf(Sources, value);

    public const double RankStep = 1024;
}

internal sealed record LeadCommand(
    string Title,
    string? CompanyName = null,
    string? ContactName = null,
    string? Email = null,
    string? Phone = null,
    string Source = "other",
    Guid? PartyId = null,
    decimal? EstimatedValue = null,
    string Currency = "TRY",
    DateOnly? ExpectedCloseDate = null,
    Guid? OwnerUserId = null,
    string? NextStep = null,
    DateOnly? NextStepDate = null,
    string? Notes = null);

internal sealed class LeadValidator : AbstractValidator<LeadCommand>
{
    public LeadValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Lead.TitleMaxLength);
        RuleFor(command => command.CompanyName).MaximumLength(200);
        RuleFor(command => command.ContactName).MaximumLength(200);
        RuleFor(command => command.Email!).EmailAddress().MaximumLength(254).When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Phone).MaximumLength(30);
        RuleFor(command => command.Source).Must(source => LeadApi.Sources.Contains(source)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.EstimatedValue).InclusiveBetween(0m, 1_000_000_000m);
        RuleFor(command => command.Currency).Must(Currency.IsSupported).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.NextStep).MaximumLength(300);
        RuleFor(command => command.Notes).MaximumLength(Lead.NotesMaxLength);
    }
}

/// <param name="Index">Position in the target column (0 = top); null puts it at the bottom.</param>
/// <param name="LostReason">Required when moving to <c>lost</c>.</param>
internal sealed record MoveLeadCommand(string Stage, int? Index = null, string? LostReason = null);

internal sealed class MoveLeadValidator : AbstractValidator<MoveLeadCommand>
{
    public MoveLeadValidator()
    {
        RuleFor(command => command.Stage).Must(stage => LeadApi.Stages.Contains(stage)).WithErrorCode("validation.invalid_value");
        RuleFor(command => command.Index).GreaterThanOrEqualTo(0);
        RuleFor(command => command.LostReason).NotEmpty().When(command => command.Stage == "lost");
        RuleFor(command => command.LostReason).MaximumLength(500);
    }
}

/// <param name="PartyId">Link to an existing party instead of creating one.</param>
internal sealed record ConvertLeadCommand(Guid? PartyId = null);

internal sealed record LeadFilter(string? View = null, string? Search = null, Guid? OwnerId = null, Guid? PartyId = null);

internal sealed class LeadFilterValidator : AbstractValidator<LeadFilter>
{
    public LeadFilterValidator()
    {
        RuleFor(filter => filter.View).Must(view => view is null or "open" or "closed").WithErrorCode("validation.invalid_value");
        RuleFor(filter => filter.Search).MaximumLength(100);
    }
}

internal sealed record LeadResponse(
    Guid Id,
    string Title,
    string? CompanyName,
    string? ContactName,
    string? Email,
    string? Phone,
    Guid? PartyId,
    string? PartyName,
    string Source,
    string Stage,
    double Rank,
    decimal? EstimatedValue,
    string Currency,
    DateOnly? ExpectedCloseDate,
    Guid? OwnerUserId,
    string? OwnerName,
    string? NextStep,
    DateOnly? NextStepDate,
    string? Notes,
    string? LostReason,
    Guid? QuoteId,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt);

internal sealed record ConvertLeadResponse(LeadResponse Lead, Guid PartyId, bool PartyCreated);

/// <summary>The lead funnel: the board, adding and editing leads, moving them, converting them to parties.</summary>
internal sealed class LeadsHandler(
    CrmDbContext db,
    IMemberDirectory members,
    CreatePartyHandler createParty,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const int MaxOpen = 500;
    private const int MaxClosed = 200;

    public async Task<IReadOnlyList<LeadResponse>> ListAsync(LeadFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Leads.AsNoTracking();
        if (filter.OwnerId is { } ownerId)
        {
            query = query.Where(lead => lead.OwnerUserId == ownerId);
        }

        if (filter.PartyId is { } partyId)
        {
            query = query.Where(lead => lead.PartyId == partyId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = "%" + filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(lead => EF.Functions.ILike(lead.Title, pattern, "\\")
                || (lead.CompanyName != null && EF.Functions.ILike(lead.CompanyName, pattern, "\\"))
                || (lead.ContactName != null && EF.Functions.ILike(lead.ContactName, pattern, "\\"))
                || (lead.Email != null && EF.Functions.ILike(lead.Email, pattern, "\\")));
        }

        var leads = filter.View == "closed"
            ? await query.Where(lead => lead.Stage == LeadStage.Won || lead.Stage == LeadStage.Lost)
                .OrderByDescending(lead => lead.ClosedAt).Take(MaxClosed).ToListAsync(cancellationToken)
            : filter.View == "open"
                ? await query.Where(lead => lead.Stage != LeadStage.Won && lead.Stage != LeadStage.Lost)
                    .OrderBy(lead => lead.Stage).ThenBy(lead => lead.Rank).Take(MaxOpen).ToListAsync(cancellationToken)
                : await query.OrderByDescending(lead => lead.CreatedAt).Take(MaxOpen).ToListAsync(cancellationToken);
        return await ToResponsesAsync(leads, cancellationToken);
    }

    public async Task<Result<LeadResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var lead = await FindAsync(id, cancellationToken);
        return lead is null ? LeadErrors.NotFound : await ToResponseAsync(lead, cancellationToken);
    }

    public async Task<Result<LeadResponse>> CreateAsync(LeadCommand command, CancellationToken cancellationToken)
    {
        var details = await DetailsAsync(command, cancellationToken);
        if (!details.IsSuccess)
        {
            return details.Error;
        }

        if (command.PartyId is { } partyId && !await db.Parties.AnyAsync(party => party.Id == PartyId.From(partyId), cancellationToken))
        {
            return LeadErrors.PartyNotFound;
        }

        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        var lead = Lead.Create(details.Value, command.PartyId, await RankAtAsync(LeadStage.New, null, null, cancellationToken));
        db.Leads.Add(lead);
        db.Publish(new LeadCreated(
            tenantContext.TenantId, timeProvider.GetUtcNow(), lead.Id.Value, lead.Title, lead.CompanyName, lead.ContactName,
            LeadApi.Source(lead.Source), lead.PartyId, lead.OwnerUserId, userId.Value, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(lead, cancellationToken);
    }

    public async Task<Result<LeadResponse>> UpdateAsync(Guid id, LeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await FindAsync(id, cancellationToken);
        if (lead is null)
        {
            return LeadErrors.NotFound;
        }

        var details = await DetailsAsync(command, cancellationToken);
        if (!details.IsSuccess)
        {
            return details.Error;
        }

        if (command.PartyId is { } partyId && partyId != lead.PartyId)
        {
            if (!await db.Parties.AnyAsync(party => party.Id == PartyId.From(partyId), cancellationToken))
            {
                return LeadErrors.PartyNotFound;
            }

            lead.LinkParty(partyId);
        }

        lead.Update(details.Value);
        await db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(lead, cancellationToken);
    }

    public async Task<Result<LeadResponse>> MoveAsync(Guid id, MoveLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await FindAsync(id, cancellationToken);
        if (lead is null)
        {
            return LeadErrors.NotFound;
        }

        var stage = LeadApi.ParseStage(command.Stage);
        var now = timeProvider.GetUtcNow();
        var rank = await RankAtAsync(stage, lead.Id, command.Index, cancellationToken);
        if (lead.MoveTo(stage, rank, now, command.LostReason) is { } from)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
            db.Publish(new LeadMoved(
                lead.TenantId, now, lead.Id.Value, lead.Title, lead.PartyId, LeadApi.Stage(from), LeadApi.Stage(stage),
                lead.LostReason, userId.Value, currentUser.DisplayName));
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(lead, cancellationToken);
    }

    /// <summary>
    /// Makes the lead a customer: links an existing party, or creates one from the lead (company or person,
    /// with its contact). The party gets its own C-code and timeline; the lead stays on the board.
    /// </summary>
    public async Task<Result<ConvertLeadResponse>> ConvertAsync(Guid id, ConvertLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await FindAsync(id, cancellationToken);
        if (lead is null)
        {
            return LeadErrors.NotFound;
        }

        if (lead.PartyId is not null && command.PartyId is null)
        {
            return LeadErrors.AlreadyConverted;
        }

        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        Party party;
        var created = false;
        if (command.PartyId is { } partyId)
        {
            var existing = await db.Parties.FirstOrDefaultAsync(candidate => candidate.Id == PartyId.From(partyId), cancellationToken);
            if (existing is null)
            {
                return LeadErrors.PartyNotFound;
            }

            party = existing;
        }
        else
        {
            var isCompany = lead.CompanyName is not null;
            var result = await createParty.HandleAsync(
                new CreatePartyCommand(
                    isCompany ? PartyKinds.Company : PartyKinds.Person,
                    lead.CompanyName ?? lead.ContactName ?? lead.Title,
                    IsCustomer: true,
                    IsSupplier: false,
                    Email: isCompany ? null : lead.Email,
                    Phone: isCompany ? null : lead.Phone),
                cancellationToken);
            if (!result.IsSuccess)
            {
                return result.Error;
            }

            party = await db.Parties.FirstAsync(candidate => candidate.Id == PartyId.From(result.Value.Id), cancellationToken);
            created = true;

            if (isCompany && (lead.ContactName is not null || lead.Email is not null))
            {
                var contact = PartyContact.Create(party.Id, new PartyContactDetails(lead.ContactName ?? lead.Email!, null, lead.Email, lead.Phone, IsPrimary: true));
                db.Contacts.Add(contact);
                db.Publish(new PartyContactAdded(party.TenantId, timeProvider.GetUtcNow(), party.Id.Value, contact.Id.Value, party.Name, contact.FullName, userId.Value, currentUser.DisplayName));
            }
        }

        lead.LinkParty(party.Id.Value);
        db.Publish(new LeadConverted(lead.TenantId, timeProvider.GetUtcNow(), lead.Id.Value, lead.Title, party.Id.Value, party.Name, created, userId.Value, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        return new ConvertLeadResponse(await ToResponseAsync(lead, cancellationToken), party.Id.Value, created);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var lead = await FindAsync(id, cancellationToken);
        if (lead is null)
        {
            return LeadErrors.NotFound;
        }

        db.Leads.Remove(lead);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Where a card lands in a column: between its neighbours, spacing the column out when there is no room.</summary>
    internal async Task<double> RankAtAsync(LeadStage stage, LeadId? moving, int? index, CancellationToken cancellationToken)
    {
        var others = await db.Leads
            .Where(lead => lead.Stage == stage && (moving == null || lead.Id != moving))
            .OrderBy(lead => lead.Rank)
            .Take(MaxOpen)
            .ToListAsync(cancellationToken);
        return LeadRanks.At(others, index);
    }

    private async Task<Result<LeadDetails>> DetailsAsync(LeadCommand command, CancellationToken cancellationToken)
    {
        string? ownerName = null;
        if (command.OwnerUserId is { } ownerId)
        {
            var found = await members.FindAsync([ownerId], cancellationToken);
            if (!found.TryGetValue(ownerId, out var owner))
            {
                return LeadErrors.OwnerNotMember;
            }

            ownerName = owner.FullName;
        }

        return new LeadDetails(
            command.Title, command.CompanyName, command.ContactName, command.Email, command.Phone, LeadApi.ParseSource(command.Source),
            command.EstimatedValue, Currency.From(command.Currency), command.ExpectedCloseDate, command.OwnerUserId, ownerName,
            command.NextStep, command.NextStepDate, command.Notes);
    }

    private Task<Lead?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var leadId = LeadId.From(id);
        return db.Leads.FirstOrDefaultAsync(lead => lead.Id == leadId, cancellationToken);
    }

    private async Task<LeadResponse> ToResponseAsync(Lead lead, CancellationToken cancellationToken) => (await ToResponsesAsync([lead], cancellationToken))[0];

    private async Task<IReadOnlyList<LeadResponse>> ToResponsesAsync(IReadOnlyList<Lead> leads, CancellationToken cancellationToken)
    {
        var partyIds = leads.Where(lead => lead.PartyId is not null).Select(lead => PartyId.From(lead.PartyId!.Value)).Distinct().ToList();
        var names = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Parties.IncludingArchived().Where(party => partyIds.Contains(party.Id))
                .ToDictionaryAsync(party => party.Id.Value, party => party.Name, cancellationToken);
        return leads.Select(lead => LeadViews.ToResponse(lead, lead.PartyId is { } id ? names.GetValueOrDefault(id) : null)).ToList();
    }
}

internal static class LeadViews
{
    public static LeadResponse ToResponse(Lead lead, string? partyName) => new(
        lead.Id.Value,
        lead.Title,
        lead.CompanyName,
        lead.ContactName,
        lead.Email,
        lead.Phone,
        lead.PartyId,
        partyName,
        LeadApi.Source(lead.Source),
        LeadApi.Stage(lead.Stage),
        lead.Rank,
        lead.EstimatedValue,
        lead.Currency.Code,
        lead.ExpectedCloseDate,
        lead.OwnerUserId,
        lead.OwnerName,
        lead.NextStep,
        lead.NextStepDate,
        lead.Notes,
        lead.LostReason,
        lead.QuoteId,
        lead.ClosedAt,
        lead.CreatedAt);
}

internal static class LeadRanks
{
    /// <summary>The rank for position <paramref name="index"/> among <paramref name="others"/> (sorted by rank).</summary>
    public static double At(IReadOnlyList<Lead> others, int? index)
    {
        var position = Math.Clamp(index ?? others.Count, 0, others.Count);
        if (others.Count == 0)
        {
            return LeadApi.RankStep;
        }

        if (position == others.Count)
        {
            return others[^1].Rank + LeadApi.RankStep;
        }

        if (position == 0)
        {
            return others[0].Rank - LeadApi.RankStep;
        }

        var before = others[position - 1].Rank;
        var after = others[position].Rank;
        if (after - before > 1e-6)
        {
            return (before + after) / 2;
        }

        for (var i = 0; i < others.Count; i++)
        {
            others[i].Rerank((i < position ? i + 1 : i + 2) * LeadApi.RankStep);
        }

        return (position + 1) * LeadApi.RankStep;
    }
}

internal static class LeadErrors
{
    public static readonly Error NotFound = Error.NotFound("crm.lead.not_found", "No such lead in this organisation.");

    public static readonly Error PartyNotFound = Error.Validation("crm.lead.party_not_found", "The party does not exist in this organisation.");

    public static readonly Error OwnerNotMember = Error.Validation("crm.lead.owner_not_member", "The owner must be a member of this organisation.");

    public static readonly Error AlreadyConverted = Error.Rule("crm.lead.already_converted", "The lead already belongs to a party.");
}
