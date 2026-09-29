using Akiron.BuildingBlocks.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Akiron.BuildingBlocks.Security;

/// <summary>
/// Answers 401 and 403 with the same ProblemDetails shape as every other error, instead of the
/// empty bodies the authentication handlers send by default.
/// </summary>
public sealed class ProblemAuthorizationResultHandler(IProblemDetailsService problemDetailsService)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var (status, title, code) = authorizeResult.Forbidden
            ? (StatusCodes.Status403Forbidden, "Forbidden", CommonErrorCodes.Forbidden)
            : (StatusCodes.Status401Unauthorized, "Unauthenticated", CommonErrorCodes.Unauthenticated);

        context.Response.StatusCode = status;

        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });
    }
}
