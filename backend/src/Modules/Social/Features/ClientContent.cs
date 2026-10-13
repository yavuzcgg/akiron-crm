using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Scheduling;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts;
using Akiron.Contracts.Portal;
using Akiron.Contracts.Social;
using Akiron.Modules.Social.Domain;
using Akiron.Modules.Social.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Social.Features;

/// <summary>A post as its client sees it: what goes out, where and when, and where its approval stands. No internal notes, no author.</summary>
internal sealed record ClientContentPost(
    Guid Id,
    string Title,
    string? Caption,
    string? Hashtags,
    IReadOnlyList<string> Platforms,
    string Format,
    DateOnly PlannedDate,
    TimeOnly? PlannedTime,
    string Status,
    int Revision,
    string? ClientNote,
    string? DecidedByName,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? PublishedAt,
    string? PublishedUrl);

internal sealed record RequestChangesCommand(string Note);

internal sealed class RequestChangesValidator : AbstractValidator<RequestChangesCommand>
{
    public RequestChangesValidator() => RuleFor(command => command.Note).NotEmpty().MaximumLength(ContentPost.NoteMaxLength);
}

/// <summary>
/// The client's side of the calendar: posts sent to them, approve or ask for changes. Drafts and
/// posts in internal review do not exist for the client.
/// </summary>
internal sealed class ClientContentHandler(SocialDbContext db, IPortalVisitor visitor, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ClientContentPost>> ListAsync(CancellationToken cancellationToken)
    {
        var partyId = visitor.PartyId;
        var since = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddMonths(-3);
        var posts = await Visible(db.Posts.AsNoTracking())
            .Where(post => post.PartyId == partyId && (post.PlannedDate >= since || post.Status == ContentStatus.ClientReview))
            .OrderBy(post => post.PlannedDate).ThenBy(post => post.PlannedTime)
            .Take(300)
            .ToListAsync(cancellationToken);
        return posts.Select(ToClient).ToList();
    }

    public async Task<Result<ClientContentPost>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var post = await FindAsync(id, cancellationToken);
        return post is null ? SocialErrors.PostNotFound : ToClient(post);
    }

    public async Task<Result<ClientContentPost>> DecideAsync(Guid id, bool approve, string? note, CancellationToken cancellationToken)
    {
        var post = await FindAsync(id, cancellationToken);
        if (post is null)
        {
            return SocialErrors.PostNotFound;
        }

        // Only what is waiting for the client can be answered; an approved post stays approved.
        var now = timeProvider.GetUtcNow();
        if (post.Status != ContentStatus.ClientReview || !post.Decide(approve, visitor.Name, note, now))
        {
            return SocialErrors.WrongState;
        }

        db.Publish(new ContentDecided(
            post.TenantId, now, post.Id.Value, post.Title, post.PartyId, post.PartyName, post.PlannedDate, approve, post.ClientNote,
            visitor.Name, ByClient: true, null, post.OwnerUserId));
        await db.SaveChangesAsync(cancellationToken);
        return ToClient(post);
    }

    internal static IQueryable<ContentPost> Visible(IQueryable<ContentPost> posts) =>
        posts.Where(post => post.Status == ContentStatus.ClientReview || post.Status == ContentStatus.ChangesRequested
            || post.Status == ContentStatus.Approved || post.Status == ContentStatus.Published);

    private static ClientContentPost ToClient(ContentPost post) => new(
        post.Id.Value, post.Title, post.Caption, post.Hashtags, post.Platforms, post.Format, post.PlannedDate, post.PlannedTime,
        SocialApi.Status(post.Status), post.Revision, post.ClientNote, post.DecidedByName, post.DecidedAt, post.PublishedAt, post.PublishedUrl);

    private Task<ContentPost?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var postId = ContentPostId.From(id);
        var partyId = visitor.PartyId;
        return Visible(db.Posts).FirstOrDefaultAsync(post => post.Id == postId && post.PartyId == partyId, cancellationToken);
    }
}

/// <summary>
/// Tells Timeline and Files whose a post is. A post the client has not been sent is not theirs to
/// reach; once it is, all of its media are (they are what is being approved).
/// </summary>
internal sealed class ContentPortalSubject(SocialDbContext db) : IPortalSubject
{
    public string SubjectType => Subjects.ContentPost;

    public async Task<PortalSubjectInfo?> FindAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var id = ContentPostId.From(subjectId);
        var post = await ClientContentHandler.Visible(db.Posts.AsNoTracking()).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return post is null
            ? null
            : new PortalSubjectInfo(post.PartyId, post.Title, post.OwnerUserId is { } owner ? [owner] : [], SharesAllFiles: true);
    }
}

/// <summary>
/// Publishing is done by hand for now, so on the planned day the post's owner is reminded once
/// (moving the date re-arms it). A day the scheduler missed is caught up on the next run.
/// </summary>
internal sealed class ContentReminderJob(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IScheduledJob
{
    public async Task RunAsync(DateOnly today, CancellationToken cancellationToken)
    {
        List<Guid> tenants;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SocialDbContext>();
            tenants = await Due(db.Posts.IgnoreQueryFilters([QueryFilters.Tenant]), today)
                .Select(post => post.TenantId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        foreach (var tenant in tenants)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Bind(TenantId.From(tenant));
            var db = scope.ServiceProvider.GetRequiredService<SocialDbContext>();
            var now = timeProvider.GetUtcNow();
            foreach (var post in await Due(db.Posts, today).ToListAsync(cancellationToken))
            {
                if (post.TakePublishReminderIfDue(today))
                {
                    db.Publish(new ContentPublishDue(post.TenantId, now, post.Id.Value, post.Title, post.PartyName, post.PlannedDate, post.OwnerUserId));
                }
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone changed a post meanwhile; the next run looks again.
            }
        }
    }

    private static IQueryable<ContentPost> Due(IQueryable<ContentPost> posts, DateOnly today) =>
        posts.Where(post => post.Status == ContentStatus.Approved && post.PlannedDate <= today
            && (post.PublishReminderSentFor == null || post.PublishReminderSentFor != post.PlannedDate));
}
