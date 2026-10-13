using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Email;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Portal;
using Akiron.Modules.Portal.Domain;
using Akiron.Modules.Portal.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Portal.Features;

internal sealed record RequestLinkCommand(string Email);

internal sealed class RequestLinkValidator : AbstractValidator<RequestLinkCommand>
{
    public RequestLinkValidator() => RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(254);
}

internal sealed record EnterPortalCommand(string Token);

internal sealed class EnterPortalValidator : AbstractValidator<EnterPortalCommand>
{
    public EnterPortalValidator() => RuleFor(command => command.Token).NotEmpty().MaximumLength(128);
}

/// <summary>Who is signed in and what the agency tells every client (greeting, how to pay).</summary>
internal sealed record PortalMeResponse(
    string Name,
    string Email,
    Guid PartyId,
    string PartyName,
    string WorkspaceName,
    string? WelcomeNote,
    string? PaymentInstructions);

/// <summary>What entering the portal produced: the session secret for the cookie and when it ends.</summary>
internal sealed record PortalEntry(string SessionSecret, DateTimeOffset ExpiresAt);

/// <summary>
/// Signing in without a password: a single-use link sent by e-mail. An address may have access at
/// several agencies, so "send me a link" answers every matching account, each in its own tenant
/// scope, and never reveals whether an address is known.
/// </summary>
internal sealed class PortalAccessHandler(
    PortalDbContext db,
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    IPartyDirectory parties,
    IWorkspaceDirectory workspace,
    TimeProvider timeProvider)
{
    public async Task RequestLinkAsync(RequestLinkCommand command, CancellationToken cancellationToken)
    {
        var email = PortalUser.NormalizeEmail(command.Email);

        // No tenant is known yet: the address is the only key, so this one lookup crosses tenants.
        var accounts = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.Email == email && user.Status != PortalUserStatus.Disabled)
            .Select(user => new { user.Id, user.TenantId })
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(account.TenantId);
            var scoped = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var links = scope.ServiceProvider.GetRequiredService<PortalLinks>();

            var user = await scoped.Users.FirstAsync(candidate => candidate.Id == account.Id, cancellationToken);
            var secret = PortalSecret.New();
            scoped.SignInLinks.Add(PortalSignInLink.Issue(user.Id, secret, timeProvider.GetUtcNow() + PortalSignInLink.RequestedLifetime));
            await scoped.SaveChangesAsync(cancellationToken);

            var workspaceName = await scope.ServiceProvider.GetRequiredService<IWorkspaceDirectory>().CurrentNameAsync(cancellationToken);
            await links.SendAsync(user, workspaceName, links.LinkFor(secret), invitation: false, cancellationToken);
        }
    }

    /// <summary>Trades a link for a session. The link names the tenant; the scope is bound to it before anything is written.</summary>
    public async Task<Result<PortalEntry>> EnterAsync(EnterPortalCommand command, CancellationToken cancellationToken)
    {
        var hash = PortalSecret.Hash(command.Token.Trim());
        var now = timeProvider.GetUtcNow();
        var found = await db.SignInLinks.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(link => link.TokenHash == hash, cancellationToken);
        if (found is null || !found.IsUsableAt(now))
        {
            return PortalErrors.LinkInvalid;
        }

        tenantContext.Bind(found.TenantId);
        var link = await db.SignInLinks.FirstAsync(candidate => candidate.Id == found.Id, cancellationToken);
        var user = await db.Users.FirstAsync(candidate => candidate.Id == link.PortalUserId, cancellationToken);
        if (user.Status == PortalUserStatus.Disabled)
        {
            return PortalErrors.Disabled;
        }

        var secret = PortalSecret.New();
        var session = PortalSession.Start(user.Id, secret, now);
        link.Use(now);
        user.SignedIn(now);
        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return new PortalEntry(secret, session.ExpiresAt);
    }

    public async Task LeaveAsync(string? secret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return;
        }

        var hash = PortalSecret.Hash(secret);
        var session = await db.Sessions.FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);
        if (session is not null)
        {
            session.Revoke(timeProvider.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<PortalMeResponse> MeAsync(IPortalVisitor visitor, CancellationToken cancellationToken)
    {
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var party = await parties.FindAsync(visitor.PartyId, cancellationToken);
        return new PortalMeResponse(
            visitor.Name,
            visitor.Email,
            visitor.PartyId,
            party?.Name ?? string.Empty,
            await workspace.CurrentNameAsync(cancellationToken),
            settings?.WelcomeNote,
            settings?.PaymentInstructions);
    }
}

/// <summary>
/// Tells the client's portal users by e-mail when staff write to them or share a file, with a way
/// back into the portal. Sent once per event and person (the outbox may redeliver; a second
/// identical e-mail is the accepted cost, as with every e-mail here).
/// </summary>
internal sealed class ClientMessageEmail(PortalDbContext db, IWorkspaceDirectory workspace, PortalLinks links, IEmailSender emailSender)
    : IIntegrationEventConsumer<ClientMessagePosted>, IIntegrationEventConsumer<FileSharedWithClient>, IIntegrationEventConsumer<Akiron.Contracts.Social.ContentSentToClient>
{
    public Task HandleAsync(Akiron.Contracts.Social.ContentSentToClient integrationEvent, CancellationToken cancellationToken)
    {
        var listed = integrationEvent.Posts.Take(10).Select(post => $"- {post.PlannedDate:dd.MM.yyyy} · {post.Title}");
        var more = integrationEvent.Posts.Count > 10 ? $"\n… ve {integrationEvent.Posts.Count - 10} içerik daha" : string.Empty;
        return SendAsync(
            integrationEvent.PartyId,
            Subjects.ContentPost,
            Guid.Empty,
            name => integrationEvent.Posts.Count == 1
                ? $"{name}: bir içerik onayınızı bekliyor"
                : $"{name}: {integrationEvent.Posts.Count} içerik onayınızı bekliyor",
            $"Onayınızı bekleyen içerikler:\n{string.Join("\n", listed)}{more}",
            cancellationToken);
    }

    public Task HandleAsync(ClientMessagePosted integrationEvent, CancellationToken cancellationToken) =>
        integrationEvent.FromClient
            ? Task.CompletedTask
            : SendAsync(
                integrationEvent.PartyId,
                integrationEvent.SubjectType,
                integrationEvent.SubjectId,
                name => $"{name}: yeni mesaj — {integrationEvent.Title}",
                $"{integrationEvent.AuthorName}: {integrationEvent.Excerpt}",
                cancellationToken);

    public Task HandleAsync(FileSharedWithClient integrationEvent, CancellationToken cancellationToken) =>
        SendAsync(
            integrationEvent.PartyId,
            integrationEvent.SubjectType,
            integrationEvent.SubjectId,
            name => $"{name}: yeni dosya — {integrationEvent.Title}",
            $"Sizinle bir dosya paylaşıldı: {integrationEvent.FileName}",
            cancellationToken);

    private async Task SendAsync(Guid partyId, string subjectType, Guid subjectId, Func<string, string> subject, string body, CancellationToken cancellationToken)
    {
        var recipients = await db.Users.AsNoTracking()
            .Where(user => user.PartyId == partyId && user.Status == PortalUserStatus.Active)
            .ToListAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        var workspaceName = await workspace.CurrentNameAsync(cancellationToken);
        var page = subjectType == Subjects.WorkOrder
            ? $"{links.WebUrl}/portal/jobs/{subjectId}"
            : subjectType == Subjects.ContentPost
                ? subjectId == Guid.Empty ? $"{links.WebUrl}/portal/content" : $"{links.WebUrl}/portal/content/{subjectId}"
                : $"{links.WebUrl}/portal";
        foreach (var user in recipients)
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    subject(workspaceName),
                    $"""
                    Merhaba {user.FullName},

                    {body}

                    Portalda görmek için: {page}
                    """),
                cancellationToken);
        }
    }
}
