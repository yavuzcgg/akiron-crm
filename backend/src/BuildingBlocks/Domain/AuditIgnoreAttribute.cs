namespace Akiron.BuildingBlocks.Domain;

/// <summary>
/// Keeps a property out of the audit trail: secrets (password hashes, token hashes) and noise that
/// changes on every request (last sign-in time).
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class AuditIgnoreAttribute : Attribute;
