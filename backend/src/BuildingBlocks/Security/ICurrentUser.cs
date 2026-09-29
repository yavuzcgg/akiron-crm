using Akiron.BuildingBlocks.Domain;

namespace Akiron.BuildingBlocks.Security;

/// <summary>Who is acting in this scope; null for anonymous requests and system work.</summary>
public interface ICurrentUser
{
    UserId? UserId { get; }
}
