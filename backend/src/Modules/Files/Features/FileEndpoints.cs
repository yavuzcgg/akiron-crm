using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts;
using Akiron.Contracts.Files;
using Akiron.Modules.Files.Domain;
using Akiron.Modules.Files.Persistence;
using Akiron.Modules.Files.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Akiron.Modules.Files.Features;

internal sealed record FileResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string SubjectType,
    Guid SubjectId,
    DateTimeOffset UploadedAt,
    Guid? UploadedBy);

internal static class FileErrors
{
    public static readonly Error UnknownSubject =
        Error.Validation("files.subject.unknown", "Files cannot be attached to this kind of record.");

    public static readonly Error Missing =
        Error.Validation("files.file.missing", "Send the file as multipart/form-data in a field named 'file'.");

    public static readonly Error Empty = Error.Validation("files.file.empty", "The file is empty.");

    public static Error TooLarge(long maxBytes) =>
        new("files.file.too_large", "The file is larger than allowed.", ErrorKind.Validation)
        {
            Parameters = new Dictionary<string, object?> { ["maxBytes"] = maxBytes },
        };

    public static readonly Error ExecutableRefused =
        Error.Validation("files.file.type_refused", "Executable and script files are not accepted.");

    public static readonly Error NotFound = Error.NotFound("files.file.not_found", "No such file in this organisation.");
}

internal sealed class UploadFileHandler(
    FilesDbContext db,
    IObjectStorage storage,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public const long MaxBytes = 25 * 1024 * 1024;

    /// <summary>Refused regardless of the declared content type: they could run if someone opens them.</summary>
    private static readonly HashSet<string> RefusedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".com", ".scr", ".ps1", ".vbs", ".js", ".jar", ".sh", ".dll", ".apk",
    };

    public async Task<Result<FileResponse>> HandleAsync(IFormFile? upload, string? subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        if (subjectType is null || !Subjects.All.Contains(subjectType)
            || (subjectType == Subjects.Workspace && subjectId != tenantContext.TenantId.Value))
        {
            return FileErrors.UnknownSubject;
        }

        if (upload is null)
        {
            return FileErrors.Missing;
        }

        if (upload.Length == 0)
        {
            return FileErrors.Empty;
        }

        if (upload.Length > MaxBytes)
        {
            return FileErrors.TooLarge(MaxBytes);
        }

        if (RefusedExtensions.Contains(Path.GetExtension(upload.FileName)))
        {
            return FileErrors.ExecutableRefused;
        }

        var now = timeProvider.GetUtcNow();
        var contentType = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType;
        var file = StoredFile.Create(tenantContext.TenantId, subjectType, subjectId, upload.FileName, contentType, upload.Length, now);

        // Bytes first, row second: a failed upload leaves no row pointing at nothing. An orphaned
        // object (row insert failed after upload) is harmless and swept by the purge job.
        await using (var stream = upload.OpenReadStream())
        {
            await storage.PutAsync(file.StorageKey, stream, contentType, cancellationToken);
        }

        var userId = currentUser.UserId ?? throw new InvalidOperationException("Uploads need a signed-in user.");
        db.Files.Add(file);
        db.Publish(new FileUploaded(file.TenantId, now, file.Id.Value, file.FileName, file.SizeBytes, subjectType, subjectId, userId.Value, currentUser.DisplayName));
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(file);
    }

    internal static FileResponse ToResponse(StoredFile file) => new(
        file.Id.Value, file.FileName, file.ContentType, file.SizeBytes, file.SubjectType, file.SubjectId, file.CreatedAt, file.CreatedBy?.Value);
}

internal static class FileEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/", async (IFormFile? file, [Microsoft.AspNetCore.Mvc.FromForm] string? subjectType, [Microsoft.AspNetCore.Mvc.FromForm] Guid subjectId, UploadFileHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(file, subjectType, subjectId, cancellationToken);
                return result.IsSuccess ? Results.Created($"/api/v1/files/{result.Value.Id}", result.Value) : result.Error.ToProblem();
            })
            .RequirePermission(FilesPermissions.Write)
            // Multipart uploads from the web app carry the session cookie, not an antiforgery token;
            // SameSite=Lax cookies already keep cross-site form posts out.
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(UploadFileHandler.MaxBytes + (1024 * 1024)))
            .Produces<FileResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Attach a file to a record");

        endpoints.MapGet("/", async (string subjectType, Guid subjectId, FilesDbContext db, CancellationToken cancellationToken) =>
            {
                var files = await db.Files
                    .Where(file => file.SubjectType == subjectType && file.SubjectId == subjectId)
                    .OrderByDescending(file => file.CreatedAt)
                    .ToListAsync(cancellationToken);
                return Results.Ok(files.Select(UploadFileHandler.ToResponse).ToList());
            })
            .RequirePermission(FilesPermissions.Read)
            .Produces<List<FileResponse>>()
            .WithSummary("Files attached to a record");

        endpoints.MapGet("/{id:guid}/content", async (Guid id, FilesDbContext db, IObjectStorage storage, HttpContext httpContext) =>
            {
                var fileId = StoredFileId.From(id);
                var file = await db.Files.FirstOrDefaultAsync(candidate => candidate.Id == fileId, httpContext.RequestAborted);
                if (file is null)
                {
                    return FileErrors.NotFound.ToProblem();
                }

                // Always a download, never rendered: an uploaded HTML or SVG must not run in our origin.
                httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                var stream = await storage.OpenReadAsync(file.StorageKey, httpContext.RequestAborted);
                return Results.File(stream, file.ContentType, file.FileName);
            })
            .RequirePermission(FilesPermissions.Read)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Download a file");

        endpoints.MapDelete("/{id:guid}", async (Guid id, FilesDbContext db, CancellationToken cancellationToken) =>
            {
                var fileId = StoredFileId.From(id);
                var file = await db.Files.FirstOrDefaultAsync(candidate => candidate.Id == fileId, cancellationToken);
                if (file is null)
                {
                    return FileErrors.NotFound.ToProblem();
                }

                db.Files.Remove(file);
                await db.SaveChangesAsync(cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(FilesPermissions.Write)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a file from its record");
    }
}
