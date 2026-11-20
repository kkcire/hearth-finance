using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.DirectExpenses.ListDirectExpenses;

public sealed class ListDirectExpensesHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IDirectExpenseRepository _directExpenses;

    public ListDirectExpensesHandler(
        IAccountRepository accounts,
        IDirectExpenseRepository directExpenses)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(directExpenses);

        _accounts = accounts;
        _directExpenses = directExpenses;
    }

    public async Task<IReadOnlyList<DirectExpenseDto>> ExecuteAsync(
        ListDirectExpensesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), query.AccountId);

        var expenses = await _directExpenses.GetByAccountAsync(query.AccountId, cancellationToken);

        return expenses
            .OrderByDescending(expense => expense.Date)
            .Select(DirectExpenseDto.FromEntity)
            .ToList();
    }
}
