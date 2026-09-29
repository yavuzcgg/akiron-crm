using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

namespace Akiron.BuildingBlocks.Security;

/// <summary>The caller's token must carry <see cref="Permission"/> or <see cref="PermissionNames.All"/>.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var granted = context.User.FindAll(AkironClaimTypes.Permission)
            .Any(claim => claim.Value == requirement.Permission || claim.Value == PermissionNames.All);

        if (granted)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Builds a policy per permission on first use, so endpoints can name any permission without a
/// registration step for each one (the lastik-depo pattern).
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionNames.PolicyPrefix, StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[PermissionNames.PolicyPrefix.Length..]))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

public static class PermissionEndpointExtensions
{
    /// <summary>Usage: <c>group.MapGet("/members", ...).RequirePermission(IdentityPermissions.MembersRead);</c></summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PermissionNames.PolicyPrefix + permission);
}
