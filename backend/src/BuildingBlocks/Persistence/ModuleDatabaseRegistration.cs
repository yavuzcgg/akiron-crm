using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Akiron.BuildingBlocks.Persistence;

/// <summary>Records which DbContexts the host runs migrations for.</summary>
public sealed record ModuleDatabase(Type ContextType, string Schema);

public static partial class ModuleDatabaseRegistration
{
    public const string ConnectionStringName = "Default";
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>
    /// Registers a module DbContext on the shared database: its own schema and migrations history
    /// table, snake_case names, the tenant/audit interceptor, and a readiness health check.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema)
        where TContext : ModuleDbContext
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is not configured.");

        services.AddDbContext<TContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<TenantAuditInterceptor>()));

        services.AddSingleton(new ModuleDatabase(typeof(TContext), schema));
        services.AddHealthChecks().AddDbContextCheck<TContext>($"db-{schema}", tags: ["ready"]);

        return services;
    }

    /// <summary>Applies pending migrations for every module, in registration order.</summary>
    public static async Task MigrateModuleDatabasesAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ModuleDatabaseRegistration));

        foreach (var database in scope.ServiceProvider.GetServices<ModuleDatabase>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(database.ContextType);
            LogMigrating(logger, database.Schema);
            await context.Database.MigrateAsync();
        }
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Applying migrations for schema {Schema}")]
    private static partial void LogMigrating(ILogger logger, string schema);
}
