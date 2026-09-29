using Akiron.Contracts;

namespace Akiron.Modules.Timeline.Domain;

/// <summary>Streams an entry can be linked to; the shared list lives in <see cref="Subjects"/>.</summary>
public static class TimelineSubjects
{
    public const string Workspace = Subjects.Workspace;

    public const string User = Subjects.User;

    public static IReadOnlySet<string> All => Subjects.All;
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
    public const string FileUploaded = "files.file.uploaded";

    public static readonly IReadOnlyDictionary<string, string> RequiredPermission = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Who was invited is personnel information; only people who can see the team see it.
        [InvitationSent] = "identity.members.read",
    };
}
