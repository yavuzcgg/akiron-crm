using Akiron.Contracts;

namespace Akiron.Modules.Timeline.Domain;

/// <summary>Streams an entry can be linked to; the shared list lives in <see cref="Subjects"/>.</summary>
public static class TimelineSubjects
{
    public const string Workspace = Subjects.Workspace;

    public const string User = Subjects.User;

    public const string Party = Subjects.Party;

    public const string WorkOrder = Subjects.WorkOrder;

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
    public const string WorkspaceRenamed = "identity.workspace.renamed";
    public const string MemberJoined = "identity.member.joined";
    public const string InvitationSent = "identity.invitation.sent";
    public const string FileUploaded = "files.file.uploaded";
    public const string PartyCreated = "crm.party.created";
    public const string PartyUpdated = "crm.party.updated";
    public const string PartyArchived = "crm.party.archived";
    public const string PartyContactAdded = "crm.contact.added";
    public const string WorkOrderCreated = "jobs.work_order.created";
    public const string WorkOrderMoved = "jobs.work_order.moved";

    private const string PartiesRead = "crm.parties.read";
    private const string WorkOrdersRead = "jobs.work_orders.read";

    public static readonly IReadOnlyDictionary<string, string> RequiredPermission = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Who was invited is personnel information; only people who can see the team see it.
        [InvitationSent] = "identity.members.read",

        // Client names and contacts are commercial information.
        [PartyCreated] = PartiesRead,
        [PartyUpdated] = PartiesRead,
        [PartyArchived] = PartiesRead,
        [PartyContactAdded] = PartiesRead,
        [WorkOrderCreated] = WorkOrdersRead,
        [WorkOrderMoved] = WorkOrdersRead,
    };
}
