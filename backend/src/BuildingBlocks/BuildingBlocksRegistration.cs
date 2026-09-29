using System.Text.Json.Serialization;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.BuildingBlocks;

public static class BuildingBlocksRegistration
{
    /// <summary>Cross-cutting services every module relies on. Modules add their own afterwards.</summary>
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services, IReadOnlyCollection<IModule> modules)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<TenantAuditInterceptor>();
        services.AddSingleton<Email.IEmailSender, Email.LoggingEmailSender>();

        foreach (var module in modules)
        {
            services.AddSingleton(module);
        }

        services.AddSingleton<PermissionCatalog>();

        // Numbers are numbers: the web default also accepts "12" for 12, which makes the OpenAPI
        // document type every integer as "number | string" in the generated client.
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<ApiExceptionHandler>();

        // Outside Development, minimal APIs answer unreadable bodies with a bare 400 of their own.
        // Throwing sends them through ApiExceptionHandler instead, so they carry an error code too.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddOptions<ConstraintErrorMap>();
        services.AddValidatorsFromAssemblyContaining<PageRequestValidator>(includeInternalTypes: true);

        // Validation messages are English, for logs; users see text the client derives from the code
        // (ADR-0006). Without this, the server's OS culture would leak Turkish into the API.
        ValidatorOptions.Global.LanguageManager.Enabled = false;

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();

        // Endpoints must opt out of authentication explicitly (AllowAnonymous), never opt in.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
