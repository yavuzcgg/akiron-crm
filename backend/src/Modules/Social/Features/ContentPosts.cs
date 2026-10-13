using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.Contracts.Crm;
using Akiron.Contracts.Social;
using Akiron.Modules.Social.Domain;
using Akiron.Modules.Social.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Social.Features;

internal sealed record ContentPostCommand(
    Guid PartyId,
    string Title,
    DateOnly PlannedDate,
    IReadOnlyList<string>? Platforms = null,
    string Format = "post",
    TimeOnly? PlannedTime = null,
    string? Caption = null,
    string? Hashtags = null);

internal sealed class ContentPostValidator : AbstractValidator<ContentPostCommand>
{
    public ContentPostValidator()
    {
        RuleFor(command => command.PartyId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(ContentPost.TitleMaxLength);
        RuleFor(command => command.Caption).MaximumLength(ContentPost.CaptionMaxLength);
        RuleFor(command => command.Hashtags).MaximumLength(ContentPost.HashtagsMaxLength);
        RuleFor(command => command.Format).Must(format => ContentPost.KnownFormats.Contains(format)).WithErrorCode("validation.invalid_value");
        RuleForEach(command => command.Platforms).Must(platform => ContentPost.KnownPlatforms.Contains(platform)).WithErrorCode("validation.invalid_value");
    }
}

internal sealed record RescheduleCommand(DateOnly PlannedDate, TimeOnly? PlannedTime = null);

internal sealed record ContentNoteCommand(string Note);

internal sealed class ContentNoteValidator : AbstractValidator<ContentNoteCommand>
{
    public ContentNoteValidator() => RuleFor(command => command.Note).NotEmpty().MaximumLength(ContentPost.NoteMaxLength);
}

internal sealed record PublishCommand(string? Url = null);

internal sealed class PublishValidator : AbstractValidator<PublishCommand>
{
    public PublishValidator() =>
        RuleFor(command => command.Url!)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .WithErrorCode("validation.invalid_value")
            .When(command => !string.IsNullOrWhiteSpace(command.Url));
}

/// <summary>Every post of one client in a date range that is ready to go: sent to the client in one e-mail.</summary>
internal sealed record SendBatchCommand(Guid PartyId, DateOnly From, DateOnly To);

internal sealed class SendBatchValidator : AbstractValidator<SendBatchCommand>
{
    public SendBatchValidator()
    {
        RuleFor(command => command.PartyId).NotEmpty();
        RuleFor(command => command.To).GreaterThanOrEqualTo(command => command.From);
    }
}

internal sealed record SendBatchResponse(int Sent);

internal sealed record ContentFilter(
    [FromQuery(Name = "from")] DateOnly? From = null,
    [FromQuery(Name = "to")] DateOnly? To = null,
    [FromQuery(Name = "partyId")] Guid? PartyId = null,
    [FromQuery(Name = "status")] string? Status = null);

internal sealed class ContentFilterValidator : AbstractValidator<ContentFilter>
{
    public ContentFilterValidator() =>
        RuleFor(filter => filter.Status).Must(status => status is null || SocialApi.Statuses.Contains(status)).WithErrorCode("validation.invalid_value");
}

internal sealed record ContentPostResponse(
    Guid Id,
    Guid PartyId,
    string PartyName,
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
    string? ReviewNote,
    string? DecidedByName,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? SentToClientAt,
    DateTimeOffset? PublishedAt,
    string? PublishedUrl,
    Guid? OwnerUserId,
    string? OwnerName,
    DateTimeOffset CreatedAt);

internal static class SocialApi
{
    public static readonly string[] Statuses = ["draft", "internal_review", "client_review", "changes_requested", "approved", "published"];

    public static string Status(ContentStatus status) => Statuses[(int)status];

    public static ContentStatus ParseStatus(string value) => (ContentStatus)Array.IndexOf(Statuses, value);

    public static ContentPostResponse ToResponse(ContentPost post) => new(
        post.Id.Value,
        post.PartyId,
        post.PartyName,
        post.Title,
        post.Caption,
        post.Hashtags,
        post.Platforms,
        post.Format,
        post.PlannedDate,
        post.PlannedTime,
        Status(post.Status),
        post.Revision,
        post.ClientNote,
        post.ReviewNote,
        post.DecidedByName,
        post.DecidedAt,
        post.SentToClientAt,
        post.PublishedAt,
        post.PublishedUrl,
        post.OwnerUserId,
        post.OwnerName,
        post.CreatedAt);

    public static ContentRef ToRef(ContentPost post) => new(post.Id.Value, post.Title, post.PlannedDate);
}

internal static class SocialErrors
{
    public static readonly Error PostNotFound = Error.NotFound("social.content.not_found", "No such post in this organisation.");

    public static readonly Error PartyNotFound = Error.Validation("social.content.party_not_found", "The client does not exist in this organisation.");

    public static readonly Error NotEditable = Error.Rule("social.content.not_editable", "The post is with the client or approved; revise it first.");

    public static readonly Error WrongState = Error.Rule("social.content.wrong_state", "The post is not in a state that allows this.");

    public static readonly Error NothingToSend = Error.Rule("social.content.nothing_to_send", "No draft posts for this client in that period.");
}

/// <summary>
/// The content calendar's staff side: plan posts, move them through internal review, send them to
/// the client (one by one or a month at a time), record approval and publishing.
/// </summary>
internal sealed class ContentPostsHandler(
    SocialDbContext db,
    IPartyDirectory parties,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const int MaxPosts = 500;

    private Guid UserId => (currentUser.UserId ?? throw new InvalidOperationException("A signed-in user is required.")).Value;

    public async Task<IReadOnlyList<ContentPostResponse>> ListAsync(ContentFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Posts.AsNoTracking();
        if (filter.From is { } from)
        {
            query = query.Where(post => post.PlannedDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(post => post.PlannedDate <= to);
        }

        if (filter.PartyId is { } partyId)
        {
            query = query.Where(post => post.PartyId == partyId);
        }

        if (filter.Status is { } status)
        {
            var wanted = SocialApi.ParseStatus(status);
            query = query.Where(post => post.Status == wanted);
        }

        var posts = await query.OrderBy(post => post.PlannedDate).ThenBy(post => post.PlannedTime).ThenBy(post => post.CreatedAt).Take(MaxPosts).ToListAsync(cancellationToken);
        return posts.Select(SocialApi.ToResponse).ToList();
    }

    public async Task<Result<ContentPostResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var post = await FindAsync(id, cancellationToken);
        return post is null ? SocialErrors.PostNotFound : SocialApi.ToResponse(post);
    }

    public async Task<Result<ContentPostResponse>> CreateAsync(ContentPostCommand command, CancellationToken cancellationToken)
    {
        var party = await parties.FindAsync(command.PartyId, cancellationToken);
        if (party is null)
        {
            return SocialErrors.PartyNotFound;
        }

        var post = ContentPost.Create(party.PartyId, party.Name, ToDetails(command), UserId, currentUser.DisplayName);
        db.Posts.Add(post);
        await db.SaveChangesAsync(cancellationToken);
        return SocialApi.ToResponse(post);
    }

    /// <summary>Changes the content. The client cannot be changed; a post with the client or approved must be revised first.</summary>
    public async Task<Result<ContentPostResponse>> UpdateAsync(Guid id, ContentPostCommand command, CancellationToken cancellationToken)
    {
        var post = await FindAsync(id, cancellationToken);
        if (post is null)
        {
            return SocialErrors.PostNotFound;
        }

        if (!post.Update(ToDetails(command)))
        {
            return SocialErrors.NotEditable;
        }

        await db.SaveChangesAsync(cancellationToken);
        return SocialApi.ToResponse(post);
    }

    /// <summary>A drag on the calendar: only the day changes, whatever the approval state (short of published).</summary>
    public Task<Result<ContentPostResponse>> RescheduleAsync(Guid id, RescheduleCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(id, post => post.Reschedule(command.PlannedDate, command.PlannedTime), null, cancellationToken);

    public Task<Result<ContentPostResponse>> SubmitAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(
            id,
            post => post.SubmitForReview(),
            (post, now) => new ContentReviewRequested(post.TenantId, now, post.Id.Value, post.Title, post.PartyName, post.PlannedDate, UserId, currentUser.DisplayName),
            cancellationToken);

    public Task<Result<ContentPostResponse>> ReturnAsync(Guid id, ContentNoteCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(
            id,
            post => post.ReturnToAuthor(command.Note),
            (post, now) => new ContentReturned(post.TenantId, now, post.Id.Value, post.Title, post.ReviewNote!, post.OwnerUserId, UserId, currentUser.DisplayName),
            cancellationToken);

    public Task<Result<ContentPostResponse>> SendAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return ChangeAsync(
            id,
            post => post.SendToClient(now),
            (post, _) => new ContentSentToClient(post.TenantId, now, post.PartyId, post.PartyName, [SocialApi.ToRef(post)], UserId, currentUser.DisplayName),
            cancellationToken);
    }

    public async Task<Result<SendBatchResponse>> SendBatchAsync(SendBatchCommand command, CancellationToken cancellationToken)
    {
        var posts = await db.Posts
            .Where(post => post.PartyId == command.PartyId && post.PlannedDate >= command.From && post.PlannedDate <= command.To
                && (post.Status == ContentStatus.Draft || post.Status == ContentStatus.InternalReview))
            .OrderBy(post => post.PlannedDate)
            .Take(MaxPosts)
            .ToListAsync(cancellationToken);
        if (posts.Count == 0)
        {
            return SocialErrors.NothingToSend;
        }

        var now = timeProvider.GetUtcNow();
        posts.ForEach(post => post.SendToClient(now));
        db.Publish(new ContentSentToClient(
            tenantContext.TenantId, now, command.PartyId, posts[0].PartyName, posts.Select(SocialApi.ToRef).ToList(), UserId, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        return new SendBatchResponse(posts.Count);
    }

    /// <summary>Staff record the client's approval (given by phone, in a meeting) or approve where no client is asked.</summary>
    public Task<Result<ContentPostResponse>> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var name = currentUser.DisplayName ?? string.Empty;
        return ChangeAsync(
            id,
            post => post.Decide(approve: true, name, null, now),
            (post, _) => new ContentDecided(
                post.TenantId, now, post.Id.Value, post.Title, post.PartyId, post.PartyName, post.PlannedDate, true, null, name, ByClient: false, UserId, post.OwnerUserId),
            cancellationToken);
    }

    public Task<Result<ContentPostResponse>> ReviseAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, post => post.Revise(), null, cancellationToken);

    public Task<Result<ContentPostResponse>> PublishAsync(Guid id, PublishCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return ChangeAsync(
            id,
            post => post.MarkPublished(command.Url, now),
            (post, _) => new ContentPublished(post.TenantId, now, post.Id.Value, post.Title, post.PartyId, post.PublishedUrl, UserId, currentUser.DisplayName),
            cancellationToken);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var post = await FindAsync(id, cancellationToken);
        if (post is null)
        {
            return SocialErrors.PostNotFound;
        }

        // What went live is part of the record.
        if (post.Status == ContentStatus.Published)
        {
            return SocialErrors.WrongState;
        }

        db.Posts.Remove(post);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ContentDetails ToDetails(ContentPostCommand command) =>
        new(command.Title, command.Caption, command.Hashtags, command.Platforms ?? [], command.Format, command.PlannedDate, command.PlannedTime);

    private async Task<Result<ContentPostResponse>> ChangeAsync(
        Guid id,
        Func<ContentPost, bool> change,
        Func<ContentPost, DateTimeOffset, Akiron.BuildingBlocks.Events.IIntegrationEvent>? announce,
        CancellationToken cancellationToken)
    {
        var post = await FindAsync(id, cancellationToken);
        if (post is null)
        {
            return SocialErrors.PostNotFound;
        }

        if (!change(post))
        {
            return SocialErrors.WrongState;
        }

        if (announce is not null)
        {
            db.Publish(announce(post, timeProvider.GetUtcNow()));
        }

        await db.SaveChangesAsync(cancellationToken);
        return SocialApi.ToResponse(post);
    }

    private Task<ContentPost?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var postId = ContentPostId.From(id);
        return db.Posts.FirstOrDefaultAsync(post => post.Id == postId, cancellationToken);
    }
}
