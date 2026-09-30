using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Modules.Files.Features;
using Akiron.Modules.Files.Persistence;
using Akiron.Modules.Files.Storage;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Files;

public static class FilesPermissions
{
    public const string Read = "files.read";
    public const string Write = "files.write";

    public static IReadOnlyCollection<string> All { get; } = [Read, Write];
}

/// <summary>Files attached to any record, stored in S3-compatible object storage (ADR-0005).</summary>
public sealed class FilesModule : IModule
{
    public string Name => FilesDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => FilesPermissions.All;

    public IReadOnlyCollection<string> MemberPermissions => FilesPermissions.All;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<FilesDbContext>(configuration, FilesDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(FilesModule).Assembly);
        services.Configure<S3StorageOptions>(configuration.GetSection(S3StorageOptions.SectionName));
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => FileEndpoints.Map(endpoints);
}
