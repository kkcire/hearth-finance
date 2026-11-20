using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryCategoryRepository : ICategoryRepository
{
    private readonly Dictionary<Guid, Category> _categories = new();

    public IReadOnlyCollection<Category> Categories => _categories.Values;

    public int UpdateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _categories.Add(category.Id, category);
        return Task.CompletedTask;
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_categories.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<Category>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<Category> result = _categories.Values
            .Where(category => category.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> ExistsWithNameAsync(
        Guid accountId,
        string name,
        Guid? excludeCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        var exists = _categories.Values.Any(category =>
            category.AccountId == accountId
            && (!excludeCategoryId.HasValue || category.Id != excludeCategoryId.Value)
            && string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _categories[category.Id] = category;
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Category category, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _categories.Remove(category.Id);
        DeleteCount++;
        return Task.CompletedTask;
    }
}
