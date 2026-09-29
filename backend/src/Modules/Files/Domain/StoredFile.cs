using Akiron.BuildingBlocks.Domain;

namespace Akiron.Modules.Files.Domain;

public readonly record struct StoredFileId(Guid Value) : ITypedId<StoredFileId>
{
    public static StoredFileId New() => new(Guid.CreateVersion7());

    public static StoredFileId From(Guid value) => new(value);
}

/// <summary>
/// A file attached to a record. The bytes live in object storage under <see cref="StorageKey"/>;
/// this row is the index, the permission boundary and the tenant boundary. Deleting only hides the
/// row; the object is purged later (and never while a document still refers to it).
/// </summary>
public sealed class StoredFile : Entity<StoredFileId>, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int FileNameMaxLength = 255;

    private StoredFile(StoredFileId id, TenantId tenantId, string subjectType, Guid subjectId, string fileName, string contentType, long sizeBytes, string storageKey)
        : base(id)
    {
        TenantId = tenantId;
        SubjectType = subjectType;
        SubjectId = subjectId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
    }

    public TenantId TenantId { get; private set; }

    public string SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserId? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public UserId? UpdatedBy { get; private set; }

    public static StoredFile Create(TenantId tenantId, string subjectType, Guid subjectId, string fileName, string contentType, long sizeBytes, DateTimeOffset now)
    {
        var id = StoredFileId.New();

        // Tenant and month in the key keep buckets browsable and make per-tenant export or deletion a prefix operation.
        var key = $"{tenantId.Value:N}/{now:yyyy}/{now:MM}/{id.Value:N}";
        return new StoredFile(id, tenantId, subjectType, subjectId, SafeName(fileName), contentType, sizeBytes, key);
    }

    /// <summary>Keeps only the last path segment and drops control characters; the name is shown and sent back in a header.</summary>
    private static string SafeName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (name.Length == 0)
        {
            name = "file";
        }

        return name.Length <= FileNameMaxLength ? name : name[^FileNameMaxLength..];
    }
}
