using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Akiron.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Akiron.BuildingBlocks.Persistence;

/// <summary>
/// One change to one audited row: who, when, and each field's old and new value. Written in the
/// same transaction as the change (by <see cref="TenantAuditInterceptor"/>), into the owning
/// module's schema. Unlike the activity timeline (ADR-0009) this is for accountability, not for
/// reading as a story.
/// </summary>
public sealed class AuditChange
{
    private AuditChange(Guid id, TenantId tenantId, string entityType, string entityId, string action, string changes, UserId? userId, DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        Changes = changes;
        UserId = userId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    /// <summary>The empty id for global rows (users, tenants themselves).</summary>
    public TenantId TenantId { get; private set; }

    public string EntityType { get; private set; }

    public string EntityId { get; private set; }

    /// <summary><c>created</c>, <c>updated</c> or <c>deleted</c>.</summary>
    public string Action { get; private set; }

    /// <summary>JSON: <c>{ "name": { "old": "A", "new": "B" } }</c>.</summary>
    public string Changes { get; private set; }

    public UserId? UserId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private static readonly ConcurrentDictionary<(Type, string), bool> Ignored = new();
    private static readonly HashSet<string> StampProperties = new(StringComparer.Ordinal)
    {
        nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy), nameof(IAuditable.UpdatedAt), nameof(IAuditable.UpdatedBy),
    };

    /// <summary>Builds the record for a tracked entry, or null when nothing audited changed.</summary>
    internal static AuditChange? From(EntityEntry entry, string action, TenantId tenantId, UserId? userId, DateTimeOffset now)
    {
        var changes = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (property.Metadata.IsPrimaryKey() || StampProperties.Contains(name) || IsIgnored(entry.Metadata.ClrType, name))
            {
                continue;
            }

            if (action == "created")
            {
                changes[ToCamelCase(name)] = new { old = (object?)null, @new = Plain(property.CurrentValue) };
            }
            else if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
            {
                changes[ToCamelCase(name)] = new { old = Plain(property.OriginalValue), @new = Plain(property.CurrentValue) };
            }
        }

        if (changes.Count == 0 && action == "updated")
        {
            return null;
        }

        var key = entry.Metadata.FindPrimaryKey()!.Properties.Select(keyProperty => Plain(entry.Property(keyProperty.Name).CurrentValue));

        return new AuditChange(
            Guid.CreateVersion7(),
            tenantId,
            entry.Metadata.ClrType.Name,
            string.Join("|", key),
            action,
            JsonSerializer.Serialize(changes, JsonSerializerOptions.Web),
            userId,
            now);
    }

    private static bool IsIgnored(Type type, string property) =>
        Ignored.GetOrAdd((type, property), key =>
            key.Item1.GetProperty(key.Item2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetCustomAttribute<AuditIgnoreAttribute>() is not null);

    /// <summary>Typed ids are written as their Guid so the JSON stays readable.</summary>
    private static object? Plain(object? value) =>
        value?.GetType().GetProperty("Value") is { } valueProperty
        && value.GetType().GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ITypedId<>))
            ? valueProperty.GetValue(value)
            : value;

    private static string ToCamelCase(string value) => char.ToLowerInvariant(value[0]) + value[1..];

    internal static void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<AuditChange>(audit =>
        {
            audit.ToTable("audit_changes");
            audit.HasKey(change => change.Id);
            audit.Property(change => change.Id).ValueGeneratedNever();
            audit.Property(change => change.EntityType).HasMaxLength(100);
            audit.Property(change => change.EntityId).HasMaxLength(200);
            audit.Property(change => change.Action).HasMaxLength(20);
            audit.Property(change => change.Changes).HasColumnType("jsonb");
            audit.HasIndex(change => new { change.TenantId, change.EntityType, change.EntityId, change.OccurredAt });
        });
}
