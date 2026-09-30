namespace Akiron.BuildingBlocks.Modules;

/// <summary>All permissions of all loaded modules; used to build role templates and to validate custom roles.</summary>
public sealed class PermissionCatalog(IEnumerable<IModule> modules)
{
    public IReadOnlyList<string> All { get; } = modules
        .SelectMany(module => module.Permissions)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>The built-in Member role's permissions (<see cref="IModule.MemberPermissions"/>).</summary>
    public IReadOnlyList<string> MemberDefaults { get; } = modules
        .SelectMany(module => module.MemberPermissions)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToList();
}
