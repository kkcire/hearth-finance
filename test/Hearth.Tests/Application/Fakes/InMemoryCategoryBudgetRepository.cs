using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryCategoryBudgetRepository : ICategoryBudgetRepository
{
    private readonly Dictionary<Guid, CategoryBudget> _budgets = new();

    public IReadOnlyCollection<CategoryBudget> Budgets => _budgets.Values;

    public int UpdateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(CategoryBudget budget, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _budgets.Add(budget.Id, budget);
        return Task.CompletedTask;
    }

    public Task<CategoryBudget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_budgets.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<CategoryBudget>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<CategoryBudget> result = _budgets.Values
            .Where(budget => budget.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<CategoryBudget>> GetByAccountAndCategoryAsync(
        Guid accountId,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<CategoryBudget> result = _budgets.Values
            .Where(budget => budget.AccountId == accountId && budget.CategoryId == categoryId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> AnyByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_budgets.Values.Any(budget => budget.CategoryId == categoryId));
    }

    public Task UpdateAsync(CategoryBudget budget, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _budgets[budget.Id] = budget;
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CategoryBudget budget, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _budgets.Remove(budget.Id);
        DeleteCount++;
        return Task.CompletedTask;
    }
}
