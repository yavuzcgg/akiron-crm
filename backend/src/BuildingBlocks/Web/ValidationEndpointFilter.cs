using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.BuildingBlocks.Web;

/// <summary>Runs the FluentValidation validator for a request argument before the handler sees it.</summary>
public static class ValidationEndpointFilter
{
    /// <summary>
    /// FluentValidation's built-in codes are class names ("NotEmptyValidator"). The client
    /// dictionary keys on short, stable codes instead; custom rules pass their own dotted code
    /// through <c>WithErrorCode</c> and are left untouched.
    /// </summary>
    private static readonly Dictionary<string, string> BuiltInCodes = new(StringComparer.Ordinal)
    {
        ["NotEmptyValidator"] = "validation.required",
        ["NotNullValidator"] = "validation.required",
        ["EmailValidator"] = "validation.email",
        ["MaximumLengthValidator"] = "validation.max_length",
        ["MinimumLengthValidator"] = "validation.min_length",
        ["LengthValidator"] = "validation.length",
        ["ExactLengthValidator"] = "validation.length",
        ["InclusiveBetweenValidator"] = "validation.between",
        ["GreaterThanValidator"] = "validation.greater_than",
        ["GreaterThanOrEqualValidator"] = "validation.greater_than_or_equal",
        ["LessThanValidator"] = "validation.less_than",
        ["LessThanOrEqualValidator"] = "validation.less_than_or_equal",
        ["RegularExpressionValidator"] = "validation.format",
        ["EnumValidator"] = "validation.invalid_value",
    };

    /// <summary>Placeholders that describe the rule, as opposed to echoing the submitted value back.</summary>
    private static readonly HashSet<string> RuleParameters = new(StringComparer.Ordinal)
    {
        "MinLength", "MaxLength", "From", "To", "ComparisonValue",
    };

    /// <summary>Usage: <c>group.MapPost("/register", Handle).Validate&lt;RegisterCommand&gt;();</c></summary>
    public static RouteHandlerBuilder Validate<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class
    {
        return builder
            .AddEndpointFilter(async (invocationContext, next) =>
            {
                var request = invocationContext.Arguments.OfType<TRequest>().FirstOrDefault();

                if (request is null)
                {
                    return await next(invocationContext);
                }

                var httpContext = invocationContext.HttpContext;
                var validator = httpContext.RequestServices.GetRequiredService<IValidator<TRequest>>();
                var result = await validator.ValidateAsync(request, httpContext.RequestAborted);

                if (result.IsValid)
                {
                    return await next(invocationContext);
                }

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                };
                problem.Extensions["code"] = CommonErrorCodes.ValidationFailed;
                problem.Extensions["errors"] = result.Errors
                    .GroupBy(failure => ToCamelCase(failure.PropertyName))
                    .ToDictionary(group => group.Key, group => group.Select(ToFieldError).Distinct().ToArray());

                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

                await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });

                return Results.Empty;
            })
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static FieldError ToFieldError(ValidationFailure failure)
    {
        var code = BuiltInCodes.TryGetValue(failure.ErrorCode ?? string.Empty, out var mapped)
            ? mapped
            : string.IsNullOrEmpty(failure.ErrorCode) ? "validation.invalid_value" : failure.ErrorCode;

        // FluentValidation reports an unbounded side of a length rule as -1; that is noise to a client.
        var parameters = failure.FormattedMessagePlaceholderValues?
            .Where(pair => RuleParameters.Contains(pair.Key) && pair.Value is not -1)
            .ToDictionary(pair => ToCamelCase(pair.Key), pair => pair.Value);

        return new FieldError(code, failure.ErrorMessage, parameters is { Count: > 0 } ? parameters : null);
    }

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    /// <summary>One rejected rule: the code the client translates, its parameters, and English for logs.</summary>
    private sealed record FieldError(
        string Code,
        string Message,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, object>? Params);
}
