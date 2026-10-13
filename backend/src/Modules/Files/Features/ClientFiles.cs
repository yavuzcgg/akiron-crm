using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Portal;
using Akiron.Modules.Files.Domain;
using Akiron.Modules.Files.Persistence;
using Akiron.Modules.Files.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Akiron.Modules.Files.Features;

internal sealed record ShareFileCommand(bool Shared);

/// <summary>A file as the client sees it; who on the team uploaded it is not their business.</summary>
internal sealed record ClientFileResponse(Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt, bool UploadedByClient);

/// <summary>
/// Files across the staff/client line: staff share a file (a deliverable) into the portal, clients
/// see shared files of their own records and send files back (a brief, a payment receipt). Whose a
/// record is comes from the module that owns it (<see cref="IPortalSubject"/>).
/// </summary>
internal sealed class ClientFilesHandler(
    FilesDbContext db,
    IObjectStorage storage,
    IEnumerable<IPortalSubject> subjects,
    IPortalVisitor visitor,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    /// <summary>Staff: show a file to the client, or take it back.</summary>
    public async Task<Result<FileResponse>> ShareAsync(Guid id, bool shared, CancellationToken cancellationToken)
    {
        var fileId = StoredFileId.From(id);
        var file = await db.Files.FirstOrDefaultAsync(candidate => candidate.Id == fileId, cancellationToken);
        if (file is null)
        {
            return FileErrors.NotFound;
        }

        var subject = await FindAsync(file.SubjectType, file.SubjectId, cancellationToken);
        if (subject is null)
        {
            return FileErrors.NotShareable;
        }

        if (file.Share(shared) && shared)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("Sharing needs a signed-in user.");
            db.Publish(new FileSharedWithClient(
                file.TenantId, timeProvider.GetUtcNow(), file.Id.Value, file.FileName, file.SubjectType, file.SubjectId,
                subject.PartyId, subject.Title, userId.Value, currentUser.DisplayName));
        }

        await db.SaveChangesAsync(cancellationToken);
        return UploadFileHandler.ToResponse(file);
    }

    public async Task<Result<IReadOnlyList<ClientFileResponse>>> ListAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        if (await OwnAsync(subjectType, subjectId, cancellationToken) is null)
        {
            return FileErrors.UnknownSubject;
        }

        var files = await db.Files.AsNoTracking()
            .Where(file => file.SubjectType == subjectType && file.SubjectId == subjectId && file.SharedWithClient)
            .OrderByDescending(file => file.CreatedAt)
            .ToListAsync(cancellationToken);
        return files.Select(ToClient).ToList();
    }

    /// <summary>A shared file of one of the visitor's records; anything else does not exist for them.</summary>
    public async Task<StoredFile?> FindSharedAsync(Guid id, CancellationToken cancellationToken)
    {
        var fileId = StoredFileId.From(id);
        var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == fileId && candidate.SharedWithClient, cancellationToken);
        return file is not null && await OwnAsync(file.SubjectType, file.SubjectId, cancellationToken) is not null ? file : null;
    }

    public async Task<Result<ClientFileResponse>> UploadAsync(IFormFile? upload, string? subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var subject = subjectType is null ? null : await OwnAsync(subjectType, subjectId, cancellationToken);
        if (subject is null)
        {
            return FileErrors.UnknownSubject;
        }

        if (UploadFileHandler.Check(upload) is { } refused)
        {
            return refused;
        }

        var now = timeProvider.GetUtcNow();
        var contentType = string.IsNullOrWhiteSpace(upload!.ContentType) ? "application/octet-stream" : upload.ContentType;
        var file = StoredFile.CreateFromClient(tenantContext.TenantId, subjectType!, subjectId, upload.FileName, contentType, upload.Length, now, visitor.Name);
        await using (var stream = upload.OpenReadStream())
        {
            await storage.PutAsync(file.StorageKey, stream, contentType, cancellationToken);
        }

        db.Files.Add(file);
        db.Publish(new ClientFileUploaded(
            tenantContext.TenantId, now, file.Id.Value, file.FileName, file.SizeBytes, subjectType!, subjectId,
            subject.PartyId, subject.Title, visitor.Name, subject.StaffUserIds));
        await db.SaveChangesAsync(cancellationToken);
        return ToClient(file);
    }

    private static ClientFileResponse ToClient(StoredFile file) =>
        new(file.Id.Value, file.FileName, file.ContentType, file.SizeBytes, file.CreatedAt, file.ClientUploaderName is not null);

    private async Task<PortalSubjectInfo?> OwnAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var subject = await FindAsync(subjectType, subjectId, cancellationToken);
        return subject is not null && subject.PartyId == visitor.PartyId ? subject : null;
    }

    private async Task<PortalSubjectInfo?> FindAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var owner = subjects.FirstOrDefault(candidate => candidate.SubjectType == subjectType);
        return owner is null ? null : await owner.FindAsync(subjectId, cancellationToken);
    }
}

internal static class ClientFileEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var portal = endpoints.MapGroup("/portal");

        portal.MapGet("/", async (string subjectType, Guid subjectId, ClientFilesHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.ListAsync(subjectType, subjectId, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .RequirePortal()
            .Produces<IReadOnlyList<ClientFileResponse>>()
            .WithSummary("Portal: files shared with the client on one of their records");

        portal.MapGet("/{id:guid}/content", async (Guid id, ClientFilesHandler handler, IObjectStorage storage, HttpContext httpContext) =>
            {
                var file = await handler.FindSharedAsync(id, httpContext.RequestAborted);
                if (file is null)
                {
                    return FileErrors.NotFound.ToProblem();
                }

                httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                var stream = await storage.OpenReadAsync(file.StorageKey, httpContext.RequestAborted);
                return Results.File(stream, file.ContentType, file.FileName);
            })
            .RequirePortal()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Portal: download a shared file");

        portal.MapPost("/", async (IFormFile? file, [Microsoft.AspNetCore.Mvc.FromForm] string? subjectType, [Microsoft.AspNetCore.Mvc.FromForm] Guid subjectId, ClientFilesHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.UploadAsync(file, subjectType, subjectId, cancellationToken);
                return result.IsSuccess ? Results.Created((string?)null, result.Value) : result.Error.ToProblem();
            })
            .RequirePortal()
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(UploadFileHandler.MaxBytes + (1024 * 1024)))
            .Produces<ClientFileResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Portal: send a file to the team (a brief, a logo, a payment receipt)");
    }
}
