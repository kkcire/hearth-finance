using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryFixedIncomeRepository : IFixedIncomeRepository
{
    private readonly Dictionary<Guid, FixedIncome> _fixedIncomes = [];

    public IReadOnlyCollection<FixedIncome> FixedIncomes => _fixedIncomes.Values;

    public int UpdateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(FixedIncome income, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _fixedIncomes[income.Id] = income;
        return Task.CompletedTask;
    }

    public Task<FixedIncome?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _fixedIncomes.TryGetValue(id, out var income);
        return Task.FromResult(income);
    }

    public Task<IReadOnlyList<FixedIncome>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<FixedIncome> result = _fixedIncomes.Values
            .Where(income => income.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateAsync(FixedIncome income, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        UpdateCount++;
        _fixedIncomes[income.Id] = income;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(FixedIncome income, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        DeleteCount++;
        _fixedIncomes.Remove(income.Id);
        return Task.CompletedTask;
    }
}
