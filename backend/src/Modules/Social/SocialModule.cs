using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Scheduling;
using Akiron.Contracts.Portal;
using Akiron.Modules.Social.Features;
using Akiron.Modules.Social.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Modules.Social;

public static class SocialPermissions
{
    /// <summary>Seeing the content calendar.</summary>
    public const string ContentRead = "social.content.read";

    /// <summary>Planning and editing posts, asking for review, marking them published.</summary>
    public const string ContentWrite = "social.content.write";

    /// <summary>The internal check: sending posts to the client, sending them back, recording approval.</summary>
    public const string ContentApprove = "social.content.approve";

    public static IReadOnlyCollection<string> All { get; } = [ContentRead, ContentWrite, ContentApprove];

    /// <summary>Content is everyday work; what reaches the client goes through someone who may approve.</summary>
    public static IReadOnlyCollection<string> Member { get; } = [ContentRead, ContentWrite];
}

/// <summary>
/// Social media management (Phase 4): the content calendar and client approval. Publishing through
/// the networks' APIs and reporting arrive in Phase 6; until then a post is marked published by hand.
/// </summary>
public sealed class SocialModule : IModule
{
    public string Name => SocialDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => SocialPermissions.All;

    public IReadOnlyCollection<string> MemberPermissions => SocialPermissions.Member;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SocialDbContext>(configuration, SocialDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(SocialModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<SocialModule>(includeInternalTypes: true);
        services.AddScoped<IPortalSubject, ContentPortalSubject>();
        services.AddScheduledJob<ContentReminderJob>(configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => SocialEndpoints.Map(endpoints);
}
