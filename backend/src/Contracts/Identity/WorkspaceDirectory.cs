namespace Akiron.Contracts.Identity;

/// <summary>The current organisation as documents print it (quotes, later invoices).</summary>
public interface IWorkspaceDirectory
{
    Task<string> CurrentNameAsync(CancellationToken cancellationToken);
}
