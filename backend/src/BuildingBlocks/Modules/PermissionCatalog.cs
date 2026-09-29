namespace Akiron.BuildingBlocks.Modules;

/// <summary>All permissions of all loaded modules; used to build role templates and to validate custom roles.</summary>
public sealed class PermissionCatalog(IEnumerable<IModule> modules)
{
    public IReadOnlyList<string> All { get; } = modules
        .SelectMany(module => module.Permissions)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToList();
}
