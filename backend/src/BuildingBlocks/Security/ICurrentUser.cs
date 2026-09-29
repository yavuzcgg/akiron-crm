using Akiron.BuildingBlocks.Domain;

namespace Akiron.BuildingBlocks.Security;

/// <summary>Who is acting in this scope; null for anonymous requests and system work.</summary>
public interface ICurrentUser
{
    UserId? UserId { get; }

    /// <summary>Display name from the access token, for snapshots such as timeline actors.</summary>
    string? DisplayName { get; }
}
