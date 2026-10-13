using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Events;

namespace Akiron.Contracts.Portal;

/// <summary>
/// What the portal needs to know about a record a client may open: whose it is, how to name it, and
/// which members of staff hear about it when the client writes or uploads.
/// </summary>
/// <param name="SharesAllFiles">Every file on the record is the client's to see (a post's media); otherwise files are shared one by one.</param>
public sealed record PortalSubjectInfo(Guid PartyId, string Title, IReadOnlyList<Guid> StaffUserIds, bool SharesAllFiles = false);

/// <summary>
/// Implemented by the module that owns a kind of record (<see cref="Subjects"/>) clients can see.
/// Timeline and Files ask it before showing or accepting anything through the portal, so a client
/// only ever reaches records of their own party (ADR-0011).
/// </summary>
public interface IPortalSubject
{
    string SubjectType { get; }

    /// <summary>The record in the current tenant, or null when it does not exist (or is archived).</summary>
    Task<PortalSubjectInfo?> FindAsync(Guid subjectId, CancellationToken cancellationToken);
}

/// <summary>A client contact was given portal access; the invitation link went out by e-mail.</summary>
[IntegrationEventName("portal.user.invited")]
public sealed record PortalUserInvited(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PortalUserId,
    Guid PartyId,
    string FullName,
    string Email,
    Guid InvitedByUserId,
    string? InvitedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A client's portal access was switched off (<paramref name="Enabled"/> false) or back on.</summary>
[IntegrationEventName("portal.user.access_changed")]
public sealed record PortalAccessChanged(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid PortalUserId,
    Guid PartyId,
    string FullName,
    bool Enabled,
    Guid ChangedByUserId,
    string? ChangedByName) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>
/// A message on a record's client-visible thread: from the client (<paramref name="FromClient"/>, staff
/// are notified) or from staff (the client's portal users are e-mailed).
/// </summary>
[IntegrationEventName("portal.message.posted")]
public sealed record ClientMessagePosted(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid EntryId,
    string SubjectType,
    Guid SubjectId,
    Guid PartyId,
    string Title,
    string Excerpt,
    bool FromClient,
    string? AuthorName,
    Guid? AuthorUserId,
    IReadOnlyList<Guid> StaffUserIds) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>A client uploaded a file through the portal (a brief, a logo, a payment receipt).</summary>
[IntegrationEventName("portal.file.uploaded")]
public sealed record ClientFileUploaded(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid FileId,
    string FileName,
    long SizeBytes,
    string SubjectType,
    Guid SubjectId,
    Guid PartyId,
    string Title,
    string ClientName,
    IReadOnlyList<Guid> StaffUserIds) : IntegrationEvent(TenantId, OccurredAt);

/// <summary>Staff shared a file with the client; it now shows in their portal.</summary>
[IntegrationEventName("portal.file.shared")]
public sealed record FileSharedWithClient(
    TenantId TenantId,
    DateTimeOffset OccurredAt,
    Guid FileId,
    string FileName,
    string SubjectType,
    Guid SubjectId,
    Guid PartyId,
    string Title,
    Guid SharedByUserId,
    string? SharedByName) : IntegrationEvent(TenantId, OccurredAt);
