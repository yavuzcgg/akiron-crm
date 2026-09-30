using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Contracts.Timeline;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Timeline.Features;

internal sealed record TimelineActorResponse(string Kind, Guid? Id, string? Name);

/// <param name="Payload">Type-specific values the client fills into the entry's template.</param>
internal sealed record TimelineItemResponse(
    Guid Id,
    string Type,
    DateTimeOffset OccurredAt,
    TimelineActorResponse Actor,
    JsonElement Payload,
    string Visibility);

/// <param name="NextCursor">Pass as <c>before</c> for the next (older) page; null on the last page.</param>
internal sealed record TimelinePageResponse(IReadOnlyList<TimelineItemResponse> Items, string? NextCursor);

internal static class TimelineErrors
{
    public static readonly Error UnknownSubject =
        Error.NotFound("timeline.subject.unknown", "No timeline exists for this kind of record.");

    public static readonly Error InvalidCursor =
        Error.Validation("timeline.cursor.invalid", "The paging cursor is not valid; start from the first page.");
}

/// <summary>Keyset position: the last item's timestamp and link sequence, opaque to clients.</summary>
internal readonly record struct TimelineCursor(DateTimeOffset OccurredAt, long Sequence)
{
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        string.Create(CultureInfo.InvariantCulture, $"{OccurredAt.UtcTicks}:{Sequence}")));

    public static bool TryDecode(string? value, out TimelineCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split(':');
            if (parts.Length == 2
                && long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks)
                && long.TryParse(parts[1], CultureInfo.InvariantCulture, out var sequence))
            {
                cursor = new TimelineCursor(new DateTimeOffset(ticks, TimeSpan.Zero), sequence);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }
}

internal sealed class GetTimelineHandler(TimelineDbContext db)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public async Task<Result<TimelinePageResponse>> HandleAsync(
        string subjectType,
        Guid subjectId,
        string? before,
        int? limit,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!TimelineSubjects.All.Contains(subjectType))
        {
            return TimelineErrors.UnknownSubject;
        }

        if (!TimelineCursor.TryDecode(before, out var cursor))
        {
            return TimelineErrors.InvalidCursor;
        }

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var hidden = HiddenTypes(user);

        var links = db.Links.Where(link => link.SubjectType == subjectType && link.SubjectId == subjectId);
        if (cursor is { } position)
        {
            links = links.Where(link => link.OccurredAt < position.OccurredAt
                || (link.OccurredAt == position.OccurredAt && link.Sequence < position.Sequence));
        }

        var rows = await links
            .Join(db.Entries, link => link.EntryId, entry => entry.Id, (link, entry) => new { link.Sequence, entry })
            .Where(row => !hidden.Contains(row.entry.Type))
            .OrderByDescending(row => row.entry.OccurredAt)
            .ThenByDescending(row => row.Sequence)
            .Take(take + 1)
            .ToListAsync(cancellationToken);

        var page = rows.Take(take).ToList();
        var next = rows.Count > take ? new TimelineCursor(page[^1].entry.OccurredAt, page[^1].Sequence).Encode() : null;

        return new TimelinePageResponse(page.Select(row => ToResponse(row.entry)).ToList(), next);
    }

    /// <summary>Entry types whose extra permission the caller lacks (ADR-0009, permission filtering).</summary>
    private static string[] HiddenTypes(ClaimsPrincipal user)
    {
        var granted = user.FindAll(AkironClaimTypes.Permission).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
        if (granted.Contains(PermissionNames.All))
        {
            return [];
        }

        return TimelineEntryTypes.RequiredPermission
            .Where(pair => !granted.Contains(pair.Value))
            .Select(pair => pair.Key)
            .ToArray();
    }

    internal static TimelineItemResponse ToResponse(TimelineEntry entry)
    {
        using var payload = JsonDocument.Parse(entry.Payload);
        return new TimelineItemResponse(
            entry.Id.Value,
            entry.Type,
            entry.OccurredAt,
            new TimelineActorResponse(ToApi(entry.ActorKind), entry.ActorId, entry.ActorName),
            payload.RootElement.Clone(),
            entry.Visibility == TimelineVisibility.Portal ? "portal" : "internal");
    }

    private static string ToApi(ActorKind kind) => kind switch
    {
        ActorKind.User => "user",
        ActorKind.PortalContact => "portal_contact",
        ActorKind.Integration => "integration",
        _ => "system",
    };
}

/// <param name="MentionedUserIds">People @mentioned in the text; each is notified.</param>
internal sealed record AddNoteCommand(string Text, IReadOnlyList<Guid>? MentionedUserIds = null);

internal sealed class AddNoteValidator : AbstractValidator<AddNoteCommand>
{
    public const int MaxLength = 4000;

    public AddNoteValidator()
    {
        RuleFor(command => command.Text).NotEmpty().MaximumLength(MaxLength);
        RuleFor(command => command.MentionedUserIds!.Count).LessThanOrEqualTo(20).When(command => command.MentionedUserIds is not null)
            .OverridePropertyName(nameof(AddNoteCommand.MentionedUserIds));
    }
}

/// <summary>A note a person writes on a record's timeline; the one entry type written directly, not projected.</summary>
internal sealed class AddNoteHandler(
    TimelineWriter writer,
    TimelineDbContext db,
    IMemberDirectory members,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task<Result<TimelineItemResponse>> HandleAsync(string subjectType, Guid subjectId, AddNoteCommand command, CancellationToken cancellationToken)
    {
        if (!TimelineSubjects.All.Contains(subjectType)
            || (subjectType == TimelineSubjects.Workspace && subjectId != tenantContext.TenantId.Value))
        {
            return TimelineErrors.UnknownSubject;
        }

        // Only this organisation's people can be mentioned; unknown ids are dropped, not an error,
        // since someone may have left between typing and sending.
        var mentioned = await members.FindAsync(command.MentionedUserIds ?? [], cancellationToken);
        var text = command.Text.Trim();

        var userId = currentUser.UserId ?? throw new InvalidOperationException("Notes need a signed-in user.");
        var now = timeProvider.GetUtcNow();
        var entry = TimelineEntry.Create(
            tenantContext.TenantId,
            TimelineEntryTypes.Note,
            now,
            TimelineActor.User(userId.Value, currentUser.DisplayName),
            JsonSerializer.Serialize(
                new { text, mentions = mentioned.Values.Select(member => new { userId = member.UserId, name = member.FullName }) },
                JsonSerializerOptions.Web),
            Guid.CreateVersion7().ToString(),
            [new TimelineSubject(subjectType, subjectId)]);

        if (mentioned.Count > 0)
        {
            // Saved by the writer below, in the same transaction as the note.
            db.Publish(new NoteMentioned(
                tenantContext.TenantId, now, entry.Id.Value, subjectType, subjectId, [.. mentioned.Keys],
                text.Length <= 140 ? text : text[..140] + "…", userId.Value, currentUser.DisplayName));
        }

        await writer.WriteAsync(entry, cancellationToken);
        return GetTimelineHandler.ToResponse(entry);
    }
}

internal static class TimelineEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{subjectType}/{subjectId:guid}", async (
                string subjectType,
                Guid subjectId,
                string? before,
                int? limit,
                GetTimelineHandler handler,
                HttpContext httpContext) =>
            {
                var result = await handler.HandleAsync(subjectType, subjectId, before, limit, httpContext.User, httpContext.RequestAborted);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(TimelinePermissions.Read)
            .Produces<TimelinePageResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("A record's history, newest first");

        endpoints.MapPost("/{subjectType}/{subjectId:guid}/notes", async (
                string subjectType,
                Guid subjectId,
                AddNoteCommand command,
                AddNoteHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(subjectType, subjectId, command, cancellationToken);
                return result.IsSuccess ? Results.Created((string?)null, result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(TimelinePermissions.NotesWrite)
            .Validate<AddNoteCommand>()
            .Produces<TimelineItemResponse>(StatusCodes.Status201Created)
            .WithSummary("Add a note to a record's history");

        // For the @mention picker: whoever can write notes can name any teammate.
        endpoints.MapGet("/mentionable", async (IMemberDirectory members, CancellationToken cancellationToken) =>
                TypedResults.Ok(await members.ListAsync(cancellationToken)))
            .RequirePermission(TimelinePermissions.NotesWrite)
            .Produces<IReadOnlyList<MemberSummary>>()
            .WithSummary("People who can be @mentioned in notes");
    }
}
