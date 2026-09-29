using Akiron.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Akiron.BuildingBlocks.Web;

public static class ErrorResults
{
    /// <summary>Writes the error as ProblemDetails with its stable <c>code</c> and <c>params</c> (ADR-0006).</summary>
    public static ProblemHttpResult ToProblem(this Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error.Parameters.Count > 0)
        {
            extensions["params"] = error.Parameters;
        }

        return TypedResults.Problem(
            statusCode: StatusFor(error.Kind),
            title: TitleFor(error.Kind),
            detail: error.Detail,
            extensions: extensions);
    }

    public static int StatusFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Rule => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static string TitleFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => "Invalid request",
        ErrorKind.NotFound => "Resource not found",
        ErrorKind.Conflict => "Conflict",
        ErrorKind.Rule => "Request refused by a business rule",
        ErrorKind.Unauthorized => "Unauthenticated",
        ErrorKind.Forbidden => "Forbidden",
        _ => "Error",
    };
}
