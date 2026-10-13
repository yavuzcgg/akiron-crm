using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Email;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Portal;
using Akiron.Modules.Portal.Domain;
using Akiron.Modules.Portal.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Akiron.Modules.Portal.Features;

internal sealed record InvitePortalUserCommand(Guid PartyId, string FullName, string Email);

internal sealed class InvitePortalUserValidator : AbstractValidator<InvitePortalUserCommand>
{
    public InvitePortalUserValidator()
    {
        RuleFor(command => command.PartyId).NotEmpty();
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(PortalUser.NameMaxLength);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(254);
    }
}

internal sealed record PortalUserResponse(Guid Id, Guid PartyId, string FullName, string Email, string Status, DateTimeOffset? LastSignInAt, DateTimeOffset CreatedAt);

/// <param name="Link">The sign-in link, shown once so it can also be shared by hand (WhatsApp); only its hash is stored.</param>
internal sealed record PortalInvitationResponse(PortalUserResponse User, string Link);

internal sealed record PortalSettingsCommand(string? WelcomeNote = null, string? PaymentInstructions = null);

internal sealed class PortalSettingsValidator : AbstractValidator<PortalSettingsCommand>
{
    public PortalSettingsValidator()
    {
        RuleFor(command => command.WelcomeNote).MaximumLength(PortalSettings.TextMaxLength);
        RuleFor(command => command.PaymentInstructions).MaximumLength(PortalSettings.TextMaxLength);
    }
}

internal sealed record PortalSettingsResponse(string? WelcomeNote, string? PaymentInstructions);

internal static class PortalErrors
{
    public static readonly Error UserNotFound = Error.NotFound("portal.user.not_found", "No such portal user in this organisation.");

    public static readonly Error PartyNotFound = Error.Validation("portal.user.party_not_found", "The party does not exist in this organisation.");

    public static readonly Error EmailInUse = Error.Conflict("portal.user.email_in_use", "This address already has portal access for another party.");

    public static readonly Error Disabled = Error.Rule("portal.user.disabled", "Portal access is switched off for this person.");

    public static readonly Error LinkInvalid = Error.NotFound("portal.link.invalid", "This sign-in link is invalid, was already used, or has expired.");
}

/// <summary>Writes the sign-in e-mail; the same text serves invitations and requested links.</summary>
internal sealed class PortalLinks(IEmailSender emailSender, IConfiguration configuration)
{
    public string LinkFor(string secret) => $"{WebUrl}/portal/enter?token={secret}";

    public string WebUrl => configuration["App:WebUrl"]?.TrimEnd('/') ?? "http://localhost:3100";

    public Task SendAsync(PortalUser user, string workspaceName, string link, bool invitation, CancellationToken cancellationToken) =>
        emailSender.SendAsync(
            new EmailMessage(
                user.Email,
                invitation ? $"{workspaceName} müşteri portalı" : $"{workspaceName} portal giriş bağlantınız",
                $"""
                Merhaba {user.FullName},

                {(invitation
                    ? $"{workspaceName} sizi müşteri portalına davet etti. İşlerinizin durumunu, tekliflerinizi ve ödemelerinizi buradan izleyebilirsiniz."
                    : $"{workspaceName} müşteri portalına giriş bağlantınız aşağıda.")}

                {link}

                {(invitation ? "Bağlantı 7 gün geçerlidir ve bir kez kullanılır." : "Bağlantı 20 dakika geçerlidir ve bir kez kullanılır.")}
                Parola gerekmez; yeni bağlantıyı her zaman portal giriş sayfasından isteyebilirsiniz.

                ---
                {(invitation ? $"{workspaceName} invited you to their client portal." : $"Your sign-in link for the {workspaceName} client portal.")}
                {link}
                """),
            cancellationToken);
}

/// <summary>Staff side: who of a client's people may open the portal, inviting them, switching access off and on.</summary>
internal sealed class PortalUsersHandler(
    PortalDbContext db,
    IPartyDirectory parties,
    IWorkspaceDirectory workspace,
    PortalLinks links,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<PortalUserResponse>> ListAsync(Guid? partyId, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        if (partyId is { } party)
        {
            query = query.Where(user => user.PartyId == party);
        }

        var users = await query.OrderBy(user => user.FullName).Take(500).ToListAsync(cancellationToken);
        return users.Select(ToResponse).ToList();
    }

    /// <summary>Gives a contact access (or sends a fresh link to one who already has it) and e-mails the link.</summary>
    public async Task<Result<PortalInvitationResponse>> InviteAsync(InvitePortalUserCommand command, CancellationToken cancellationToken)
    {
        var party = await parties.FindAsync(command.PartyId, cancellationToken);
        if (party is null)
        {
            return PortalErrors.PartyNotFound;
        }

        var email = PortalUser.NormalizeEmail(command.Email);
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        var actor = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
        var now = timeProvider.GetUtcNow();
        if (user is null)
        {
            user = PortalUser.Invite(party.PartyId, command.FullName, email);
            db.Users.Add(user);
            db.Publish(new PortalUserInvited(tenantContext.TenantId, now, user.Id.Value, party.PartyId, user.FullName, user.Email, actor.Value, currentUser.DisplayName));
        }
        else if (user.PartyId != party.PartyId)
        {
            return PortalErrors.EmailInUse;
        }
        else if (user.Status == PortalUserStatus.Disabled)
        {
            return PortalErrors.Disabled;
        }

        return await IssueAsync(user, now, cancellationToken);
    }

    public async Task<Result<PortalInvitationResponse>> ResendAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id, cancellationToken);
        if (user is null)
        {
            return PortalErrors.UserNotFound;
        }

        return user.Status == PortalUserStatus.Disabled ? PortalErrors.Disabled : await IssueAsync(user, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Switching off ends every open session at once; switching on restores the earlier state.</summary>
    public async Task<Result<PortalUserResponse>> SetAccessAsync(Guid id, bool enabled, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id, cancellationToken);
        if (user is null)
        {
            return PortalErrors.UserNotFound;
        }

        if (enabled ? user.Enable() : user.Disable())
        {
            var now = timeProvider.GetUtcNow();
            if (!enabled)
            {
                foreach (var session in await db.Sessions.Where(session => session.PortalUserId == user.Id && session.RevokedAt == null).ToListAsync(cancellationToken))
                {
                    session.Revoke(now);
                }
            }

            var actor = currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.");
            db.Publish(new PortalAccessChanged(user.TenantId, now, user.Id.Value, user.PartyId, user.FullName, enabled, actor.Value, currentUser.DisplayName));
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(user);
    }

    public async Task<PortalSettingsResponse> SettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return new PortalSettingsResponse(settings?.WelcomeNote, settings?.PaymentInstructions);
    }

    public async Task<PortalSettingsResponse> SaveSettingsAsync(PortalSettingsCommand command, CancellationToken cancellationToken)
    {
        var settings = await db.Settings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = PortalSettings.Create();
            db.Settings.Add(settings);
        }

        settings.Update(command.WelcomeNote, command.PaymentInstructions);
        await db.SaveChangesAsync(cancellationToken);
        return new PortalSettingsResponse(settings.WelcomeNote, settings.PaymentInstructions);
    }

    internal static PortalUserResponse ToResponse(PortalUser user) => new(
        user.Id.Value, user.PartyId, user.FullName, user.Email, user.Status.ToString().ToLowerInvariant(), user.LastSignInAt, user.CreatedAt);

    private async Task<PortalInvitationResponse> IssueAsync(PortalUser user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var secret = PortalSecret.New();
        db.SignInLinks.Add(PortalSignInLink.Issue(user.Id, secret, now + PortalSignInLink.InvitationLifetime));
        await db.SaveChangesAsync(cancellationToken);

        var link = links.LinkFor(secret);
        await links.SendAsync(user, await workspace.CurrentNameAsync(cancellationToken), link, invitation: true, cancellationToken);
        return new PortalInvitationResponse(ToResponse(user), link);
    }

    private Task<PortalUser?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = PortalUserId.From(id);
        return db.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }
}
