using Hearth.Application.Abstractions;
using Hearth.Application.Authorization;

namespace Hearth.Application.FixedExpenses.ListFixedExpenses;

public sealed class ListFixedExpensesHandler
{
    private readonly AccountAccessGuard _accountAccess;
    private readonly IFixedExpenseRepository _fixedExpenses;

    public ListFixedExpensesHandler(
        AccountAccessGuard accountAccess,
        IFixedExpenseRepository fixedExpenses)
    {
        ArgumentNullException.ThrowIfNull(accountAccess);
        ArgumentNullException.ThrowIfNull(fixedExpenses);

        _accountAccess = accountAccess;
        _fixedExpenses = fixedExpenses;
    }

    public async Task<IReadOnlyList<FixedExpenseDto>> ExecuteAsync(
        ListFixedExpensesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await _accountAccess.GetAuthorizedAccountAsync(query.AccountId, cancellationToken);

        var expenses = await _fixedExpenses.GetByAccountAsync(query.AccountId, cancellationToken);

        return expenses
            .OrderByDescending(expense => expense.StartMonth)
            .ThenBy(expense => expense.Description, StringComparer.OrdinalIgnoreCase)
            .Select(FixedExpenseDto.FromEntity)
            .ToList();
    }
}
