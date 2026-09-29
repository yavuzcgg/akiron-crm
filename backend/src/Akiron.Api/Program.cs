using System.Threading.RateLimiting;
using Akiron.Api;
using Akiron.BuildingBlocks;
using Akiron.BuildingBlocks.Events;
using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Tenancy;
using Akiron.BuildingBlocks.Web;
using Akiron.Contracts.Identity;
using Akiron.Modules.Files;
using Akiron.Modules.Identity;
using Akiron.Modules.Notifications;
using Akiron.Modules.Reference;
using Akiron.Modules.Timeline;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.ValidateSecrets(builder.Environment);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}",
        formatProvider: System.Globalization.CultureInfo.InvariantCulture));

// The explicit module list is the product's composition: adding a module is one line here (ADR-0001).
IModule[] modules = [new IdentityModule(), new TimelineModule(), new ReferenceModule(), new FilesModule(), new NotificationsModule()];

builder.Services.AddBuildingBlocks(modules, builder.Configuration);
builder.Services.AddOutbox(builder.Configuration, typeof(WorkspaceCreated).Assembly);
foreach (var module in modules)
{
    module.AddServices(builder.Services, builder.Configuration);
}

builder.Services.AddRateLimiter(options =>
{
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests" };
        problem.Extensions["code"] = CommonErrorCodes.RateLimited;

        await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = problem });
    };
});

builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Akiron CRM API";
    document.Info.Version = "v1";
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.MigrateModuleDatabasesAsync();
}

app.MapOpenApi().AllowAnonymous();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

var api = app.MapGroup("/api/v1");
foreach (var module in modules)
{
    module.MapEndpoints(api.MapGroup($"/{module.Name}").WithTags(module.Name));
}

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host it with WebApplicationFactory.</summary>
#pragma warning disable CA1515 // WebApplicationFactory<Program> needs an accessible type.
public partial class Program;
#pragma warning restore CA1515
