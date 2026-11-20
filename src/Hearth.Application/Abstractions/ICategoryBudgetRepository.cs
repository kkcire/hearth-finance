using Hearth.Domain.Entities;

namespace Hearth.Application.Abstractions;

public interface ICategoryBudgetRepository
{
    Task AddAsync(CategoryBudget budget, CancellationToken cancellationToken = default);

    Task<CategoryBudget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryBudget>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryBudget>> GetByAccountAndCategoryAsync(
        Guid accountId,
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<bool> AnyByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task UpdateAsync(CategoryBudget budget, CancellationToken cancellationToken = default);

    Task DeleteAsync(CategoryBudget budget, CancellationToken cancellationToken = default);
}
