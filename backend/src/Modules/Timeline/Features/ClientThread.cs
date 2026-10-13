using System.Text.Json;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Portal;
using Akiron.Modules.Timeline.Domain;
using Akiron.Modules.Timeline.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Akiron.Modules.Timeline.Features;

internal sealed record ClientNoteCommand(string Text);

internal sealed class ClientNoteValidator : AbstractValidator<ClientNoteCommand>
{
    public ClientNoteValidator() => RuleFor(command => command.Text).NotEmpty().MaximumLength(AddNoteValidator.MaxLength);
}

/// <summary>
/// Finds the record behind a client-visible thread through the module that owns it, so Timeline
/// never guesses whose a work order is.
/// </summary>
internal sealed class PortalSubjects(IEnumerable<IPortalSubject> subjects)
{
    public async Task<PortalSubjectInfo?> FindAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var owner = subjects.FirstOrDefault(candidate => candidate.SubjectType == subjectType);
        return owner is null ? null : await owner.FindAsync(subjectId, cancellationToken);
    }

    public static string Excerpt(string text) => text.Length <= 140 ? text : text[..140] + "…";
}

/// <summary>
/// The client's side of a record's thread: only entries marked visible to the client (ADR-0009), and
/// only on records of the visitor's own party. A client's message is a note like any other, written
/// under their name and linked to the party too.
/// </summary>
internal sealed class ClientThreadHandler(
    TimelineDbContext db,
    TimelineWriter writer,
    PortalSubjects subjects,
    IPortalVisitor visitor,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task<Result<TimelinePageResponse>> ReadAsync(string subjectType, Guid subjectId, string? before, int? limit, CancellationToken cancellationToken)
    {
        if (await OwnAsync(subjectType, subjectId, cancellationToken) is null)
        {
            return TimelineErrors.UnknownSubject;
        }

        if (!TimelineCursor.TryDecode(before, out var cursor))
        {
            return TimelineErrors.InvalidCursor;
        }

        var take = Math.Clamp(limit ?? GetTimelineHandler.MaxLimit, 1, GetTimelineHandler.MaxLimit);
        var links = db.Links.Where(link => link.SubjectType == subjectType && link.SubjectId == subjectId);
        if (cursor is { } position)
        {
            links = links.Where(link => link.OccurredAt < position.OccurredAt
                || (link.OccurredAt == position.OccurredAt && link.Sequence < position.Sequence));
        }

        var rows = await links
            .Join(db.Entries, link => link.EntryId, entry => entry.Id, (link, entry) => new { link.Sequence, entry })
            .Where(row => row.entry.Visibility == TimelineVisibility.Portal)
            .OrderByDescending(row => row.entry.OccurredAt)
            .ThenByDescending(row => row.Sequence)
            .Take(take + 1)
            .ToListAsync(cancellationToken);

        var page = rows.Take(take).ToList();
        var next = rows.Count > take ? new TimelineCursor(page[^1].entry.OccurredAt, page[^1].Sequence).Encode() : null;
        return new TimelinePageResponse(page.Select(row => GetTimelineHandler.ToResponse(row.entry)).ToList(), next);
    }

    public async Task<Result<TimelineItemResponse>> WriteAsync(string subjectType, Guid subjectId, ClientNoteCommand command, CancellationToken cancellationToken)
    {
        var subject = await OwnAsync(subjectType, subjectId, cancellationToken);
        if (subject is null)
        {
            return TimelineErrors.UnknownSubject;
        }

        var text = command.Text.Trim();
        var now = timeProvider.GetUtcNow();
        var entry = TimelineEntry.Create(
            tenantContext.TenantId,
            TimelineEntryTypes.Note,
            now,
            TimelineActor.ClientContact(visitor.Name),
            JsonSerializer.Serialize(new { text, mentions = Array.Empty<object>() }, JsonSerializerOptions.Web),
            Guid.CreateVersion7().ToString(),
            [new TimelineSubject(subjectType, subjectId), new TimelineSubject(TimelineSubjects.Party, subject.PartyId)],
            TimelineVisibility.Portal);

        db.Publish(new ClientMessagePosted(
            tenantContext.TenantId, now, entry.Id.Value, subjectType, subjectId, subject.PartyId, subject.Title,
            PortalSubjects.Excerpt(text), FromClient: true, visitor.Name, null, subject.StaffUserIds));
        await writer.WriteAsync(entry, cancellationToken);
        return GetTimelineHandler.ToResponse(entry);
    }

    /// <summary>The record when it belongs to the visitor's party; anything else does not exist for them.</summary>
    private async Task<PortalSubjectInfo?> OwnAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var subject = await subjects.FindAsync(subjectType, subjectId, cancellationToken);
        return subject is not null && subject.PartyId == visitor.PartyId ? subject : null;
    }
}

internal static class ClientThreadEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var portal = endpoints.MapGroup("/portal");

        portal.MapGet("/{subjectType}/{subjectId:guid}", async (string subjectType, Guid subjectId, string? before, int? limit, ClientThreadHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.ReadAsync(subjectType, subjectId, before, limit, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .RequirePortal()
            .Produces<TimelinePageResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Portal: the messages and events the client may see on one of their records");

        portal.MapPost("/{subjectType}/{subjectId:guid}/notes", async (string subjectType, Guid subjectId, ClientNoteCommand command, ClientThreadHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.WriteAsync(subjectType, subjectId, command, cancellationToken);
                return result.IsSuccess ? Results.Created((string?)null, result.Value) : result.Error.ToProblem();
            })
            .RequirePortal()
            .Validate<ClientNoteCommand>()
            .Produces<TimelineItemResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Portal: write to the team on one of the client's records");
    }
}
