using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;

namespace Hearth.Tests.Application.Fakes;

internal sealed class InMemoryDirectExpenseRepository : IDirectExpenseRepository
{
    private readonly Dictionary<Guid, DirectExpense> _expenses = new();

    public IReadOnlyCollection<DirectExpense> Expenses => _expenses.Values;

    public int UpdateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task AddAsync(DirectExpense expense, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _expenses.Add(expense.Id, expense);
        return Task.CompletedTask;
    }

    public Task<DirectExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_expenses.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<DirectExpense>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        IReadOnlyList<DirectExpense> result = _expenses.Values
            .Where(expense => expense.AccountId == accountId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateAsync(DirectExpense expense, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _expenses[expense.Id] = expense;
        UpdateCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(DirectExpense expense, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        _expenses.Remove(expense.Id);
        DeleteCount++;
        return Task.CompletedTask;
    }
}
