namespace Akiron.Contracts;

/// <summary>
/// Kinds of records other modules can hang things on: timeline entries, notes, files. Each module
/// adds its record types here when it is built (party, work_order, quote …).
/// </summary>
public static class Subjects
{
    /// <summary>The whole organisation; its id is the tenant id.</summary>
    public const string Workspace = "workspace";

    public const string User = "user";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Workspace, User };
}
