using Hearth.Application.Abstractions;
using Hearth.Application.Exceptions;
using Hearth.Domain.Entities;

namespace Hearth.Application.FixedExpenses.ListFixedExpenses;

public sealed class ListFixedExpensesHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IFixedExpenseRepository _fixedExpenses;

    public ListFixedExpensesHandler(
        IAccountRepository accounts,
        IFixedExpenseRepository fixedExpenses)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(fixedExpenses);

        _accounts = accounts;
        _fixedExpenses = fixedExpenses;
    }

    public async Task<IReadOnlyList<FixedExpenseDto>> ExecuteAsync(
        ListFixedExpensesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accounts.GetByIdAsync(query.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), query.AccountId);

        var expenses = await _fixedExpenses.GetByAccountAsync(query.AccountId, cancellationToken);

        return expenses
            .OrderByDescending(expense => expense.StartMonth)
            .ThenBy(expense => expense.Description, StringComparer.OrdinalIgnoreCase)
            .Select(FixedExpenseDto.FromEntity)
            .ToList();
    }
}
