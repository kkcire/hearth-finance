using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryIncomeRepository : IIncomeRepository
{
    private readonly Dictionary<Guid, Income> _incomes = new();

    public IReadOnlyCollection<Income> Incomes => _incomes.Values;

    public int UpdateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(Income income, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _incomes.Add(income.Id, income);
        return Task.CompletedTask;
    }

    public Task<Income?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_incomes.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<Income>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<Income> result = _incomes.Values
            .Where(income => income.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateAsync(Income income, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _incomes[income.Id] = income;
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Income income, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _incomes.Remove(income.Id);
        DeleteCount++;
        return Task.CompletedTask;
    }
}
