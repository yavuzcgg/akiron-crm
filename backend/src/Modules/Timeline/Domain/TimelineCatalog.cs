namespace Akiron.Modules.Timeline.Domain;

/// <summary>Streams an entry can be linked to. Each module adds its subjects when it is built.</summary>
public static class TimelineSubjects
{
    /// <summary>The whole organisation; the dashboard feed. Its id is the tenant id.</summary>
    public const string Workspace = "workspace";

    public const string User = "user";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Workspace, User };
}

/// <summary>
/// Entry types and the permission each needs beyond <c>timeline.read</c>. A type missing here
/// needs nothing extra; finance types, for instance, will require a finance permission.
/// </summary>
public static class TimelineEntryTypes
{
    public const string Note = "timeline.note";
    public const string WorkspaceCreated = "identity.workspace.created";
    public const string MemberJoined = "identity.member.joined";
    public const string InvitationSent = "identity.invitation.sent";

    public static readonly IReadOnlyDictionary<string, string> RequiredPermission = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Who was invited is personnel information; only people who can see the team see it.
        [InvitationSent] = "identity.members.read",
    };
}
