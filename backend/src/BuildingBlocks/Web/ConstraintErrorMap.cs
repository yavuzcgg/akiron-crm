using Akiron.BuildingBlocks.Domain;

namespace Akiron.BuildingBlocks.Web;

/// <summary>
/// Maps database constraint names to the error a client should see when the constraint fires.
/// Handlers check the common cases first; this covers the race where two requests both pass the
/// check and the database refuses the second.
/// </summary>
public sealed class ConstraintErrorMap
{
    private readonly Dictionary<string, Error> _errors = new(StringComparer.Ordinal);

    /// <summary>Called from a module's service registration: <c>map.Add("ix_users_email", IdentityErrors.EmailTaken)</c>.</summary>
    public ConstraintErrorMap Add(string constraintName, Error error)
    {
        _errors[constraintName] = error;
        return this;
    }

    public Error? Find(string? constraintName) =>
        constraintName is not null && _errors.TryGetValue(constraintName, out var error) ? error : null;
}
