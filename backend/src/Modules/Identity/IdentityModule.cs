using Akiron.BuildingBlocks.Modules;
using Akiron.BuildingBlocks.Persistence;
using Akiron.BuildingBlocks.Security;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Identity.Features.Account;
using Akiron.Modules.Identity.Features.GetSession;
using Akiron.Modules.Identity.Features.Invitations;
using Akiron.Modules.Identity.Features.ListMembers;
using Akiron.Modules.Identity.Features.Login;
using Akiron.Modules.Identity.Features.Logout;
using Akiron.Modules.Identity.Features.RefreshSession;
using Akiron.Modules.Identity.Features.Register;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Identity.Features.SystemRoleSync;
using Akiron.Modules.Identity.Persistence;
using Akiron.Modules.Identity.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Akiron.Modules.Identity;

/// <summary>Organisations, users, memberships, roles and sessions (ADR-0007).</summary>
public sealed class IdentityModule : IModule
{
    public string Name => IdentityDbContext.SchemaName;

    public IReadOnlyCollection<string> Permissions => IdentityPermissions.All;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        services.Configure<JwtOptions>(jwtSection);

        services.AddModuleDbContext<IdentityDbContext>(configuration, IdentityDbContext.SchemaName);
        services.AddHandlersFromAssembly(typeof(IdentityModule).Assembly);
        services.AddValidatorsFromAssemblyContaining<IdentityModule>(includeInternalTypes: true);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<AccessTokenIssuer>();
        services.AddSingleton<AuthCookies>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<InvitationLookup>();
        services.AddScoped<SystemRoleSync>();
        services.AddHostedService<SystemRoleSyncService>();

        services.Configure<ConstraintErrorMap>(map => map.Add(IdentityConstraints.UserEmailUnique, IdentityErrors.EmailTaken));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep claim names as issued ("sub", "tenant_id", "perm") instead of mapping them to long URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = AccessTokenIssuer.SigningKey(jwt),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = AkironClaimTypes.Subject,
                    RoleClaimType = AkironClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue(AuthCookies.AccessCookieName, out var token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
            });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth");
        RegisterEndpoint.Map(auth);
        LoginEndpoint.Map(auth);
        RefreshSessionEndpoint.Map(auth);
        LogoutEndpoint.Map(auth);
        GetSessionEndpoint.Map(auth);
        AccountEndpoints.Map(endpoints, auth);

        ListMembersEndpoint.Map(endpoints);

        var invitations = endpoints.MapGroup("/invitations");
        CreateInvitationEndpoint.Map(invitations);
        ManageInvitationsEndpoints.Map(invitations);
        AcceptInvitationEndpoints.Map(invitations);
    }
}
