using Akiron.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;

namespace Akiron.BuildingBlocks.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public UserId? UserId
    {
        get
        {
            var subject = httpContextAccessor.HttpContext?.User.FindFirst(AkironClaimTypes.Subject)?.Value;
            return Guid.TryParse(subject, out var id) ? Domain.UserId.From(id) : null;
        }
    }

    public string? DisplayName => httpContextAccessor.HttpContext?.User.FindFirst(AkironClaimTypes.Name)?.Value;

    public bool HasPermission(string permission) => httpContextAccessor.HttpContext?.User.HasPermission(permission) ?? false;
}
