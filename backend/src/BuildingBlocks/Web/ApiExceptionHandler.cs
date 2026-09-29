using Akiron.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Akiron.BuildingBlocks.Web;

/// <summary>
/// Last line of defence: turns escaping exceptions into ProblemDetails. Expected failures travel as
/// <see cref="Result{T}"/> and never get here; what does get here is either a constraint the
/// database enforced (409) or a bug (500, with the correlation id and no internal message).
/// </summary>
public sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IOptions<ConstraintErrorMap> constraintErrors,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var error = Describe(exception);
        var status = error is null ? StatusCodes.Status500InternalServerError : ErrorResults.StatusFor(error.Kind);

        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path.Value ?? string.Empty;

        if (error is null)
        {
            LogUnhandled(logger, exception, method, path);
        }
        else
        {
            LogRejected(logger, status, method, path, error.Code);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = error is null ? "An unexpected error occurred" : ErrorResults.TitleFor(error.Kind),
            Detail = error?.Detail ?? "Please retry; if this keeps happening, quote the correlationId when reporting it.",
        };
        problem.Extensions["code"] = error?.Code ?? CommonErrorCodes.Unexpected;

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    private Error? Describe(Exception exception) => exception switch
    {
        // Unreadable JSON, a wrong content type or an oversized body: the client's fault, not ours.
        BadHttpRequestException =>
            Error.Validation(CommonErrorCodes.MalformedRequest, "The request body could not be read; send valid UTF-8 JSON."),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique } =>
            constraintErrors.Value.Find(unique.ConstraintName)
            ?? Error.Conflict(CommonErrorCodes.Conflict, "The request clashes with existing data."),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } foreignKey } =>
            constraintErrors.Value.Find(foreignKey.ConstraintName)
            ?? Error.Conflict(CommonErrorCodes.Conflict, "The request references data that does not exist, or is referenced by data that does."),

        DbUpdateConcurrencyException =>
            Error.Conflict(CommonErrorCodes.Conflict, "The record was changed by someone else; reload and retry."),

        _ => null,
    };

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Request rejected with {StatusCode} on {Method} {Path} as {Code}")]
    private static partial void LogRejected(ILogger logger, int statusCode, string method, string path, string code);
}
