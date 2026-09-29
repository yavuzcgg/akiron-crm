using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Akiron.BuildingBlocks.Web;

/// <summary>
/// Gives every request one id that appears in the response header, in every log line and in every
/// ProblemDetails body, so a user's bug report can be matched to the server logs.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();

        // An upstream proxy may already have assigned one; accept it only if it looks harmless,
        // since it ends up in logs.
        var correlationId = IsAcceptable(incoming) ? incoming : Guid.CreateVersion7().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static bool IsAcceptable(string value) =>
        value.Length is > 0 and <= MaxLength && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
