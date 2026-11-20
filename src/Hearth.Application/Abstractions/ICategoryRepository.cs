using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface ICategoryRepository
{
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsWithNameAsync(
        Guid accountId,
        string name,
        Guid? excludeCategoryId = null,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(Category category, CancellationToken cancellationToken = default);

    Task DeleteAsync(Category category, CancellationToken cancellationToken = default);
}
