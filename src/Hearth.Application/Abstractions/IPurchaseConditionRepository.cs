using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface IPurchaseConditionRepository
{
    Task AddAsync(PurchaseCondition condition, CancellationToken cancellationToken = default);

    Task<PurchaseCondition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all global system conditions together with the custom conditions owned by <paramref name="ownerUserId"/>.
    /// </summary>
    Task<IReadOnlyList<PurchaseCondition>> GetVisibleForUserAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a visible condition (either global system or owned by <paramref name="ownerUserId"/>)
    /// matching <paramref name="name"/> case-insensitively after trimming.
    /// </summary>
    Task<PurchaseCondition?> FindByNameForUserAsync(
        Guid ownerUserId,
        string name,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(PurchaseCondition condition, CancellationToken cancellationToken = default);
}
