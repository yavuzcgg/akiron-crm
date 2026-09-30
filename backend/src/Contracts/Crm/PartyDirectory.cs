namespace Akiron.Contracts.Crm;

public sealed record PartySummary(Guid PartyId, string Code, string Name);

/// <summary>Read side of CRM for other modules: which parties exist in the current tenant.</summary>
public interface IPartyDirectory
{
    /// <summary>The party, or null when the current tenant has no such (unarchived) party.</summary>
    Task<PartySummary?> FindAsync(Guid partyId, CancellationToken cancellationToken);
}
