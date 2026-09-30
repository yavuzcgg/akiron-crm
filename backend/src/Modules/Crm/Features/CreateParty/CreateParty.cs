using System.Globalization;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Crm;
using Akiron.Modules.Crm.Domain;
using Akiron.Modules.Crm.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Crm.Features.CreateParty;

internal sealed record CreatePartyCommand(
    string Kind,
    string Name,
    bool IsCustomer,
    bool IsSupplier,
    string? Code = null,
    string? TaxNumber = null,
    string? TaxOffice = null,
    string? Email = null,
    string? Phone = null,
    string? Website = null,
    string? City = null,
    string? District = null,
    string? AddressLine = null) : IPartyInput;

internal sealed class CreatePartyValidator : AbstractValidator<CreatePartyCommand>
{
    public CreatePartyValidator()
    {
        PartyInputRules.Apply(this);
        RuleFor(command => command.Code).MaximumLength(Party.CodeMaxLength);
    }
}

/// <summary>
/// Adds a party. Without a code the next one in the tenant's C00001 series is used; a code typed by
/// hand is kept as is, since accountants often bring their Logo codes.
/// </summary>
internal sealed class CreatePartyHandler(CrmDbContext db, ITenantContext tenantContext, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private const string CodeSeries = "party";

    public async Task<Result<PartyResponse>> HandleAsync(CreatePartyCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var code = string.IsNullOrWhiteSpace(command.Code) ? await NextFreeCodeAsync(cancellationToken) : command.Code.Trim();
        if (await db.Parties.AnyAsync(party => party.Code == code, cancellationToken))
        {
            return CrmErrors.CodeTaken;
        }

        var party = Party.Create(code, PartyInputRules.ToDetails(command));
        var userId = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");

        db.Parties.Add(party);
        db.Publish(new PartyCreated(
            tenantContext.TenantId,
            timeProvider.GetUtcNow(),
            party.Id.Value,
            party.Code,
            party.Name,
            PartyKinds.ToApi(party.Kind),
            party.IsCustomer,
            party.IsSupplier,
            userId.Value,
            currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return PartyResponse.From(party, []);
    }

    /// <summary>Skips numbers already taken by hand-typed codes, so the series never collides.</summary>
    private async Task<string> NextFreeCodeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var sequence = await db.NextDocumentSequenceAsync(CodeSeries, 0, cancellationToken);
            var code = string.Create(CultureInfo.InvariantCulture, $"C{sequence:00000}");
            if (!await db.Parties.AnyAsync(party => party.Code == code, cancellationToken))
            {
                return code;
            }
        }
    }
}
