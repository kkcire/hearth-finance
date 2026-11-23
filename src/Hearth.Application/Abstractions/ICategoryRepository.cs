using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface ICategoryRepository
{
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all global system categories together with the custom categories owned by <paramref name="ownerUserId"/>.
    /// </summary>
    Task<IReadOnlyList<Category>> GetVisibleForUserAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True if either a global system category or a category owned by <paramref name="ownerUserId"/>
    /// already has the given name (case-insensitive).
    /// </summary>
    Task<bool> ExistsWithNameForUserAsync(
        Guid ownerUserId,
        string name,
        Guid? excludeCategoryId = null,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(Category category, CancellationToken cancellationToken = default);

    Task DeleteAsync(Category category, CancellationToken cancellationToken = default);
}
