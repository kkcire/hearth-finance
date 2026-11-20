namespace Hearth.Application.Abstractions;

/// <summary>
/// Commits all pending changes made during a use case in a single transaction.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
