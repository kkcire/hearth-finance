using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryFixedExpenseRepository : IFixedExpenseRepository
{
    private readonly Dictionary<Guid, FixedExpense> _expenses = new();

    public IReadOnlyCollection<FixedExpense> Expenses => _expenses.Values;

    public int UpdateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(FixedExpense expense, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _expenses.Add(expense.Id, expense);
        return Task.CompletedTask;
    }

    public Task<FixedExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_expenses.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<FixedExpense>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<FixedExpense> result = _expenses.Values
            .Where(expense => expense.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateAsync(FixedExpense expense, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _expenses[expense.Id] = expense;
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(FixedExpense expense, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _expenses.Remove(expense.Id);
        DeleteCount++;
        return Task.CompletedTask;
    }
}
